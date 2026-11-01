using Microsoft.AspNetCore.Identity;

namespace SatbayevUniversity.Syllabus.Domain;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    /// <summary>Учёная степень и должность, например «PhD, ассоциированный профессор».</summary>
    public string? Regalia { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? InstituteId { get; set; }
    public Institute? Institute { get; set; }
    /// <summary>Для обучающихся — образовательная программа.</summary>
    public int? EducationalProgramId { get; set; }
    public EducationalProgram? EducationalProgram { get; set; }
}

public class Institute
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string DirectorName { get; set; } = "";
    public List<Department> Departments { get; set; } = new();
}

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string HeadName { get; set; } = "";
    /// <summary>Как подписывается заведующий: «Зав. кафедрой» или «И.о зав. кафедрой».</summary>
    public string HeadTitle { get; set; } = "Зав. кафедрой";
    public int InstituteId { get; set; }
    public Institute? Institute { get; set; }
}

public class EducationalProgram
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public StudyLevel Level { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string Display => $"{Code} «{Name}»";
}

public class Discipline
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DisciplineCycle Cycle { get; set; }
    public int Credits { get; set; }
    /// <summary>Структура кредита (лек/лаб/пр/СРОП), например «2/0/1/1».</summary>
    public string CreditStructure { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string Display => $"{Code} {Name}";
}

/// <summary>Электронный силлабус дисциплины на учебный год (форма Ф КазНИТУ 703-08).</summary>
public class CourseSyllabus
{
    public int Id { get; set; }
    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    // Титульный лист
    public string AcademicYear { get; set; } = "";
    public int Course { get; set; } = 1;
    public int Semester { get; set; } = 1;
    public string StudyForm { get; set; } = "очное";
    public string Language { get; set; } = "русский";
    public string City { get; set; } = "Алматы";
    public List<SyllabusProgram> Programs { get; set; } = new();

    // 1. Информация о преподавателе
    public TeacherInfo Lecturer { get; set; } = new();
    public TeacherInfo PracticeTeacher { get; set; } = new();

    // 2. Пре- и постреквизиты
    public List<SyllabusRequisite> Requisites { get; set; } = new();

    // 3. Цель и задачи
    public string Goal { get; set; } = "";
    /// <summary>Задачи — по одной на строку.</summary>
    public string Tasks { get; set; } = "";

    // 4. Описание курса
    public string Description { get; set; } = "";

    // 5. Результаты обучения — по одному на строку
    public string OutcomesKnow { get; set; } = "";
    public string OutcomesCan { get; set; } = "";
    public string OutcomesSkills { get; set; } = "";

    // 6–9. Табличные разделы
    public List<CalendarRow> CalendarRows { get; set; } = new();
    public List<LiteratureItem> Literature { get; set; } = new();
    public List<CompetencyCell> Competencies { get; set; } = new();
    public List<AssessmentItem> Assessments { get; set; } = new();
    public int WeeksCount { get; set; } = 15;

    // 11–13. Критерии и политики
    public string CriteriaNote { get; set; } = "";
    public string LatePolicy { get; set; } = "";
    public string AcademicPolicy { get; set; } = "";

    // Подписи и согласование
    public string? ProtocolNumber { get; set; }
    public DateOnly? ProtocolDate { get; set; }
    public string HeadOfDepartmentName { get; set; } = "";
    public string HeadOfDepartmentTitle { get; set; } = "Зав. кафедрой";
    public string CompilerName { get; set; } = "";
    /// <summary>«Согласовано» — ФИО по одному на строку.</summary>
    public string AgreedBy { get; set; } = "";
    public string? ApprovedByName { get; set; }
    public string? ApprovedByTitle { get; set; }
    public DateOnly? ApprovedDate { get; set; }

    // Служебные поля
    public SyllabusStatus Status { get; set; } = SyllabusStatus.Draft;
    public string AuthorId { get; set; } = "";
    public ApplicationUser? Author { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? ClonedFromId { get; set; }

    public List<ApprovalLogEntry> History { get; set; } = new();
    public List<UmkdMaterial> Materials { get; set; } = new();

    public bool IsEditable => Status is SyllabusStatus.Draft or SyllabusStatus.Returned;
}

/// <summary>Owned-тип: данные преподавателя для раздела 1.</summary>
public class TeacherInfo
{
    public string FullName { get; set; } = "";
    public string Regalia { get; set; } = "";
    public string Office { get; set; } = "";
    public string OfficeHours { get; set; } = "";
    public string Email { get; set; } = "";
}

public class SyllabusProgram
{
    public int SyllabusId { get; set; }
    public CourseSyllabus? Syllabus { get; set; }
    public int EducationalProgramId { get; set; }
    public EducationalProgram? EducationalProgram { get; set; }
}

public class SyllabusRequisite
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public RequisiteKind Kind { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }
}

public class CalendarRow
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public int Order { get; set; }
    public CalendarRowKind Kind { get; set; }
    public int? Week { get; set; }
    public string LectureTopic { get; set; } = "";
    public string PracticeTopic { get; set; } = "";
    public string SroTopic { get; set; } = "";
    public string LiteratureRefs { get; set; } = "";
    public string Deadline { get; set; } = "";
}

public class LiteratureItem
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public int Number { get; set; }
    public LiteratureKind Kind { get; set; }
    public string Text { get; set; } = "";
}

public class CompetencyCell
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public int DescriptorIndex { get; set; }
    public int CategoryIndex { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>Строка раздела 9 «График сдачи требуемых работ».</summary>
public class AssessmentItem
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Макс. балл за неделю как в шаблоне («5», «2/3»).</summary>
    public string MaxPerWeek { get; set; } = "";
    /// <summary>Баллы по неделям, через «;» (пустое значение — нет контроля).</summary>
    public string WeekPoints { get; set; } = "";
    /// <summary>Итоговый контроль (экзамен) — учитывается отдельно от рейтинга допуска.</summary>
    public bool IsFinal { get; set; }
    public decimal? FinalPoints { get; set; }

    public decimal?[] GetWeekPoints(int weeks)
    {
        var parts = (WeekPoints ?? "").Split(';');
        var result = new decimal?[weeks];
        for (var i = 0; i < weeks && i < parts.Length; i++)
            if (decimal.TryParse(parts[i], System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                result[i] = v;
        return result;
    }

    public void SetWeekPoints(IEnumerable<decimal?> points) =>
        WeekPoints = string.Join(";", points.Select(p =>
            p?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""));

    public decimal Total(int weeks) =>
        IsFinal ? FinalPoints ?? 0 : GetWeekPoints(weeks).Sum(p => p ?? 0);
}

public class ApprovalLogEntry
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public SyllabusStatus FromStatus { get; set; }
    public SyllabusStatus ToStatus { get; set; }
    public string Action { get; set; } = "";
    public string? Comment { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>Вид компонента УМКД (справочник, настраивается администратором).</summary>
public class UmkdComponentType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    /// <summary>Компонент закрывается самим утверждённым силлабусом (файлы не нужны).</summary>
    public bool IsSyllabus { get; set; }
    public bool VisibleToStudentsByDefault { get; set; } = true;
}

/// <summary>Материал УМКД: загруженный файл или ссылка на электронный ресурс.</summary>
public class UmkdMaterial
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public CourseSyllabus? Syllabus { get; set; }
    public int ComponentTypeId { get; set; }
    public UmkdComponentType? ComponentType { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? OriginalFileName { get; set; }
    public string? StoredFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? ExternalUrl { get; set; }
    public bool VisibleToStudents { get; set; } = true;
    public MaterialReviewStatus ReviewStatus { get; set; } = MaterialReviewStatus.Pending;
    public string? ReviewComment { get; set; }
    public string UploadedById { get; set; } = "";
    public string UploadedByName { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Глобальные настройки шаблона (одна запись).</summary>
public class TemplateSettings
{
    public int Id { get; set; }
    public string UniversityName { get; set; } = "";
    public string UniversityShortName { get; set; } = "";
    public string FormCode { get; set; } = "";
    public string DefaultCriteriaNote { get; set; } = "";
    public string DefaultLatePolicy { get; set; } = "";
    public string DefaultAcademicPolicy { get; set; } = "";
    public string AcquaintanceNote { get; set; } = "";
}

/// <summary>Шкала оценивания (раздел 10) — общая для всех силлабусов.</summary>
public class GradeScaleRow
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Letter { get; set; } = "";
    public decimal Gpa { get; set; }
    public int MinPercent { get; set; }
    public int MaxPercent { get; set; }
    public string Criterion { get; set; } = "";
    public string Traditional { get; set; } = "";
}
