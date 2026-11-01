using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SatbayevUniversity.Syllabus.Api.Auth;
using SatbayevUniversity.Syllabus.Api.Contracts;
using SatbayevUniversity.Syllabus.Domain;

namespace SatbayevUniversity.Syllabus.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthController> _log;

    public AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, TokenService tokens,
        ILogger<AuthController> log)
    {
        _log = log;
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
    }

    /// <summary>Вход по e-mail и паролю. Возвращает JWT для заголовка Authorization: Bearer.</summary>
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var email = req.Email?.Trim() ?? "";
        var user = await _users.FindByEmailAsync(email);
        if (user == null)
        {
            _log.LogWarning("Неудачный вход: пользователь {Email} не найден", email);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Неверный e-mail или пароль." });
        }

        var check = await _signIn.CheckPasswordSignInAsync(user, req.Password ?? "", lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            _log.LogWarning("Вход заблокирован: учётная запись {Email} временно заблокирована", email);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Учётная запись временно заблокирована." });
        }
        if (!check.Succeeded)
        {
            _log.LogWarning("Неудачный вход: неверный пароль для {Email}", email);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Неверный e-mail или пароль." });
        }

        var roles = await _users.GetRolesAsync(user);
        var (token, expires) = _tokens.Issue(user, roles);
        _log.LogInformation("Вход выполнен: {Email} ({UserId}), роли {Roles}", email, user.Id, string.Join(",", roles));
        return new LoginResponse(token, expires, ToDto(user, roles));
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();
        return ToDto(user, await _users.GetRolesAsync(user));
    }

    private static UserDto ToDto(ApplicationUser u, IList<string> roles) =>
        new(u.Id, u.Email ?? "", u.FullName, u.Regalia, roles.ToList(), u.DepartmentId, u.InstituteId, u.EducationalProgramId);
}
