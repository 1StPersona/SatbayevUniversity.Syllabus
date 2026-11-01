using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SatbayevUniversity.Syllabus.Api.Auth;
using SatbayevUniversity.Syllabus.Api.Contracts;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Infrastructure.Dtos;
using SatbayevUniversity.Syllabus.Infrastructure.Export;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Controllers;

/// <summary>Электронный силлабус: реестр, редактирование, согласование, экспорт.</summary>
[ApiController, Authorize]
[Route("api/syllabi")]
public class SyllabiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SyllabusService _service;
    private readonly WorkflowService _workflow;
    private readonly UmkdService _umkd;
    private readonly SyllabusDocxExporter _docx;
    private readonly ICurrentUser _me;
    private readonly ILogger<SyllabiController> _log;

    public SyllabiController(AppDbContext db, SyllabusService service, WorkflowService workflow, UmkdService umkd,
        SyllabusDocxExporter docx, ICurrentUser me, ILogger<SyllabiController> log)
    {
        _log = log;
        _db = db;
        _service = service;
        _workflow = workflow;
        _umkd = umkd;
        _docx = docx;
        _me = me;
    }

    /// <summary>Реестр силлабусов с фильтрами. По умолчанию архивные не показываются.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<SyllabusListItemDto>> List(string? q, SyllabusStatus? status, string? year, int? programId)
    {
        var u = await _me.GetAsync();
        var query = _service.VisibleTo(u).Include(s => s.Materials).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Discipline!.Code.Contains(term) || s.Discipline!.Name.Contains(term) ||
                                     s.Lecturer.FullName.Contains(term));
        }
        query = status != null ? query.Where(s => s.Status == status) : query.Where(s => s.Status != SyllabusStatus.Archived);
        if (!string.IsNullOrEmpty(year)) query = query.Where(s => s.AcademicYear == year);
        if (programId != null) query = query.Where(s => s.Programs.Any(p => p.EducationalProgramId == programId));

        var items = await query.OrderByDescending(s => s.AcademicYear).ThenBy(s => s.Discipline!.Code).ToListAsync();
        var types = u.IsStaff ? await _umkd.TypesAsync() : null;
        return items.Select(s => s.ToListItem(u, types == null ? null : UmkdService.Summarize(s, types))).ToList();
    }

    [HttpGet("years")]
    public async Task<IReadOnlyList<string>> Years()
    {
        var u = await _me.GetAsync();
        return await _service.VisibleTo(u).Select(s => s.AcademicYear).Distinct().OrderByDescending(y => y).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SyllabusDetailsDto>> Get(int id)
    {
        var u = await _me.GetAsync();
        var s = await _service.LoadFullAsync(id);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanView(u, s)) return Forbid();
        return s.ToDetails(u, UmkdService.Summarize(s, await _umkd.TypesAsync()));
    }

    /// <summary>Заготовка нового силлабуса: типовые тексты политик, 15 недель, типовой график 60/40.</summary>
    [HttpGet("new"), Authorize(Policy = "Staff")]
    public async Task<SyllabusEditModel> Template(int? disciplineId) =>
        await _service.NewModelAsync(await _me.GetAsync(), disciplineId);

    /// <summary>Проверка содержимого на соответствие шаблону без сохранения.</summary>
    [HttpPost("validate"), Authorize(Policy = "Staff")]
    public IReadOnlyList<IssueDto> Validate(SyllabusEditModel m)
    {
        var s = new CourseSyllabus();
        _service.Apply(m, s);
        return SyllabusValidator.Validate(s).Select(IssueDto.From).ToList();
    }

    [HttpPost, Authorize(Policy = "Staff")]
    public async Task<ActionResult<CreatedDto>> Create(SyllabusEditModel m)
    {
        var u = await _me.GetAsync();
        var discipline = await _db.Disciplines.AsNoTracking().FirstOrDefaultAsync(d => d.Id == m.DisciplineId);
        if (discipline == null) return ValidationError(nameof(m.DisciplineId), "Выберите дисциплину.");
        if (!AccessPolicy.CanCreateFor(u, discipline)) return Forbid();
        if (await CheckProgramsAsync(m) is { } bad) return bad;
        var s = await _service.CreateAsync(u, m);
        _log.LogInformation("Аудит: {User} ({UserId}) создал силлабус {SyllabusId} ({Discipline}, {Year})", u.FullName, u.UserId, s.Id, discipline.Code, s.AcademicYear);
        return CreatedAtAction(nameof(Get), new { id = s.Id }, new CreatedDto(s.Id));
    }

    [HttpPut("{id:int}"), Authorize(Policy = "Staff")]
    public async Task<IActionResult> Update(int id, SyllabusEditModel m)
    {
        var u = await _me.GetAsync();
        var s = await _service.LoadFullAsync(id, tracking: true);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanEdit(u, s)) return Forbid();
        var discipline = await _db.Disciplines.AsNoTracking().FirstOrDefaultAsync(d => d.Id == m.DisciplineId);
        if (discipline == null) return ValidationError(nameof(m.DisciplineId), "Выберите дисциплину.");
        if (discipline.Id != s.DisciplineId && !AccessPolicy.CanCreateFor(u, discipline)) return Forbid();
        if (await CheckProgramsAsync(m) is { } bad) return bad;
        _service.Apply(m, s);
        await _db.SaveChangesAsync();
        _log.LogInformation("Аудит: {User} ({UserId}) изменил силлабус {SyllabusId}", u.FullName, u.UserId, s.Id);
        return NoContent();
    }

    [HttpDelete("{id:int}"), Authorize(Policy = "Staff")]
    public async Task<IActionResult> Delete(int id)
    {
        var u = await _me.GetAsync();
        var s = await _service.LoadFullAsync(id, tracking: true);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanDelete(u, s)) return Forbid();
        foreach (var m in s.Materials.ToList()) await _umkd.DeleteAsync(m);
        _db.Syllabi.Remove(s);
        await _db.SaveChangesAsync();
        _log.LogInformation("Аудит: {User} ({UserId}) удалил черновик силлабуса {SyllabusId}", u.FullName, u.UserId, id);
        return NoContent();
    }

    /// <summary>Копия силлабуса на новый учебный год (черновик, без материалов УМКД).</summary>
    [HttpPost("{id:int}/clone"), Authorize(Policy = "Staff")]
    public async Task<ActionResult<CreatedDto>> Clone(int id, CloneRequest req)
    {
        var u = await _me.GetAsync();
        var s = await _service.LoadFullAsync(id);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanClone(u, s)) return Forbid();
        var year = string.IsNullOrWhiteSpace(req.AcademicYear) ? SyllabusService.NextAcademicYear(s.AcademicYear) : req.AcademicYear.Trim();
        if (!Regex.IsMatch(year, @"^\d{4}-\d{4}$")) return ValidationError("academicYear", "Учебный год указывается в формате 2027-2028.");
        var copy = await _service.CloneAsync(u, s, year);
        _log.LogInformation("Аудит: {User} ({UserId}) скопировал силлабус {SourceId} → {SyllabusId} на {Year}", u.FullName, u.UserId, s.Id, copy.Id, year);
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, new CreatedDto(copy.Id));
    }

    /// <summary>Действие маршрута согласования: отправить, одобрить на кафедре, утвердить, вернуть, отозвать, архивировать.</summary>
    [HttpPost("{id:int}/workflow")]
    public async Task<ActionResult<SyllabusDetailsDto>> Workflow(int id, WorkflowRequestDto req)
    {
        var u = await _me.GetAsync();
        var s = await _service.LoadFullAsync(id, tracking: true);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanView(u, s)) return Forbid();
        try
        {
            var inst = s.Discipline?.Department?.Institute;
            await _workflow.ExecuteAsync(u, s, new WorkflowRequest(req.Action, req.Comment, req.ProtocolNumber, req.ProtocolDate),
                inst != null ? $"Директор института {inst.ShortName}" : null);
        }
        catch (WorkflowException ex)
        {
            var problem = new ProblemDetails { Title = ex.Message, Status = 400 };
            problem.Extensions["issues"] = ex.Issues.Select(IssueDto.From).ToList();
            return BadRequest(problem);
        }
        var fresh = await _service.LoadFullAsync(id);
        return fresh!.ToDetails(u, UmkdService.Summarize(fresh!, await _umkd.TypesAsync()));
    }

    /// <summary>Силлабус в формате DOCX по форме Ф КазНИТУ 703-08.</summary>
    [HttpGet("{id:int}/docx")]
    public async Task<IActionResult> Docx(int id)
    {
        var u = await _me.GetAsync();
        var s = await _service.LoadFullAsync(id);
        if (s == null) return NotFound();
        if (!AccessPolicy.CanView(u, s)) return Forbid();
        var bytes = _docx.Export(s, await SettingsAsync(), await ScaleAsync());
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"Силлабус {s.Discipline!.Code} {s.AcademicYear}.docx");
    }

    private async Task<TemplateSettings> SettingsAsync() =>
        await _db.TemplateSettings.AsNoTracking().FirstOrDefaultAsync() ?? new TemplateSettings();

    private async Task<List<GradeScaleRow>> ScaleAsync() =>
        await _db.GradeScale.AsNoTracking().OrderBy(r => r.Order).ToListAsync();

    private async Task<ActionResult?> CheckProgramsAsync(SyllabusEditModel m)
    {
        var ids = m.ProgramIds.Distinct().ToList();
        if (ids.Count == 0) return null;
        var known = await _db.EducationalPrograms.CountAsync(p => ids.Contains(p.Id));
        return known == ids.Count ? null : ValidationError(nameof(m.ProgramIds), "Неизвестная образовательная программа.");
    }

    private ActionResult ValidationError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
