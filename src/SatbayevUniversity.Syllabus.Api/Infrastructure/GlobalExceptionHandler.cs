using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Infrastructure;

/// <summary>
/// Единая обработка необработанных исключений: каждое превращается в ProblemDetails
/// с понятным сообщением на русском и traceId, по которому ошибку можно найти в журнале.
/// Подробности исключения клиенту не отдаются.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _log;
    private readonly IProblemDetailsService _problems;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> log, IProblemDetailsService problems)
    {
        _log = log;
        _problems = problems;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title, level) = Classify(ex, http);
        var path = $"{http.Request.Method} {http.Request.Path}";

        if (level == LogLevel.Error)
            _log.LogError(ex, "Необработанная ошибка при {Request}: {Message}", path, ex.Message);
        else
            _log.Log(level, "Запрос {Request} завершился ошибкой {Status}: {Exception}: {Message}", path, status, ex.GetType().Name, ex.Message);

        if (http.Response.HasStarted) return true;
        http.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Instance = http.Request.Path };
        if (ex is WorkflowException wf && wf.Issues.Count > 0)
            problem.Extensions["issues"] = wf.Issues.Select(i => new { severity = i.Severity == IssueSeverity.Error ? "error" : "warning", i.Section, i.Message });
        return await _problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem, Exception = ex });
    }

    public static (int Status, string Title, LogLevel Level) Classify(Exception ex, HttpContext http) => ex switch
    {
        OperationCanceledException when http.RequestAborted.IsCancellationRequested
            => (499, "Запрос отменён клиентом.", LogLevel.Information),
        BadHttpRequestException b when b.StatusCode == StatusCodes.Status413PayloadTooLarge
            => (413, "Размер запроса превышает допустимый (файл слишком большой).", LogLevel.Warning),
        BadHttpRequestException b
            => (b.StatusCode, "Некорректный запрос.", LogLevel.Warning),
        WorkflowException w
            => (400, w.Message, LogLevel.Warning),
        DbUpdateConcurrencyException
            => (409, "Данные были изменены другим пользователем. Обновите страницу и повторите действие.", LogLevel.Warning),
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }
            => (409, "Запись с такими данными уже существует.", LogLevel.Warning),
        DbUpdateException { InnerException: SqlException { Number: 547 } }
            => (409, "Действие нарушает связи между данными (запись используется или ссылается на несуществующую).", LogLevel.Warning),
        SqlException or TimeoutException or InvalidOperationException { InnerException: SqlException }
            => (503, "База данных временно недоступна. Повторите попытку позже.", LogLevel.Error),
        UnauthorizedAccessException
            => (403, "Недостаточно прав для этого действия.", LogLevel.Warning),
        KeyNotFoundException or FileNotFoundException
            => (404, "Запрашиваемые данные не найдены.", LogLevel.Warning),
        InvalidDataException or FormatException
            => (400, "Некорректный формат данных.", LogLevel.Warning),
        IOException
            => (500, "Ошибка файлового хранилища УМКД.", LogLevel.Error),
        _ => (500, "Внутренняя ошибка сервера.", LogLevel.Error)
    };

    /// <summary>Добавляет traceId во все ответы ProblemDetails (в т.ч. 400/401/403/404).</summary>
    public static void Customize(ProblemDetailsContext ctx)
    {
        ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
        // Стандартные английские заголовки заменяем русскими; собственные заголовки контроллеров не трогаем.
        var title = ctx.ProblemDetails.Title;
        if (title is null || EnglishDefaults.Contains(title))
            ctx.ProblemDetails.Title = ctx.ProblemDetails.Status switch
            {
                400 => "Некорректные данные запроса.",
                401 => "Требуется вход в систему.",
                403 => "Недостаточно прав для этого действия.",
                404 => "Ресурс не найден.",
                405 => "Метод не поддерживается.",
                413 => "Размер запроса превышает допустимый.",
                415 => "Неподдерживаемый формат данных.",
                429 => "Слишком много запросов. Повторите попытку позже.",
                _ => title
            };
    }

    private static readonly HashSet<string> EnglishDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bad Request", "Unauthorized", "Forbidden", "Not Found", "Method Not Allowed", "Payload Too Large",
        "Content Too Large", "Unsupported Media Type", "Too Many Requests", "One or more validation errors occurred."
    };
}
