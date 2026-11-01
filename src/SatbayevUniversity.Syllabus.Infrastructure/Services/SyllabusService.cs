using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace SatbayevUniversity.Syllabus.Infrastructure.Services;

public class SyllabusService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public SyllabusService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>Запрос силлабусов с данными, нужными для проверки прав и списков.</summary>
    public IQueryable<CourseSyllabus> QueryHeaders() => _db.Syllabi
        .Include(s => s.Discipline!).ThenInclude(d => d.Department!).ThenInclude(d => d.Institute)
        .Include(s => s.Programs).ThenInclude(p => p.EducationalProgram)
        .Include(s => s.Author);

    /// <summary>Фильтр видимости на уровне БД (зеркалит <see cref="AccessPolicy.CanView"/>).</summary>
    public IQueryable<CourseSyllabus> VisibleTo(UserContext u)
    {
        var q = QueryHeaders();
        if (u.IsAdmin) return q;
        var isStudentOnly = !u.IsStaff && u.Is(Roles.Student);
        if (isStudentOnly)
            return q.Where(s => s.Status == SyllabusStatus.Approved &&
                                (u.ProgramId == null || s.Programs.Any(p => p.EducationalProgramId == u.ProgramId)));

        var seeDept = u.Is(Roles.Teacher) || u.Is(Roles.HeadOfDepartment);
        var seeInst = u.Is(Roles.Director);
        return q.Where(s => s.AuthorId == u.UserId
                            || (seeDept && u.DepartmentId != null && s.Discipline!.DepartmentId == u.DepartmentId)
                            || (seeInst && u.InstituteId != null && s.Discipline!.Department!.InstituteId == u.InstituteId)
                            || (u.Is(Roles.Student) && s.Status == SyllabusStatus.Approved));
    }

    public async Task<CourseSyllabus?> LoadFullAsync(int id, bool tracking = false)
    {
        var q = QueryHeaders()
            .Include(s => s.Requisites)
            .Include(s => s.CalendarRows)
            .Include(s => s.Literature)
            .Include(s => s.Competencies)
            .Include(s => s.Assessments)
            .Include(s => s.History)
            .Include(s => s.Materials).ThenInclude(m => m.ComponentType)
            .AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        var s = await q.FirstOrDefaultAsync(x => x.Id == id);
        if (s != null) Normalize(s);
        return s;
    }

    /// <summary>Упорядочивает дочерние коллекции для вывода.</summary>
    public static void Normalize(CourseSyllabus s)
    {
        s.Requisites = s.Requisites.OrderBy(r => r.Kind).ThenBy(r => r.Order).ToList();
        s.CalendarRows = s.CalendarRows.OrderBy(r => r.Order).ToList();
        s.Literature = s.Literature.OrderBy(l => l.Number).ToList();
        s.Assessments = s.Assessments.OrderBy(a => a.Order).ToList();
        s.History = s.History.OrderBy(h => h.At).ToList();
    }

    public async Task<SyllabusEditModel> NewModelAsync(UserContext u, int? disciplineId)
    {
        var settings = await _db.TemplateSettings.AsNoTracking().FirstOrDefaultAsync() ?? new TemplateSettings();
        var me = await _db.Users.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == u.UserId);
        var now = _time.GetLocalNow();
        var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
        var teacher = new TeacherInfo { FullName = me?.FullName ?? "", Regalia = me?.Regalia ?? "", Email = me?.Email ?? "" };

        var m = new SyllabusEditModel
        {
            DisciplineId = disciplineId ?? 0,
            AcademicYear = $"{startYear}-{startYear + 1}",
            Lecturer = teacher,
            PracticeTeacher = new TeacherInfo { FullName = teacher.FullName, Regalia = teacher.Regalia, Email = teacher.Email },
            CompilerName = WorkflowService.ShortName(me?.FullName ?? ""),
            HeadOfDepartmentName = me?.Department?.HeadName ?? "",
            HeadOfDepartmentTitle = me?.Department?.HeadTitle ?? "Зав. кафедрой",
            CriteriaNote = settings.DefaultCriteriaNote,
            LatePolicy = settings.DefaultLatePolicy,
            AcademicPolicy = settings.DefaultAcademicPolicy,
            Competencies = Enumerable.Repeat<string?>(null, CompetencyFrame.CellCount).ToList(),
        };
        for (var w = 1; w <= m.WeeksCount; w++)
            m.CalendarRows.Add(new() { Kind = CalendarRowKind.Week, Week = w, Deadline = $"Неделя {w}" });
        m.CalendarRows.Insert(8, new() { Kind = CalendarRowKind.Milestone, LectureTopic = "Первая промежуточная аттестация", Deadline = "Неделя 8" });
        m.CalendarRows.Add(new() { Kind = CalendarRowKind.Milestone, LectureTopic = "Вторая промежуточная аттестация", Deadline = "Неделя 15" });
        m.CalendarRows.Add(new() { Kind = CalendarRowKind.Milestone, LectureTopic = "Итоговый экзамен", Deadline = "по расписанию" });
        m.Assessments = DefaultAssessments(m.WeeksCount);
        return m;
    }

    /// <summary>Типовой график сдачи работ (60 баллов рейтинг + 40 экзамен).</summary>
    public static List<SyllabusEditModel.AssessmentRow> DefaultAssessments(int weeks)
    {
        SyllabusEditModel.AssessmentRow Row(string name, string max, params (int week, decimal pts)[] p)
        {
            var w = Enumerable.Repeat<decimal?>(null, weeks).ToList();
            foreach (var (week, pts) in p) if (week <= weeks) w[week - 1] = pts;
            return new() { Name = name, MaxPerWeek = max, Weeks = w };
        }
        return new()
        {
            Row("Активность на лекционных занятиях", ""),
            Row("Самостоятельная работа обучающегося с преподавателем (СРОП)", "5", (4, 5), (11, 5)),
            Row("Самостоятельная работа обучающегося (СРО)", "5", (7, 5), (13, 5)),
            Row("Выполнение практических заданий", "2/3", (2, 2), (5, 3), (10, 2), (14, 3)),
            Row("Выполнение лабораторных заданий", "2", (3, 2), (6, 2), (9, 2), (12, 2), (14, 2)),
            Row("1-я промежуточная аттестация", "10", (8, 10)),
            Row("2-я промежуточная аттестация", "10", (15, 10)),
            new() { Name = "Итоговый экзамен", MaxPerWeek = "40", IsFinal = true, FinalPoints = 40,
                    Weeks = Enumerable.Repeat<decimal?>(null, weeks).ToList() },
        };
    }

    public static SyllabusEditModel ToModel(CourseSyllabus s)
    {
        var cells = Enumerable.Repeat<string?>(null, CompetencyFrame.CellCount).ToList();
        foreach (var c in s.Competencies)
        {
            var idx = c.DescriptorIndex * CompetencyFrame.Categories.Length + c.CategoryIndex;
            if (idx >= 0 && idx < cells.Count) cells[idx] = c.Text;
        }

        return new SyllabusEditModel
        {
            Id = s.Id,
            DisciplineId = s.DisciplineId,
            AcademicYear = s.AcademicYear,
            Course = s.Course,
            Semester = s.Semester,
            StudyForm = s.StudyForm,
            Language = s.Language,
            City = s.City,
            WeeksCount = s.WeeksCount,
            ProgramIds = s.Programs.Select(p => p.EducationalProgramId).ToList(),
            Lecturer = Copy(s.Lecturer),
            PracticeTeacher = Copy(s.PracticeTeacher),
            Requisites = s.Requisites.OrderBy(r => r.Kind).ThenBy(r => r.Order)
                .Select(r => new SyllabusEditModel.RequisiteRow { Kind = r.Kind, Code = r.Code, Name = r.Name }).ToList(),
            Goal = s.Goal,
            Tasks = s.Tasks,
            Description = s.Description,
            OutcomesKnow = s.OutcomesKnow,
            OutcomesCan = s.OutcomesCan,
            OutcomesSkills = s.OutcomesSkills,
            CalendarRows = s.CalendarRows.OrderBy(r => r.Order).Select(r => new SyllabusEditModel.CalendarRowModel
            {
                Kind = r.Kind, Week = r.Week, LectureTopic = r.LectureTopic, PracticeTopic = r.PracticeTopic,
                SroTopic = r.SroTopic, LiteratureRefs = r.LiteratureRefs, Deadline = r.Deadline
            }).ToList(),
            Literature = s.Literature.OrderBy(l => l.Number)
                .Select(l => new SyllabusEditModel.LiteratureRow { Number = l.Number, Kind = l.Kind, Text = l.Text }).ToList(),
            Competencies = cells,
            Assessments = s.Assessments.OrderBy(a => a.Order).Select(a => new SyllabusEditModel.AssessmentRow
            {
                Name = a.Name, MaxPerWeek = a.MaxPerWeek, IsFinal = a.IsFinal, FinalPoints = a.FinalPoints,
                Weeks = a.GetWeekPoints(s.WeeksCount).ToList()
            }).ToList(),
            CriteriaNote = s.CriteriaNote,
            LatePolicy = s.LatePolicy,
            AcademicPolicy = s.AcademicPolicy,
            HeadOfDepartmentName = s.HeadOfDepartmentName,
            HeadOfDepartmentTitle = s.HeadOfDepartmentTitle,
            CompilerName = s.CompilerName,
            AgreedBy = s.AgreedBy,
        };
    }

    /// <summary>Переносит данные формы в сущность, полностью заменяя табличные разделы.</summary>
    public void Apply(SyllabusEditModel m, CourseSyllabus s)
    {
        s.DisciplineId = m.DisciplineId;
        s.AcademicYear = m.AcademicYear.Replace(" ", "");
        s.Course = m.Course;
        s.Semester = m.Semester;
        s.StudyForm = T(m.StudyForm);
        s.Language = T(m.Language);
        s.City = T(m.City);
        s.WeeksCount = m.WeeksCount;
        s.Lecturer = Clean(m.Lecturer);
        s.PracticeTeacher = Clean(m.PracticeTeacher);
        s.Goal = T(m.Goal);
        s.Tasks = T(m.Tasks);
        s.Description = T(m.Description);
        s.OutcomesKnow = T(m.OutcomesKnow);
        s.OutcomesCan = T(m.OutcomesCan);
        s.OutcomesSkills = T(m.OutcomesSkills);
        s.CriteriaNote = T(m.CriteriaNote);
        s.LatePolicy = T(m.LatePolicy);
        s.AcademicPolicy = T(m.AcademicPolicy);
        s.HeadOfDepartmentName = T(m.HeadOfDepartmentName);
        s.HeadOfDepartmentTitle = T(m.HeadOfDepartmentTitle);
        s.CompilerName = T(m.CompilerName);
        s.AgreedBy = T(m.AgreedBy);
        s.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        // Образовательные программы
        var wanted = m.ProgramIds.Distinct().ToHashSet();
        s.Programs.RemoveAll(p => !wanted.Contains(p.EducationalProgramId));
        foreach (var pid in wanted.Where(pid => s.Programs.All(p => p.EducationalProgramId != pid)))
            s.Programs.Add(new SyllabusProgram { EducationalProgramId = pid });

        ReplaceAll(s.Requisites, m.Requisites
            .Where(r => !string.IsNullOrWhiteSpace(r.Code) || !string.IsNullOrWhiteSpace(r.Name))
            .Select((r, i) => new SyllabusRequisite { Kind = r.Kind, Code = T(r.Code), Name = T(r.Name), Order = i }));

        ReplaceAll(s.CalendarRows, m.CalendarRows
            .Where(r => r.Kind == CalendarRowKind.Milestone ? !string.IsNullOrWhiteSpace(r.LectureTopic)
                : r.Week.HasValue || !string.IsNullOrWhiteSpace(r.LectureTopic) || !string.IsNullOrWhiteSpace(r.PracticeTopic))
            .Select((r, i) => new CalendarRow
            {
                Order = i, Kind = r.Kind, Week = r.Kind == CalendarRowKind.Week ? r.Week : null,
                LectureTopic = T(r.LectureTopic), PracticeTopic = T(r.PracticeTopic), SroTopic = T(r.SroTopic),
                LiteratureRefs = T(r.LiteratureRefs), Deadline = T(r.Deadline)
            }));

        ReplaceAll(s.Literature, m.Literature.Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .Select(l => new LiteratureItem { Number = l.Number, Kind = l.Kind, Text = T(l.Text) }));

        var cols = CompetencyFrame.Categories.Length;
        ReplaceAll(s.Competencies, m.Competencies.Take(CompetencyFrame.CellCount)
            .Select((text, i) => (text, i)).Where(x => !string.IsNullOrWhiteSpace(x.text))
            .Select(x => new CompetencyCell { DescriptorIndex = x.i / cols, CategoryIndex = x.i % cols, Text = T(x.text) }));

        ReplaceAll(s.Assessments, m.Assessments.Where(a => !string.IsNullOrWhiteSpace(a.Name)).Select((a, i) =>
        {
            var item = new AssessmentItem
            {
                Order = i, Name = T(a.Name), MaxPerWeek = T(a.MaxPerWeek), IsFinal = a.IsFinal,
                FinalPoints = a.IsFinal ? a.FinalPoints : null
            };
            var weeks = Enumerable.Range(0, m.WeeksCount)
                .Select(w => a.IsFinal ? null : w < a.Weeks.Count ? a.Weeks[w] : null);
            item.SetWeekPoints(weeks);
            return item;
        }));
    }

    public async Task<CourseSyllabus> CreateAsync(UserContext u, SyllabusEditModel m)
    {
        var s = new CourseSyllabus { AuthorId = u.UserId, CreatedAt = _time.GetUtcNow().UtcDateTime };
        Apply(m, s);
        _db.Syllabi.Add(s);
        await _db.SaveChangesAsync();
        _db.ApprovalLog.Add(new ApprovalLogEntry
        {
            SyllabusId = s.Id, FromStatus = SyllabusStatus.Draft, ToStatus = SyllabusStatus.Draft,
            Action = "Создан", UserId = u.UserId, UserName = u.FullName, At = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();
        return s;
    }

    /// <summary>Копия силлабуса на новый учебный год (материалы УМКД не копируются).</summary>
    public async Task<CourseSyllabus> CloneAsync(UserContext u, CourseSyllabus source, string academicYear)
    {
        var model = ToModel(source);
        model.Id = 0;
        model.AcademicYear = academicYear;
        var copy = new CourseSyllabus { AuthorId = u.UserId, ClonedFromId = source.Id, CreatedAt = _time.GetUtcNow().UtcDateTime };
        Apply(model, copy);
        _db.Syllabi.Add(copy);
        await _db.SaveChangesAsync();
        _db.ApprovalLog.Add(new ApprovalLogEntry
        {
            SyllabusId = copy.Id, FromStatus = SyllabusStatus.Draft, ToStatus = SyllabusStatus.Draft,
            Action = $"Создан копированием силлабуса {source.AcademicYear}", UserId = u.UserId,
            UserName = u.FullName, At = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();
        return copy;
    }

    public static string NextAcademicYear(string year)
    {
        var parts = year.Split('-');
        return parts.Length == 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b)
            ? $"{a + 1}-{b + 1}" : year;
    }

    private void ReplaceAll<T>(List<T> target, IEnumerable<T> items) where T : class
    {
        foreach (var old in target) _db.Remove(old);
        target.Clear();
        target.AddRange(items);
    }

    private static string T(string? v) => (v ?? "").Trim().Replace("\r\n", "\n");

    private static TeacherInfo Clean(TeacherInfo t) => new()
    {
        FullName = T(t.FullName), Regalia = T(t.Regalia), Office = T(t.Office),
        OfficeHours = T(t.OfficeHours), Email = T(t.Email)
    };

    private static TeacherInfo Copy(TeacherInfo t) => new()
    {
        FullName = t.FullName, Regalia = t.Regalia, Office = t.Office, OfficeHours = t.OfficeHours, Email = t.Email
    };
}
