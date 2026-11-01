using System.ComponentModel.DataAnnotations;

namespace SatbayevUniversity.Syllabus.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Director = "Director";
    public const string HeadOfDepartment = "HeadOfDepartment";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static readonly string[] All = { Admin, Director, HeadOfDepartment, Teacher, Student };

    public static string Title(string role) => role switch
    {
        Admin => "Администратор",
        Director => "Директор института",
        HeadOfDepartment => "Заведующий кафедрой",
        Teacher => "Преподаватель",
        Student => "Обучающийся",
        _ => role
    };
}

/// <summary>Жизненный цикл силлабуса: разработка → кафедра → утверждение директором.</summary>
public enum SyllabusStatus
{
    [Display(Name = "Черновик")] Draft = 0,
    [Display(Name = "На рассмотрении кафедры")] OnDepartmentReview = 1,
    [Display(Name = "Возвращён на доработку")] Returned = 2,
    [Display(Name = "Одобрен кафедрой")] DepartmentApproved = 3,
    [Display(Name = "Утверждён")] Approved = 4,
    [Display(Name = "В архиве")] Archived = 5
}

public enum RequisiteKind
{
    [Display(Name = "Пререквизит")] Prerequisite = 0,
    [Display(Name = "Постреквизит")] Postrequisite = 1
}

public enum CalendarRowKind
{
    [Display(Name = "Учебная неделя")] Week = 0,
    [Display(Name = "Контрольная точка")] Milestone = 1
}

public enum LiteratureKind
{
    [Display(Name = "Основная")] Main = 0,
    [Display(Name = "Дополнительная")] Additional = 1
}

public enum DisciplineCycle
{
    [Display(Name = "ООД")] OOD = 0,
    [Display(Name = "БД")] BD = 1,
    [Display(Name = "ПД")] PD = 2
}

public enum StudyLevel
{
    [Display(Name = "Бакалавриат")] Bachelor = 0,
    [Display(Name = "Магистратура")] Master = 1,
    [Display(Name = "Докторантура")] Doctoral = 2
}

public enum MaterialReviewStatus
{
    [Display(Name = "На проверке")] Pending = 0,
    [Display(Name = "Принят")] Accepted = 1,
    [Display(Name = "На доработку")] Rejected = 2
}

/// <summary>Строки рамки компетенций (дескрипторы Дублинских критериев).</summary>
public static class CompetencyFrame
{
    public static readonly string[] Descriptors =
    {
        "Знание и понимание",
        "Применение знаний и пониманий",
        "Выражение суждений и анализа действий",
        "Коммуникативные и креативные способности",
        "Самообучаемость и цифровые навыки"
    };

    public static readonly string[] Categories =
    {
        "Естественно-научные и теоретико-мировоззренческие",
        "Социально-личностные и гражданские",
        "Общеинженерные профессиональные",
        "Межкультурно-коммуникативные",
        "Специально-профессиональные"
    };

    public static int CellCount => Descriptors.Length * Categories.Length;
}
