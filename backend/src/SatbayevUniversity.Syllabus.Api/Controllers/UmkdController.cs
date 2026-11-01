using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SatbayevUniversity.Syllabus.Api.Auth;
using SatbayevUniversity.Syllabus.Api.Contracts;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Infrastructure.Export;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Controllers;

/// <summary>Учебно-методический комплекс дисциплины (УМКД).</summary>
[ApiController, Authorize]
public class UmkdController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SyllabusService _syllabi;
    private readonly UmkdService _umkd;
    private readonly SyllabusDocxExporter _docx;
    private readonly ICurrentUser _me;

    public UmkdController(AppDbContext db, SyllabusService syllabi, UmkdService umkd, SyllabusDocxExporter docx, ICurrentUser me)
    {
        _db = db;
        _syllabi = syllabi;
        _umkd = umkd;
        _docx = docx;
        _me = me;
    }

    /// <summary>Состав УМКД силлабуса: компоненты, материалы и их статус проверки.</summary>
    [HttpGet("api/syllabi/{syllabusId:int}/umkd")]
    public async Task<ActionResult<UmkdDto>> Get(int syllabusId)
    {
        var u = await _me.GetAsync();
        var s = await _syllabi.LoadFullAsync(syllabusId);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanView(u, s)) return Forbid();
        s.Materials = s.Materials.Where(m => AccessPolicy.CanViewMaterial(u, s, m)).ToList();

        var summary = UmkdService.Summarize(s, await _umkd.TypesAsync());
        var components = summary.Components
            .Where(c => u.IsStaff || c.Type.IsSyllabus || c.Materials.Count > 0)
            .Select(c => new UmkdComponentDto(c.Type.ToDto(), c.IsComplete, c.StateText,
                c.Materials.Select(m => MaterialDto.From(m, AccessPolicy.CanDeleteMaterial(u, s, m))).ToList()))
            .ToList();
        return new UmkdDto(s.Id, UmkdProgressDto.From(summary), AccessPolicy.CanUploadMaterials(u, s),
            AccessPolicy.CanReviewMaterials(u, s), components);
    }

    /// <summary>Загрузка материала (multipart/form-data): файл и/или ссылка.</summary>
    [HttpPost("api/syllabi/{syllabusId:int}/umkd"), Authorize(Policy = "Staff")]
    [RequestFormLimits(MultipartBodyLengthLimit = 60L * 1024 * 1024), RequestSizeLimit(60L * 1024 * 1024)]
    public async Task<ActionResult<MaterialDto>> Upload(int syllabusId, [FromForm] int componentTypeId, [FromForm] string? title,
        [FromForm] string? description, IFormFile? file, [FromForm] string? url, [FromForm] bool visibleToStudents = false)
    {
        var u = await _me.GetAsync();
        var s = await _syllabi.LoadFullAsync(syllabusId);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanUploadMaterials(u, s)) return Forbid();

        var type = await _db.UmkdComponentTypes.FindAsync(componentTypeId);
        await using var stream = file?.OpenReadStream();
        var upload = file is { Length: > 0 } ? new UploadedFile(stream!, file.FileName, file.ContentType, file.Length) : null;
        var error = type == null ? "Выберите компонент УМКД."
            : type.IsSyllabus ? "Этот компонент формируется из силлабуса автоматически."
            : _umkd.ValidateUpload(upload, url);
        if (error == null && string.IsNullOrWhiteSpace(title)) title = file?.FileName ?? url;
        if (error == null && string.IsNullOrWhiteSpace(title)) error = "Укажите название материала.";
        if (error != null) return Problem(title: error, statusCode: 400);

        var m = await _umkd.AddAsync(u, s, componentTypeId, title!, description, upload, url, visibleToStudents);
        return MaterialDto.From(m, AccessPolicy.CanDeleteMaterial(u, s, m));
    }

    [HttpDelete("api/umkd/materials/{id:int}"), Authorize(Policy = "Staff")]
    public async Task<IActionResult> Delete(int id)
    {
        var (u, s, m) = await LoadAsync(id);
        if (m == null || s == null) return NotFound();
        if (!AccessPolicy.CanDeleteMaterial(u, s, m)) return Forbid();
        await _umkd.DeleteAsync(m);
        return NoContent();
    }

    /// <summary>Проверка материала заведующим кафедрой: принять или вернуть на доработку.</summary>
    [HttpPost("api/umkd/materials/{id:int}/review"), Authorize(Policy = "Staff")]
    public async Task<ActionResult<MaterialDto>> Review(int id, ReviewRequest req)
    {
        var (u, s, m) = await LoadAsync(id);
        if (m == null || s == null) return NotFound();
        if (!AccessPolicy.CanReviewMaterials(u, s)) return Forbid();
        if (req.Status == MaterialReviewStatus.Rejected && string.IsNullOrWhiteSpace(req.Comment))
            return Problem(title: "Укажите, что нужно доработать.", statusCode: 400);
        await _umkd.ReviewAsync(m, req.Status, req.Comment);
        return MaterialDto.From(m, AccessPolicy.CanDeleteMaterial(u, s, m));
    }

    [HttpPost("api/umkd/materials/{id:int}/visibility"), Authorize(Policy = "Staff")]
    public async Task<ActionResult<MaterialDto>> Visibility(int id, VisibilityRequest req)
    {
        var (u, s, m) = await LoadAsync(id);
        if (m == null || s == null) return NotFound();
        if (!AccessPolicy.CanChangeVisibility(u, s)) return Forbid();
        m.VisibleToStudents = req.Visible;
        await _db.SaveChangesAsync();
        return MaterialDto.From(m, AccessPolicy.CanDeleteMaterial(u, s, m));
    }

    [HttpGet("api/umkd/materials/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var (u, s, m) = await LoadAsync(id);
        if (m == null || s == null) return NotFound();
        if (!AccessPolicy.CanViewMaterial(u, s, m)) return Forbid();
        if (m.StoredFileName == null) return m.ExternalUrl != null ? Redirect(m.ExternalUrl) : NotFound();
        var path = _umkd.PathFor(m);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, m.ContentType ?? "application/octet-stream", m.OriginalFileName ?? "file");
    }

    /// <summary>Весь УМКД одним архивом: силлабус DOCX + материалы по компонентам + опись.</summary>
    [HttpGet("api/syllabi/{syllabusId:int}/umkd/zip")]
    public async Task<IActionResult> Zip(int syllabusId)
    {
        var u = await _me.GetAsync();
        var s = await _syllabi.LoadFullAsync(syllabusId);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanView(u, s)) return Forbid();
        var settings = await _db.TemplateSettings.AsNoTracking().FirstOrDefaultAsync() ?? new TemplateSettings();
        var scale = await _db.GradeScale.AsNoTracking().OrderBy(r => r.Order).ToListAsync();
        var ms = new MemoryStream();
        await _umkd.WriteZipAsync(ms, s, await _umkd.TypesAsync(), _docx.Export(s, settings, scale),
            m => AccessPolicy.CanViewMaterial(u, s, m));
        ms.Position = 0;
        return File(ms, "application/zip", $"УМКД {s.Discipline!.Code} {s.AcademicYear}.zip");
    }

    /// <summary>Сводная матрица комплектности УМКД по видимым силлабусам за учебный год.</summary>
    [HttpGet("api/umkd/overview"), Authorize(Policy = "Staff")]
    public async Task<UmkdOverviewDto> Overview(string? year)
    {
        var u = await _me.GetAsync();
        var q = _syllabi.VisibleTo(u).Include(s => s.Materials).AsNoTracking().Where(s => s.Status != SyllabusStatus.Archived);
        var years = await q.Select(s => s.AcademicYear).Distinct().OrderByDescending(y => y).ToListAsync();
        year ??= years.FirstOrDefault();
        if (year != null) q = q.Where(s => s.AcademicYear == year);
        var items = await q.OrderBy(s => s.Discipline!.Code).ToListAsync();
        var types = await _umkd.TypesAsync();

        var rows = items.Select(s =>
        {
            var sum = UmkdService.Summarize(s, types);
            var cells = sum.Components.Select(c =>
                c.IsComplete ? "done"
                : c.Materials.Any(m => m.ReviewStatus == MaterialReviewStatus.Pending) ? "pending"
                : c.Materials.Count > 0 ? "rejected"
                : c.Type.IsRequired ? "missing" : "optional").ToList();
            return new UmkdOverviewRowDto(s.Id, s.Discipline!.Code, s.Discipline.Name, s.Lecturer.FullName, s.Status,
                s.Status.DisplayName(), UmkdProgressDto.From(sum), cells, AccessPolicy.CanUploadMaterials(u, s));
        }).ToList();
        return new UmkdOverviewDto(years, year, types.Select(t => t.ToDto()).ToList(), rows);
    }

    private async Task<(UserContext, CourseSyllabus?, UmkdMaterial?)> LoadAsync(int materialId)
    {
        var u = await _me.GetAsync();
        var m = await _db.UmkdMaterials.FirstOrDefaultAsync(x => x.Id == materialId);
        if (m == null) return (u, null, null);
        var s = await _syllabi.QueryHeaders().AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.SyllabusId);
        return (u, s, m);
    }
}
