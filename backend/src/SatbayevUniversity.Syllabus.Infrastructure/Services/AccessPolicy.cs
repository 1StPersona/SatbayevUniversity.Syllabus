using SatbayevUniversity.Syllabus.Domain;

namespace SatbayevUniversity.Syllabus.Infrastructure.Services;

/// <summary>Снимок текущего пользователя, достаточный для проверки прав.</summary>
public sealed record UserContext(
    string UserId,
    string FullName,
    IReadOnlySet<string> Roles,
    int? DepartmentId,
    int? InstituteId,
    int? ProgramId)
{
    public static readonly UserContext Anonymous = new("", "", new HashSet<string>(), null, null, null);

    public bool IsAuthenticated => UserId.Length > 0;
    public bool Is(string role) => Roles.Contains(role);
    public bool IsAdmin => Is(Domain.Roles.Admin);
    public bool IsStaff => IsAdmin || Is(Domain.Roles.Teacher) || Is(Domain.Roles.HeadOfDepartment) || Is(Domain.Roles.Director);
}

/// <summary>
/// Правила доступа к силлабусам и УМКД. Требует, чтобы у силлабуса была загружена
/// Discipline.Department (для проверок по кафедре и институту) и Programs.
/// </summary>
public static class AccessPolicy
{
    private static int? DeptOf(CourseSyllabus s) => s.Discipline?.DepartmentId;
    private static int? InstOf(CourseSyllabus s) => s.Discipline?.Department?.InstituteId;

    private static bool IsHeadOf(UserContext u, CourseSyllabus s) =>
        u.Is(Roles.HeadOfDepartment) && u.DepartmentId != null && u.DepartmentId == DeptOf(s);

    private static bool IsDirectorOf(UserContext u, CourseSyllabus s) =>
        u.Is(Roles.Director) && u.InstituteId != null && u.InstituteId == InstOf(s);

    public static bool IsAuthor(UserContext u, CourseSyllabus s) => u.IsAuthenticated && s.AuthorId == u.UserId;

    public static bool CanView(UserContext u, CourseSyllabus s)
    {
        if (!u.IsAuthenticated) return false;
        if (u.IsAdmin || IsAuthor(u, s) || IsHeadOf(u, s) || IsDirectorOf(u, s)) return true;
        if (u.Is(Roles.Teacher) && u.DepartmentId != null && u.DepartmentId == DeptOf(s)) return true;
        if (u.Is(Roles.Student)) return IsPublishedFor(u, s);
        return false;
    }

    /// <summary>Обучающийся видит только утверждённые силлабусы своей ОП.</summary>
    public static bool IsPublishedFor(UserContext u, CourseSyllabus s) =>
        s.Status == SyllabusStatus.Approved &&
        (u.ProgramId == null || s.Programs.Any(p => p.EducationalProgramId == u.ProgramId));

    public static bool CanEdit(UserContext u, CourseSyllabus s) =>
        s.IsEditable && (u.IsAdmin || IsAuthor(u, s));

    public static bool CanCreateFor(UserContext u, Discipline d) =>
        u.IsAdmin || ((u.Is(Roles.Teacher) || u.Is(Roles.HeadOfDepartment)) && u.DepartmentId == d.DepartmentId);

    public static bool CanSubmit(UserContext u, CourseSyllabus s) => CanEdit(u, s);

    public static bool CanWithdraw(UserContext u, CourseSyllabus s) =>
        s.Status == SyllabusStatus.OnDepartmentReview && (u.IsAdmin || IsAuthor(u, s));

    public static bool CanDepartmentReview(UserContext u, CourseSyllabus s) =>
        s.Status == SyllabusStatus.OnDepartmentReview && (u.IsAdmin || IsHeadOf(u, s));

    public static bool CanFinalApprove(UserContext u, CourseSyllabus s) =>
        s.Status == SyllabusStatus.DepartmentApproved && (u.IsAdmin || IsDirectorOf(u, s));

    public static bool CanArchive(UserContext u, CourseSyllabus s) =>
        s.Status == SyllabusStatus.Approved && (u.IsAdmin || IsHeadOf(u, s));

    public static bool CanDelete(UserContext u, CourseSyllabus s) =>
        s.Status == SyllabusStatus.Draft && (u.IsAdmin || IsAuthor(u, s));

    public static bool CanClone(UserContext u, CourseSyllabus s) =>
        u.IsAdmin || ((u.Is(Roles.Teacher) || u.Is(Roles.HeadOfDepartment)) && u.DepartmentId == DeptOf(s));

    // ---- УМКД ----

    /// <summary>
    /// Загружать материалы УМКД могут все сотрудники, причастные к дисциплине: автор силлабуса,
    /// преподаватели кафедры, заведующий кафедрой, директор института и администратор (кроме архивных силлабусов).
    /// </summary>
    public static bool CanUploadMaterials(UserContext u, CourseSyllabus s) =>
        s.Status != SyllabusStatus.Archived &&
        (u.IsAdmin || IsAuthor(u, s) || IsHeadOf(u, s) || IsDirectorOf(u, s) ||
         (u.Is(Roles.Teacher) && u.DepartmentId != null && u.DepartmentId == DeptOf(s)));

    /// <summary>Удалить материал может загрузивший его, автор силлабуса, заведующий кафедрой и администратор.</summary>
    public static bool CanDeleteMaterial(UserContext u, CourseSyllabus s, UmkdMaterial m) =>
        s.Status != SyllabusStatus.Archived &&
        (u.IsAdmin || IsAuthor(u, s) || IsHeadOf(u, s) || (u.IsAuthenticated && m.UploadedById == u.UserId));

    /// <summary>Видимость материала для обучающихся меняют загружающие и проверяющие.</summary>
    public static bool CanChangeVisibility(UserContext u, CourseSyllabus s) => CanUploadMaterials(u, s) || CanReviewMaterials(u, s);

    public static bool CanReviewMaterials(UserContext u, CourseSyllabus s) => u.IsAdmin || IsHeadOf(u, s);

    public static bool CanViewMaterial(UserContext u, CourseSyllabus s, UmkdMaterial m)
    {
        if (!CanView(u, s)) return false;
        if (u.IsStaff) return true;
        return m.VisibleToStudents && m.ReviewStatus == MaterialReviewStatus.Accepted;
    }
}
