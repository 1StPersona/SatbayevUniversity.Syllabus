using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SatbayevUniversity.Syllabus.Api.Auth;
using SatbayevUniversity.Syllabus.Api.Contracts;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Controllers;

[ApiController, Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SyllabusService _syllabi;
    private readonly UmkdService _umkd;
    private readonly ICurrentUser _me;

    public DashboardController(AppDbContext db, SyllabusService syllabi, UmkdService umkd, ICurrentUser me)
    {
        _db = db;
        _syllabi = syllabi;
        _umkd = umkd;
        _me = me;
    }

    [HttpGet]
    public async Task<DashboardDto> Get()
    {
        var u = await _me.GetAsync();
        var visible = _syllabi.VisibleTo(u);
        var types = await _umkd.TypesAsync();

        if (!u.IsStaff)
        {
            var published = await visible.AsNoTracking().OrderBy(s => s.Course).ThenBy(s => s.Semester)
                .ThenBy(s => s.Discipline!.Code).ToListAsync();
            return new DashboardDto(new Dictionary<string, int>(), [], [], 0,
                published.Select(s => s.ToListItem(u, null)).ToList());
        }

        var byStatus = (await visible.GroupBy(s => s.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
            .ToDictionary(x => x.Key.ToString(), x => x.Count);

        var mine = await visible.Include(s => s.Materials).AsNoTracking()
            .Where(s => s.AuthorId == u.UserId && s.Status != SyllabusStatus.Archived)
            .OrderByDescending(s => s.UpdatedAt).Take(10).ToListAsync();

        var awaiting = await visible.AsNoTracking()
            .Where(s => s.Status == SyllabusStatus.OnDepartmentReview || s.Status == SyllabusStatus.DepartmentApproved)
            .OrderBy(s => s.UpdatedAt).ToListAsync();
        awaiting = awaiting.Where(s => AccessPolicy.CanDepartmentReview(u, s) || AccessPolicy.CanFinalApprove(u, s)).ToList();

        var toReview = 0;
        if (u.Is(Roles.HeadOfDepartment) || u.IsAdmin)
            toReview = await _db.UmkdMaterials.CountAsync(x => x.ReviewStatus == MaterialReviewStatus.Pending &&
                (u.IsAdmin || x.Syllabus!.Discipline!.DepartmentId == u.DepartmentId));

        return new DashboardDto(byStatus,
            mine.Select(s => s.ToListItem(u, UmkdService.Summarize(s, types))).ToList(),
            awaiting.Select(s => s.ToListItem(u, null)).ToList(),
            toReview, []);
    }
}
