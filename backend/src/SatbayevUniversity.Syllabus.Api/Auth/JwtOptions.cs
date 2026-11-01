namespace SatbayevUniversity.Syllabus.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "SatbayevUniversity.Syllabus";
    public string Audience { get; set; } = "SatbayevUniversity.Syllabus.Web.Client";
    /// <summary>Секрет подписи HMAC-SHA256, не короче 32 символов. Задаётся через переменные окружения / user-secrets.</summary>
    public string SigningKey { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 480;
}
