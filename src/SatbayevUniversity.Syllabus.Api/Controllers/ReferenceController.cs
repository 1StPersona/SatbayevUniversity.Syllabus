using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SatbayevUniversity.Syllabus.Api.Contracts;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Infrastructure.Export;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Controllers;

/// <summary>Справочные данные для форм и отображения силлабуса.</summary>
[ApiController, Authorize]
[Route("api/reference")]
public class ReferenceController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReferenceController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ReferenceDto> Get()
    {
        var settings = await _db.TemplateSettings.AsNoTracking().FirstOrDefaultAsync() ?? new TemplateSettings();
        return new ReferenceDto(
            settings.ToDto(),
            await _db.GradeScale.AsNoTracking().OrderBy(r => r.Order)
                .Select(r => new GradeScaleDto(r.Letter, r.Gpa, r.MinPercent, r.MaxPercent, r.Criterion, r.Traditional)).ToListAsync(),
            CompetencyFrame.Descriptors,
            CompetencyFrame.Categories,
            SyllabusDocxExporter.CriteriaIntro,
            SyllabusDocxExporter.CriteriaRows,
            (await _db.Disciplines.AsNoTracking().Include(d => d.Department).OrderBy(d => d.Code).ToListAsync()).Select(d => d.ToDto()).ToList(),
            (await _db.EducationalPrograms.AsNoTracking().OrderBy(p => p.Code).ToListAsync()).Select(p => p.ToDto()).ToList(),
            (await _db.UmkdComponentTypes.AsNoTracking().OrderBy(t => t.Order).ToListAsync()).Select(t => t.ToDto()).ToList(),
            Enum.GetValues<SyllabusStatus>().Select(s => new EnumItem(s.ToString(), s.DisplayName())).ToList());
    }
}
