using Microsoft.Extensions.Logging;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Domain;

namespace SatbayevUniversity.Syllabus.Infrastructure.Services;

public enum WorkflowAction { Submit, Withdraw, DepartmentApprove, Return, Approve, Archive }

public sealed class WorkflowException : Exception
{
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public WorkflowException(string message, IReadOnlyList<ValidationIssue>? issues = null) : base(message) =>
        Issues = issues ?? Array.Empty<ValidationIssue>();
}

public sealed record WorkflowRequest(
    WorkflowAction Action,
    string? Comment = null,
    string? ProtocolNumber = null,
    DateOnly? ProtocolDate = null);

/// <summary>
/// Маршрут согласования силлабуса:
/// Черновик → На рассмотрении кафедры → Одобрен кафедрой (протокол) → Утверждён директором.
/// Возврат на доработку возможен на любом из этапов согласования.
/// </summary>
public class WorkflowService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<WorkflowService> _log;

    public WorkflowService(AppDbContext db, TimeProvider time, ILogger<WorkflowService>? log = null)
    {
        _db = db;
        _time = time;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowService>.Instance;
    }

    public static IEnumerable<WorkflowAction> AvailableActions(UserContext u, CourseSyllabus s)
    {
        if (AccessPolicy.CanSubmit(u, s)) yield return WorkflowAction.Submit;
        if (AccessPolicy.CanWithdraw(u, s)) yield return WorkflowAction.Withdraw;
        if (AccessPolicy.CanDepartmentReview(u, s))
        {
            yield return WorkflowAction.DepartmentApprove;
            yield return WorkflowAction.Return;
        }
        if (AccessPolicy.CanFinalApprove(u, s))
        {
            yield return WorkflowAction.Approve;
            yield return WorkflowAction.Return;
        }
        if (AccessPolicy.CanArchive(u, s)) yield return WorkflowAction.Archive;
    }

    public static string Title(WorkflowAction a) => a switch
    {
        WorkflowAction.Submit => "Отправить на рассмотрение кафедры",
        WorkflowAction.Withdraw => "Отозвать",
        WorkflowAction.DepartmentApprove => "Одобрить на заседании кафедры",
        WorkflowAction.Return => "Вернуть на доработку",
        WorkflowAction.Approve => "Утвердить",
        WorkflowAction.Archive => "Перенести в архив",
        _ => a.ToString()
    };

    /// <summary>Выполняет переход. Силлабус должен быть загружен со всеми разделами.</summary>
    public async Task ExecuteAsync(UserContext u, CourseSyllabus s, WorkflowRequest req, string? institutePositionTitle = null)
    {
        if (!AvailableActions(u, s).Contains(req.Action))
        {
            _log.LogWarning("Отказ в действии {Action} над силлабусом {SyllabusId} (статус {Status}) пользователю {UserId}",
                req.Action, s.Id, s.Status, u.UserId);
            throw new WorkflowException("Действие недоступно для текущего статуса или роли.");
        }

        var from = s.Status;
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        SyllabusStatus to;

        switch (req.Action)
        {
            case WorkflowAction.Submit:
                var errors = SyllabusValidator.Validate(s).Where(i => i.Severity == IssueSeverity.Error).ToList();
                if (errors.Count > 0)
                {
                    _log.LogInformation("Силлабус {SyllabusId} не прошёл проверку при отправке: {Errors} ошибок", s.Id, errors.Count);
                    throw new WorkflowException("Силлабус не прошёл проверку: исправьте ошибки перед отправкой.", errors);
                }
                to = SyllabusStatus.OnDepartmentReview;
                break;

            case WorkflowAction.Withdraw:
                to = SyllabusStatus.Draft;
                break;

            case WorkflowAction.DepartmentApprove:
                if (string.IsNullOrWhiteSpace(req.ProtocolNumber) || req.ProtocolDate is null)
                    throw new WorkflowException("Укажите номер и дату протокола заседания кафедры.");
                s.ProtocolNumber = req.ProtocolNumber.Trim();
                s.ProtocolDate = req.ProtocolDate;
                to = SyllabusStatus.DepartmentApproved;
                break;

            case WorkflowAction.Return:
                if (string.IsNullOrWhiteSpace(req.Comment))
                    throw new WorkflowException("При возврате на доработку комментарий обязателен.");
                to = SyllabusStatus.Returned;
                break;

            case WorkflowAction.Approve:
                s.ApprovedByName = ShortName(u.FullName);
                s.ApprovedByTitle = institutePositionTitle ?? "Директор института";
                s.ApprovedDate = today;
                to = SyllabusStatus.Approved;
                break;

            case WorkflowAction.Archive:
                to = SyllabusStatus.Archived;
                break;

            default:
                throw new WorkflowException("Неизвестное действие.");
        }

        s.Status = to;
        s.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        _db.ApprovalLog.Add(new ApprovalLogEntry
        {
            SyllabusId = s.Id,
            FromStatus = from,
            ToStatus = to,
            Action = Title(req.Action),
            Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim(),
            UserId = u.UserId,
            UserName = u.FullName,
            At = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();
        _log.LogInformation("Аудит: {User} ({UserId}) — «{Action}» силлабуса {SyllabusId}: {From} → {To}",
            u.FullName, u.UserId, Title(req.Action), s.Id, from, to);
    }

    /// <summary>«Дайненко Евгения Александровна» → «Дайненко Е.А.»</summary>
    public static string ShortName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return fullName;
        return parts[0] + " " + string.Concat(parts.Skip(1).Take(2).Select(p => p[0] + "."));
    }
}
