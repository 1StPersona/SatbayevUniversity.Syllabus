using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Api.Contracts;

public static class Mapping
{
    public static DisciplineDto ToDto(this Discipline d) =>
        new(d.Id, d.Code, d.Name, d.Cycle, d.Credits, d.CreditStructure, d.DepartmentId, d.Department?.Name);

    public static ProgramDto ToDto(this EducationalProgram p) => new(p.Id, p.Code, p.Name, p.Level, p.DepartmentId);

    public static InstituteDto ToDto(this Institute i) => new(i.Id, i.Name, i.ShortName, i.DirectorName);

    public static DepartmentDto ToDto(this Department d) => new(d.Id, d.Name, d.HeadName, d.HeadTitle, d.InstituteId);

    public static UmkdTypeDto ToDto(this UmkdComponentType t) =>
        new(t.Id, t.Name, t.Description, t.IsRequired, t.Order, t.IsSyllabus, t.VisibleToStudentsByDefault);

    public static SettingsDto ToDto(this TemplateSettings s) => new(s.UniversityName, s.UniversityShortName, s.FormCode,
        s.DefaultCriteriaNote, s.DefaultLatePolicy, s.DefaultAcademicPolicy, s.AcquaintanceNote);

    public static SyllabusListItemDto ToListItem(this CourseSyllabus s, UserContext u, UmkdSummary? umkd) => new(
        s.Id, s.Discipline!.Code, s.Discipline.Name, s.Discipline.Credits, s.AcademicYear, s.Course, s.Semester,
        s.Programs.Select(p => p.EducationalProgram!.Code).OrderBy(c => c).ToList(),
        s.Lecturer.FullName, s.Status, s.Status.DisplayName(), s.UpdatedAt, s.AuthorId == u.UserId,
        umkd == null ? null : UmkdProgressDto.From(umkd));

    public static SyllabusDetailsDto ToDetails(this CourseSyllabus s, UserContext u, UmkdSummary umkd)
    {
        var (rating, final) = SyllabusValidator.Totals(s);
        var inst = s.Discipline!.Department!.Institute!;
        return new SyllabusDetailsDto
        {
            Id = s.Id,
            Data = SyllabusService.ToModel(s),
            Discipline = s.Discipline.ToDto(),
            DepartmentName = s.Discipline.Department.Name,
            InstituteName = inst.Name,
            InstituteShortName = inst.ShortName,
            DirectorName = inst.DirectorName,
            Programs = s.Programs.Select(p => p.EducationalProgram!.ToDto()).OrderBy(p => p.Code).ToList(),
            Status = s.Status,
            StatusName = s.Status.DisplayName(),
            ProtocolNumber = s.ProtocolNumber,
            ProtocolDate = s.ProtocolDate,
            ApprovedByName = s.ApprovedByName,
            ApprovedByTitle = s.ApprovedByTitle,
            ApprovedDate = s.ApprovedDate,
            AuthorName = s.Author?.FullName ?? "",
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            RatingTotal = rating,
            FinalTotal = final,
            ReturnComment = s.Status == SyllabusStatus.Returned
                ? s.History.LastOrDefault(h => h.ToStatus == SyllabusStatus.Returned)?.Comment
                : null,
            Permissions = new PermissionsDto(
                AccessPolicy.CanEdit(u, s), AccessPolicy.CanDelete(u, s), AccessPolicy.CanClone(u, s),
                AccessPolicy.CanManageMaterials(u, s), AccessPolicy.CanReviewMaterials(u, s)),
            Actions = WorkflowService.AvailableActions(u, s).Distinct()
                .Select(a => new ActionDto(a, WorkflowService.Title(a))).ToList(),
            Issues = u.IsStaff ? SyllabusValidator.Validate(s).Select(IssueDto.From).ToList() : Array.Empty<IssueDto>(),
            History = u.IsStaff
                ? s.History.OrderByDescending(h => h.At)
                    .Select(h => new HistoryDto(h.At, h.Action, h.ToStatus, h.ToStatus.DisplayName(), h.UserName, h.Comment)).ToList()
                : Array.Empty<HistoryDto>(),
            Umkd = UmkdProgressDto.From(umkd),
        };
    }
}
