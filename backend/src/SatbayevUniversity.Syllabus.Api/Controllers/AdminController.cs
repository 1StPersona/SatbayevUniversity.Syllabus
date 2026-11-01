using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SatbayevUniversity.Syllabus.Api.Contracts;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Data;

namespace SatbayevUniversity.Syllabus.Api.Controllers;

/// <summary>Справочники, настройки шаблона и пользователи. Только для администратора.</summary>
[ApiController, Authorize(Policy = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AdminController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    // ---------------------------------------------------------------- Институты и кафедры

    [HttpGet("institutes")]
    public async Task<IReadOnlyList<InstituteDto>> Institutes() =>
        (await _db.Institutes.AsNoTracking().OrderBy(i => i.Name).ToListAsync()).Select(i => i.ToDto()).ToList();

    [HttpPost("institutes")]
    public Task<ActionResult<InstituteDto>> SaveInstitute(InstituteDto d) => Save(d.Id,
        () => new Institute(), e => { e.Name = d.Name.Trim(); e.ShortName = d.ShortName.Trim(); e.DirectorName = d.DirectorName.Trim(); },
        e => e.ToDto(), string.IsNullOrWhiteSpace(d.Name) ? "Укажите название института." : null);

    [HttpGet("departments")]
    public async Task<IReadOnlyList<DepartmentDto>> Departments() =>
        (await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync()).Select(d => d.ToDto()).ToList();

    [HttpPost("departments")]
    public async Task<ActionResult<DepartmentDto>> SaveDepartment(DepartmentDto d) => await Save(d.Id,
        () => new Department(), e => { e.Name = d.Name.Trim(); e.HeadName = d.HeadName.Trim(); e.HeadTitle = d.HeadTitle.Trim(); e.InstituteId = d.InstituteId; },
        e => e.ToDto(),
        string.IsNullOrWhiteSpace(d.Name) ? "Укажите название кафедры."
        : !await _db.Institutes.AnyAsync(i => i.Id == d.InstituteId) ? "Выберите институт." : null);

    // ---------------------------------------------------------------- ОП и дисциплины

    [HttpGet("programs")]
    public async Task<IReadOnlyList<ProgramDto>> Programs() =>
        (await _db.EducationalPrograms.AsNoTracking().OrderBy(p => p.Code).ToListAsync()).Select(p => p.ToDto()).ToList();

    [HttpPost("programs")]
    public async Task<ActionResult<ProgramDto>> SaveProgram(ProgramDto d) => await Save(d.Id,
        () => new EducationalProgram(), e => { e.Code = d.Code.Trim(); e.Name = d.Name.Trim(); e.Level = d.Level; e.DepartmentId = d.DepartmentId; },
        e => e.ToDto(),
        string.IsNullOrWhiteSpace(d.Code) || string.IsNullOrWhiteSpace(d.Name) ? "Укажите шифр и название."
        : await _db.EducationalPrograms.AnyAsync(p => p.Code == d.Code.Trim() && p.Id != d.Id) ? "Программа с таким шифром уже есть."
        : !await _db.Departments.AnyAsync(x => x.Id == d.DepartmentId) ? "Выберите кафедру." : null);

    [HttpGet("disciplines")]
    public async Task<IReadOnlyList<DisciplineDto>> Disciplines() =>
        (await _db.Disciplines.AsNoTracking().Include(d => d.Department).OrderBy(d => d.Code).ToListAsync()).Select(d => d.ToDto()).ToList();

    [HttpPost("disciplines")]
    public async Task<ActionResult<DisciplineDto>> SaveDiscipline(DisciplineDto d) => await Save(d.Id,
        () => new Discipline(),
        e => { e.Code = d.Code.Trim(); e.Name = d.Name.Trim(); e.Cycle = d.Cycle; e.Credits = d.Credits; e.CreditStructure = d.CreditStructure.Trim(); e.DepartmentId = d.DepartmentId; },
        e => e.ToDto(),
        string.IsNullOrWhiteSpace(d.Code) || string.IsNullOrWhiteSpace(d.Name) ? "Укажите код и название."
        : d.Credits is < 1 or > 30 ? "Количество кредитов — от 1 до 30."
        : await _db.Disciplines.AnyAsync(x => x.Code == d.Code.Trim() && x.Id != d.Id) ? "Дисциплина с таким кодом уже есть."
        : !await _db.Departments.AnyAsync(x => x.Id == d.DepartmentId) ? "Выберите кафедру." : null);

    // ---------------------------------------------------------------- УМКД и шаблон

    [HttpGet("umkd-types")]
    public async Task<IReadOnlyList<UmkdTypeDto>> UmkdTypes() =>
        (await _db.UmkdComponentTypes.AsNoTracking().OrderBy(t => t.Order).ToListAsync()).Select(t => t.ToDto()).ToList();

    [HttpPost("umkd-types")]
    public Task<ActionResult<UmkdTypeDto>> SaveUmkdType(UmkdTypeDto d) => Save(d.Id,
        () => new UmkdComponentType(),
        e => { e.Name = d.Name.Trim(); e.Description = d.Description.Trim(); e.IsRequired = d.IsRequired; e.Order = d.Order; e.VisibleToStudentsByDefault = d.VisibleToStudentsByDefault; },
        e => e.ToDto(), string.IsNullOrWhiteSpace(d.Name) ? "Укажите название компонента." : null);

    [HttpGet("settings")]
    public async Task<SettingsDto> Settings() =>
        (await _db.TemplateSettings.AsNoTracking().FirstOrDefaultAsync() ?? new TemplateSettings()).ToDto();

    [HttpPut("settings")]
    public async Task<SettingsDto> SaveSettings(SettingsDto d)
    {
        var s = await _db.TemplateSettings.FirstOrDefaultAsync();
        if (s == null) _db.TemplateSettings.Add(s = new TemplateSettings());
        s.UniversityName = d.UniversityName; s.UniversityShortName = d.UniversityShortName; s.FormCode = d.FormCode;
        s.DefaultCriteriaNote = d.DefaultCriteriaNote; s.DefaultLatePolicy = d.DefaultLatePolicy;
        s.DefaultAcademicPolicy = d.DefaultAcademicPolicy; s.AcquaintanceNote = d.AcquaintanceNote;
        await _db.SaveChangesAsync();
        return s.ToDto();
    }

    // ---------------------------------------------------------------- Пользователи

    [HttpGet("users")]
    public async Task<IReadOnlyList<AdminUserDto>> Users()
    {
        var list = await _db.Users.AsNoTracking().Include(u => u.Department).Include(u => u.EducationalProgram)
            .OrderBy(u => u.FullName).ToListAsync();
        var roles = await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name }).ToListAsync();
        return list.Select(u => new AdminUserDto(u.Id, u.Email ?? "", u.FullName, u.Regalia,
            roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).ToList(), u.DepartmentId, u.Department?.Name,
            u.InstituteId, u.EducationalProgramId, u.EducationalProgram?.Code)).ToList();
    }

    [HttpPost("users")]
    public Task<IActionResult> CreateUser(SaveUserRequest req) => SaveUser(null, req);

    [HttpPut("users/{id}")]
    public Task<IActionResult> UpdateUser(string id, SaveUserRequest req) => SaveUser(id, req);

    private async Task<IActionResult> SaveUser(string? id, SaveUserRequest req)
    {
        var roles = req.Roles.Where(r => Roles.All.Contains(r)).Distinct().ToList();
        if (roles.Count == 0) return Problem(title: "Назначьте хотя бы одну роль.", statusCode: 400);
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.FullName))
            return Problem(title: "Укажите e-mail и ФИО.", statusCode: 400);
        if (id == null && string.IsNullOrEmpty(req.Password)) return Problem(title: "Задайте пароль.", statusCode: 400);

        var u = id == null ? new ApplicationUser() : await _users.FindByIdAsync(id);
        if (u == null) return NotFound();
        u.Email = u.UserName = req.Email.Trim();
        u.EmailConfirmed = true;
        u.FullName = req.FullName.Trim();
        u.Regalia = req.Regalia?.Trim();
        u.DepartmentId = req.DepartmentId;
        u.InstituteId = req.InstituteId;
        u.EducationalProgramId = req.EducationalProgramId;

        var res = id == null ? await _users.CreateAsync(u, req.Password!) : await _users.UpdateAsync(u);
        if (res.Succeeded && id != null && !string.IsNullOrEmpty(req.Password))
        {
            await _users.RemovePasswordAsync(u);
            res = await _users.AddPasswordAsync(u, req.Password);
        }
        if (!res.Succeeded) return Problem(title: string.Join(" ", res.Errors.Select(e => e.Description)), statusCode: 400);

        var current = await _users.GetRolesAsync(u);
        await _users.RemoveFromRolesAsync(u, current.Except(roles));
        await _users.AddToRolesAsync(u, roles.Except(current));
        return Ok(new { u.Id });
    }

    private async Task<ActionResult<TDto>> Save<TEntity, TDto>(int id, Func<TEntity> create, Action<TEntity> apply,
        Func<TEntity, TDto> map, string? error) where TEntity : class
    {
        if (error != null) return Problem(title: error, statusCode: 400);
        TEntity? e;
        if (id == 0) _db.Add(e = create());
        else if ((e = await _db.Set<TEntity>().FindAsync(id)) == null) return NotFound();
        apply(e);
        await _db.SaveChangesAsync();
        return map(e);
    }
}
