using System.ComponentModel.DataAnnotations;
using SatbayevUniversity.Syllabus.Domain;

namespace SatbayevUniversity.Syllabus.Infrastructure.Dtos;

/// <summary>Модель формы редактирования силлабуса (защищает от over-posting служебных полей).</summary>
public class SyllabusEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Выберите дисциплину")]
    [Display(Name = "Дисциплина")]
    public int DisciplineId { get; set; }

    [Required(ErrorMessage = "Укажите учебный год"), RegularExpression(@"^\d{4}\s*-\s*\d{4}$", ErrorMessage = "Формат: 2026-2027")]
    [Display(Name = "Учебный год")]
    public string AcademicYear { get; set; } = "";

    [Range(1, 6), Display(Name = "Курс")] public int Course { get; set; } = 1;
    [Range(1, 12), Display(Name = "Семестр")] public int Semester { get; set; } = 1;
    [Display(Name = "Форма обучения")] public string StudyForm { get; set; } = "очное";
    [Display(Name = "Язык обучения")] public string Language { get; set; } = "русский";
    [Display(Name = "Город")] public string City { get; set; } = "Алматы";
    [Range(1, 20), Display(Name = "Количество недель")] public int WeeksCount { get; set; } = 15;

    [Display(Name = "Образовательные программы")]
    public List<int> ProgramIds { get; set; } = new();

    public TeacherInfo Lecturer { get; set; } = new();
    public TeacherInfo PracticeTeacher { get; set; } = new();

    public List<RequisiteRow> Requisites { get; set; } = new();

    [Display(Name = "Цель")] public string Goal { get; set; } = "";
    [Display(Name = "Задачи")] public string Tasks { get; set; } = "";
    [Display(Name = "Описание курса")] public string Description { get; set; } = "";
    [Display(Name = "Знать")] public string OutcomesKnow { get; set; } = "";
    [Display(Name = "Уметь")] public string OutcomesCan { get; set; } = "";
    [Display(Name = "Владеть навыками")] public string OutcomesSkills { get; set; } = "";

    public List<CalendarRowModel> CalendarRows { get; set; } = new();
    public List<LiteratureRow> Literature { get; set; } = new();
    /// <summary>25 ячеек рамки компетенций: индекс = дескриптор * 5 + категория.</summary>
    public List<string?> Competencies { get; set; } = new();
    public List<AssessmentRow> Assessments { get; set; } = new();

    [Display(Name = "Дополнение к критериям оценивания")] public string CriteriaNote { get; set; } = "";
    [Display(Name = "Политика поздней сдачи работ")] public string LatePolicy { get; set; } = "";
    [Display(Name = "Политика академического поведения и этики")] public string AcademicPolicy { get; set; } = "";

    [Display(Name = "Заведующий кафедрой (ФИО)")] public string HeadOfDepartmentName { get; set; } = "";
    [Display(Name = "Должность в подписи")] public string HeadOfDepartmentTitle { get; set; } = "Зав. кафедрой";
    [Display(Name = "Составитель (ФИО)")] public string CompilerName { get; set; } = "";
    [Display(Name = "Согласовано (по одному на строку)")] public string AgreedBy { get; set; } = "";

    public class RequisiteRow
    {
        public RequisiteKind Kind { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
    }

    public class CalendarRowModel
    {
        public CalendarRowKind Kind { get; set; }
        public int? Week { get; set; }
        public string? LectureTopic { get; set; }
        public string? PracticeTopic { get; set; }
        public string? SroTopic { get; set; }
        public string? LiteratureRefs { get; set; }
        public string? Deadline { get; set; }
    }

    public class LiteratureRow
    {
        public int Number { get; set; }
        public LiteratureKind Kind { get; set; }
        public string? Text { get; set; }
    }

    public class AssessmentRow
    {
        public string? Name { get; set; }
        public string? MaxPerWeek { get; set; }
        public bool IsFinal { get; set; }
        public decimal? FinalPoints { get; set; }
        public List<decimal?> Weeks { get; set; } = new();
    }
}
