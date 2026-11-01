using Microsoft.Extensions.Logging;
using System.IO.Compression;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Domain;
using Microsoft.EntityFrameworkCore;

namespace SatbayevUniversity.Syllabus.Infrastructure.Services;

public sealed record UmkdComponentState(
    UmkdComponentType Type,
    IReadOnlyList<UmkdMaterial> Materials,
    bool IsComplete,
    string StateText);

public sealed record UmkdSummary(IReadOnlyList<UmkdComponentState> Components, int RequiredTotal, int RequiredDone)
{
    public int Percent => RequiredTotal == 0 ? 100 : (int)Math.Round(100.0 * RequiredDone / RequiredTotal);
    public bool IsComplete => RequiredDone == RequiredTotal;
}

/// <summary>Загружаемый файл (не зависит от ASP.NET IFormFile).</summary>
public sealed record UploadedFile(Stream Content, string FileName, string? ContentType, long Length);

public class UmkdOptions
{
    public string StoragePath { get; set; } = "App_Data/umkd";
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } =
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".odt", ".odp", ".ods",
        ".txt", ".rtf", ".zip", ".rar", ".7z", ".png", ".jpg", ".jpeg", ".mp4", ".mp3", ".ipynb", ".tex"
    };
}

/// <summary>Учебно-методический комплекс дисциплины: хранение материалов и контроль комплектности.</summary>
public class UmkdService
{
    private readonly AppDbContext _db;
    private readonly string _root;
    private readonly UmkdOptions _options;
    private readonly TimeProvider _time;

    private readonly ILogger<UmkdService> _log;

    public UmkdService(AppDbContext db, Microsoft.Extensions.Options.IOptions<UmkdOptions> options, TimeProvider time,
        ILogger<UmkdService>? log = null)
    {
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UmkdService>.Instance;
        _db = db;
        _options = options.Value;
        _time = time;
        _root = Path.GetFullPath(_options.StoragePath);
        Directory.CreateDirectory(_root);
    }

    public UmkdOptions Options => _options;

    public static UmkdSummary Summarize(CourseSyllabus s, IReadOnlyList<UmkdComponentType> types)
    {
        var states = types.OrderBy(t => t.Order).Select(t =>
        {
            var mats = s.Materials.Where(m => m.ComponentTypeId == t.Id).OrderBy(m => m.UploadedAt).ToList();
            bool done;
            string text;
            if (t.IsSyllabus)
            {
                done = s.Status == SyllabusStatus.Approved;
                text = done ? "Силлабус утверждён" : "Ожидает утверждения силлабуса";
            }
            else if (mats.Any(m => m.ReviewStatus == MaterialReviewStatus.Accepted))
            {
                done = true;
                text = "Принято";
            }
            else if (mats.Any(m => m.ReviewStatus == MaterialReviewStatus.Pending))
            {
                done = false;
                text = "На проверке";
            }
            else if (mats.Count > 0)
            {
                done = false;
                text = "Требует доработки";
            }
            else
            {
                done = false;
                text = t.IsRequired ? "Не загружено" : "Не загружено (необязательно)";
            }
            return new UmkdComponentState(t, mats, done, text);
        }).ToList();

        var required = states.Where(x => x.Type.IsRequired).ToList();
        return new UmkdSummary(states, required.Count, required.Count(x => x.IsComplete));
    }

    public async Task<List<UmkdComponentType>> TypesAsync() =>
        await _db.UmkdComponentTypes.AsNoTracking().OrderBy(t => t.Order).ToListAsync();

    public string? ValidateUpload(UploadedFile? file, string? url)
    {
        if ((file == null || file.Length == 0) && string.IsNullOrWhiteSpace(url))
            return "Прикрепите файл или укажите ссылку на электронный ресурс.";
        if (!string.IsNullOrWhiteSpace(url) &&
            (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            return "Ссылка должна начинаться с http:// или https://.";
        if (file is { Length: > 0 })
        {
            if (file.Length > _options.MaxFileSizeBytes)
                return $"Файл больше {_options.MaxFileSizeBytes / 1024 / 1024} МБ.";
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_options.AllowedExtensions.Contains(ext))
                return $"Недопустимый тип файла «{ext}». Разрешены: {string.Join(", ", _options.AllowedExtensions)}.";
        }
        return null;
    }

    public async Task<UmkdMaterial> AddAsync(UserContext u, CourseSyllabus s, int componentTypeId, string title,
        string? description, UploadedFile? file, string? url, bool visibleToStudents)
    {
        var m = new UmkdMaterial
        {
            SyllabusId = s.Id,
            ComponentTypeId = componentTypeId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ExternalUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            VisibleToStudents = visibleToStudents,
            UploadedById = u.UserId,
            UploadedByName = u.FullName,
            UploadedAt = _time.GetUtcNow().UtcDateTime,
        };
        if (file is { Length: > 0 })
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            m.StoredFileName = $"{Guid.NewGuid():N}{ext}";
            m.OriginalFileName = Path.GetFileName(file.FileName);
            m.ContentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType;
            m.FileSize = file.Length;
            await using var fs = File.Create(PathFor(m));
            await file.Content.CopyToAsync(fs);
        }
        _db.UmkdMaterials.Add(m);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Запись в БД не удалась — не оставляем «осиротевший» файл в хранилище.
            if (m.StoredFileName != null && File.Exists(PathFor(m))) File.Delete(PathFor(m));
            throw;
        }
        _log.LogInformation("Аудит: {User} ({UserId}) загрузил материал УМКД {MaterialId} «{Title}» ({Size} байт) в силлабус {SyllabusId}",
            u.FullName, u.UserId, m.Id, m.Title, m.FileSize, s.Id);
        return m;
    }

    public async Task DeleteAsync(UmkdMaterial m)
    {
        _db.UmkdMaterials.Remove(m);
        await _db.SaveChangesAsync();
        try
        {
            if (m.StoredFileName != null && File.Exists(PathFor(m))) File.Delete(PathFor(m));
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Не удалось удалить файл материала {MaterialId} ({File})", m.Id, m.StoredFileName);
        }
        _log.LogInformation("Аудит: удалён материал УМКД {MaterialId} «{Title}» силлабуса {SyllabusId}", m.Id, m.Title, m.SyllabusId);
    }

    public async Task ReviewAsync(UmkdMaterial m, MaterialReviewStatus status, string? comment)
    {
        m.ReviewStatus = status;
        m.ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        await _db.SaveChangesAsync();
        _log.LogInformation("Аудит: материал УМКД {MaterialId} силлабуса {SyllabusId} — {Status}", m.Id, m.SyllabusId, status);
    }

    public string PathFor(UmkdMaterial m)
    {
        // Имя файла генерируется сервером (GUID + расширение), но всё равно защищаемся от выхода за каталог.
        var name = Path.GetFileName(m.StoredFileName ?? throw new InvalidOperationException("У материала нет файла."));
        return Path.Combine(_root, name);
    }

    /// <summary>Архив УМКД: силлабус (DOCX) + материалы, разложенные по компонентам.</summary>
    public async Task WriteZipAsync(Stream output, CourseSyllabus s, IReadOnlyList<UmkdComponentType> types,
        byte[] syllabusDocx, Func<UmkdMaterial, bool> include)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var discipline = Safe($"{s.Discipline?.Code} {s.AcademicYear}");

        var entry = zip.CreateEntry($"{discipline}/01 Силлабус {Safe(s.Discipline?.Code ?? "")}.docx");
        await using (var es = entry.Open()) await es.WriteAsync(syllabusDocx);

        var index = new System.Text.StringBuilder();
        index.AppendLine($"УМКД дисциплины {s.Discipline?.Display}, {s.AcademicYear} уч. год");
        index.AppendLine();
        foreach (var (t, i) in types.OrderBy(t => t.Order).Select((t, i) => (t, i)))
        {
            var folder = $"{discipline}/{i + 1:00} {Safe(t.Name)}";
            index.AppendLine($"{i + 1:00}. {t.Name}{(t.IsRequired ? "" : " (необязательный)")}");
            foreach (var m in s.Materials.Where(m => m.ComponentTypeId == t.Id && include(m)).OrderBy(m => m.UploadedAt))
            {
                if (m.StoredFileName != null && File.Exists(PathFor(m)))
                {
                    var fileEntry = zip.CreateEntry($"{folder}/{m.Id}_{Safe(m.OriginalFileName ?? m.Title)}");
                    await using var src = File.OpenRead(PathFor(m));
                    await using var dst = fileEntry.Open();
                    await src.CopyToAsync(dst);
                    index.AppendLine($"    - {m.Title} ({m.OriginalFileName})");
                }
                if (m.ExternalUrl != null) index.AppendLine($"    - {m.Title}: {m.ExternalUrl}");
            }
        }
        var idx = zip.CreateEntry($"{discipline}/Опись УМКД.txt");
        await using var w = new StreamWriter(idx.Open(), new System.Text.UTF8Encoding(true));
        await w.WriteAsync(index.ToString());
    }

    private static string Safe(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }
}
