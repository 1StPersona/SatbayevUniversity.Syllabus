using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Dtos;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Contracts;

// ---------------------------------------------------------------- Аутентификация

public record LoginRequest(string Email, string Password);

public record UserDto(string Id, string Email, string FullName, string? Regalia, IReadOnlyList<string> Roles,
    int? DepartmentId, int? InstituteId, int? EducationalProgramId);

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------------------------------------------------------------- Справочники

public record EnumItem(string Value, string Name);

public record InstituteDto(int Id, string Name, string ShortName, string DirectorName);

public record DepartmentDto(int Id, string Name, string HeadName, string HeadTitle, int InstituteId);

public record ProgramDto(int Id, string Code, string Name, StudyLevel Level, int DepartmentId);

public record DisciplineDto(int Id, string Code, string Name, DisciplineCycle Cycle, int Credits, string CreditStructure,
    int DepartmentId, string? DepartmentName);

public record GradeScaleDto(string Letter, decimal Gpa, int MinPercent, int MaxPercent, string Criterion, string Traditional);

public record UmkdTypeDto(int Id, string Name, string Description, bool IsRequired, int Order, bool IsSyllabus,
    bool VisibleToStudentsByDefault);

public record SettingsDto(string UniversityName, string UniversityShortName, string FormCode, string DefaultCriteriaNote,
    string DefaultLatePolicy, string DefaultAcademicPolicy, string AcquaintanceNote);

public record ReferenceDto(
    SettingsDto Settings,
    IReadOnlyList<GradeScaleDto> GradeScale,
    IReadOnlyList<string> CompetencyDescriptors,
    IReadOnlyList<string> CompetencyCategories,
    string CriteriaIntro,
    IReadOnlyList<IReadOnlyList<string>> CriteriaRows,
    IReadOnlyList<DisciplineDto> Disciplines,
    IReadOnlyList<ProgramDto> Programs,
    IReadOnlyList<UmkdTypeDto> UmkdTypes,
    IReadOnlyList<EnumItem> Statuses);

// ---------------------------------------------------------------- Силлабусы

public record UmkdProgressDto(int Done, int Total, int Percent, bool IsComplete)
{
    public static UmkdProgressDto From(UmkdSummary s) => new(s.RequiredDone, s.RequiredTotal, s.Percent, s.IsComplete);
}

public record SyllabusListItemDto(int Id, string DisciplineCode, string DisciplineName, int Credits, string AcademicYear,
    int Course, int Semester, IReadOnlyList<string> Programs, string Lecturer, SyllabusStatus Status, string StatusName,
    DateTime UpdatedAt, bool IsMine, UmkdProgressDto? Umkd);

public record PermissionsDto(bool CanEdit, bool CanDelete, bool CanClone, bool CanManageMaterials, bool CanReviewMaterials);

public record ActionDto(WorkflowAction Action, string Title);

public record IssueDto(string Severity, string Section, string Message)
{
    public static IssueDto From(ValidationIssue i) => new(i.Severity == IssueSeverity.Error ? "error" : "warning", i.Section, i.Message);
}

public record HistoryDto(DateTime At, string Action, SyllabusStatus ToStatus, string StatusName, string UserName, string? Comment);

public class SyllabusDetailsDto
{
    public int Id { get; set; }
    /// <summary>Содержимое силлабуса (та же форма, что и при редактировании).</summary>
    public SyllabusEditModel Data { get; set; } = new();
    public DisciplineDto Discipline { get; set; } = null!;
    public string DepartmentName { get; set; } = "";
    public string InstituteName { get; set; } = "";
    public string InstituteShortName { get; set; } = "";
    public string DirectorName { get; set; } = "";
    public IReadOnlyList<ProgramDto> Programs { get; set; } = Array.Empty<ProgramDto>();
    public SyllabusStatus Status { get; set; }
    public string StatusName { get; set; } = "";
    public string? ProtocolNumber { get; set; }
    public DateOnly? ProtocolDate { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ApprovedByTitle { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public string AuthorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public decimal RatingTotal { get; set; }
    public decimal FinalTotal { get; set; }
    public string? ReturnComment { get; set; }
    public PermissionsDto Permissions { get; set; } = null!;
    public IReadOnlyList<ActionDto> Actions { get; set; } = Array.Empty<ActionDto>();
    public IReadOnlyList<IssueDto> Issues { get; set; } = Array.Empty<IssueDto>();
    public IReadOnlyList<HistoryDto> History { get; set; } = Array.Empty<HistoryDto>();
    public UmkdProgressDto Umkd { get; set; } = null!;
}

public record WorkflowRequestDto(WorkflowAction Action, string? Comment, string? ProtocolNumber, DateOnly? ProtocolDate);

public record CloneRequest(string? AcademicYear);

public record CreatedDto(int Id);

// ---------------------------------------------------------------- УМКД

public record MaterialDto(int Id, int ComponentTypeId, string Title, string? Description, string? FileName, long FileSize,
    string? ExternalUrl, bool HasFile, bool VisibleToStudents, MaterialReviewStatus ReviewStatus, string ReviewStatusName,
    string? ReviewComment, string UploadedByName, DateTime UploadedAt)
{
    public static MaterialDto From(UmkdMaterial m) => new(m.Id, m.ComponentTypeId, m.Title, m.Description, m.OriginalFileName,
        m.FileSize, m.ExternalUrl, m.StoredFileName != null, m.VisibleToStudents, m.ReviewStatus, m.ReviewStatus.DisplayName(),
        m.ReviewComment, m.UploadedByName, m.UploadedAt);
}

public record UmkdComponentDto(UmkdTypeDto Type, bool IsComplete, string StateText, IReadOnlyList<MaterialDto> Materials);

public record UmkdDto(int SyllabusId, UmkdProgressDto Progress, bool CanManage, bool CanReview, IReadOnlyList<UmkdComponentDto> Components);

public record ReviewRequest(MaterialReviewStatus Status, string? Comment);

public record VisibilityRequest(bool Visible);

public record UmkdOverviewRowDto(int SyllabusId, string DisciplineCode, string DisciplineName, string Lecturer,
    SyllabusStatus Status, string StatusName, UmkdProgressDto Progress, IReadOnlyList<string> Cells);

public record UmkdOverviewDto(IReadOnlyList<string> Years, string? Year, IReadOnlyList<UmkdTypeDto> Types, IReadOnlyList<UmkdOverviewRowDto> Rows);

// ---------------------------------------------------------------- Рабочий стол

public record DashboardDto(
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyList<SyllabusListItemDto> Mine,
    IReadOnlyList<SyllabusListItemDto> AwaitingMe,
    int MaterialsToReview,
    IReadOnlyList<SyllabusListItemDto> Published);

// ---------------------------------------------------------------- Администрирование

public record AdminUserDto(string Id, string Email, string FullName, string? Regalia, IReadOnlyList<string> Roles,
    int? DepartmentId, string? DepartmentName, int? InstituteId, int? EducationalProgramId, string? ProgramCode);

public record SaveUserRequest(string Email, string FullName, string? Regalia, IReadOnlyList<string> Roles,
    int? DepartmentId, int? InstituteId, int? EducationalProgramId, string? Password);
