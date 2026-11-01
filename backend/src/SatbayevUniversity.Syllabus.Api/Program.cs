using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SatbayevUniversity.Syllabus.Api.Auth;
using SatbayevUniversity.Syllabus.Api.Infrastructure;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Infrastructure.Export;
using SatbayevUniversity.Syllabus.Infrastructure.Services;
using Serilog;
using Serilog.Events;

// Ранний логгер — чтобы зафиксировать ошибки конфигурации и старта.
Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    // ---------------------------------------------------------------- Журналирование (Serilog)
    builder.Host.UseSerilog((ctx, services, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Application", "SatbayevUniversity.Syllabus.Api"));

    // ---------------------------------------------------------------- База данных (MS SQL Server)
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlServer(config.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure(5)));

    // ---------------------------------------------------------------- Identity + JWT
    builder.Services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireNonAlphanumeric = false;
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 10;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager();

    var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    if (jwt.SigningKey.Length < 32)
        throw new InvalidOperationException("Jwt:SigningKey должен быть задан и содержать не менее 32 символов.");
    builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = TokenService.ValidationParameters(jwt);
            o.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = c =>
                {
                    c.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Auth")
                        .LogWarning("Отклонён JWT: {Reason}", c.Exception.GetType().Name);
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization(o =>
    {
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        o.AddPolicy("Staff", p => p.RequireRole(Roles.Admin, Roles.Director, Roles.HeadOfDepartment, Roles.Teacher));
        o.AddPolicy("Admin", p => p.RequireRole(Roles.Admin));
    });

    // ---------------------------------------------------------------- Защита входа от перебора (429)
    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = config.GetValue("RateLimit:LoginPerMinute", 20),
                Window = TimeSpan.FromMinutes(1)
            }));
        o.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimit")
                .LogWarning("Превышен лимит запросов входа с адреса {Ip}", ctx.HttpContext.Connection.RemoteIpAddress);
            await ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = ctx.HttpContext,
                ProblemDetails = { Status = 429, Title = "Слишком много попыток входа. Повторите через минуту." }
            });
        };
    });

    // ---------------------------------------------------------------- Сервисы модуля
    builder.Services.Configure<UmkdOptions>(config.GetSection("Umkd"));
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<TokenService>();
    builder.Services.AddScoped<SyllabusService>();
    builder.Services.AddScoped<WorkflowService>();
    builder.Services.AddScoped<UmkdService>();
    builder.Services.AddSingleton<SyllabusDocxExporter>();

    // ---------------------------------------------------------------- Ошибки и валидация
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = GlobalExceptionHandler.Customize);
    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .ConfigureApiBehaviorOptions(o =>
        {
            var defaultFactory = o.InvalidModelStateResponseFactory;
            o.InvalidModelStateResponseFactory = ctx =>
            {
                var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                    .Select(kv => $"{kv.Key}: {string.Join("; ", kv.Value!.Errors.Select(e => e.ErrorMessage))}");
                ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Validation")
                    .LogWarning("Ошибка валидации {Request}: {Errors}", $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}", string.Join(" | ", errors));
                return defaultFactory(ctx);
            };
        });

    builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

    var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new OpenApiInfo { Title = "SatbayevUniversity.Syllabus API", Version = "v1", Description = "Электронный силлабус + УМКД" });
        o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
            Description = "JWT из POST /api/auth/login"
        });
        o.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
        });
        var xml = Path.Combine(AppContext.BaseDirectory, "SatbayevUniversity.Syllabus.Api.xml");
        if (File.Exists(xml)) o.IncludeXmlComments(xml);
    });

    var app = builder.Build();

    // ---------------------------------------------------------------- Миграции и начальные данные
    if (config.GetValue("Database:MigrateOnStartup", true))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsRelational() && db.Database.ProviderName!.Contains("SqlServer")) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
        await SeedData.InitializeAsync(scope.ServiceProvider, config);
        Log.Information("База данных готова (миграции применены)");
    }

    // Журнал запросов — снаружи обработчика ошибок, чтобы фиксировать итоговый статус (например, 503, а не 500).
    app.UseSerilogRequestLogging(o =>
    {
        o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} за {Elapsed:0} мс (пользователь {UserId})";
        o.GetLevel = (http, _, ex) => ex != null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
            : http.Response.StatusCode >= 400 ? LogEventLevel.Warning
            : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
            : LogEventLevel.Information;
        o.EnrichDiagnosticContext = (diag, http) =>
        {
            diag.Set("UserId", http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "аноним");
            diag.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString() ?? "");
        };
    });
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.DocumentTitle = "Syllabus API");
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (http, report) =>
        {
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsJsonAsync(new
            {
                status = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "ok" : "unhealthy",
                checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
            });
        }
    }).AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Приложение остановлено из-за ошибки при запуске");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
