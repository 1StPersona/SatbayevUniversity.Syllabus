using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Auth;

public interface ICurrentUser
{
    Task<UserContext> GetAsync();
}

/// <summary>
/// Контекст пользователя строится по БД (а не только по токену), чтобы смена кафедры
/// или ролей администратором применялась сразу. Кэшируется на время запроса.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private UserContext? _cached;

    public CurrentUser(IHttpContextAccessor http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task<UserContext> GetAsync()
    {
        if (_cached != null) return _cached;
        var id = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id)) return _cached = UserContext.Anonymous;

        var user = await _db.Users.AsNoTracking().Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return _cached = UserContext.Anonymous;

        var roles = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where ur.UserId == id
                           select r.Name!).ToListAsync();

        return _cached = new UserContext(user.Id, user.FullName, roles.ToHashSet(), user.DepartmentId,
            user.InstituteId ?? user.Department?.InstituteId, user.EducationalProgramId);
    }
}
