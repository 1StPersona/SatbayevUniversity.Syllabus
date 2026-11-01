using System.Text.RegularExpressions;
using SatbayevUniversity.Syllabus.Domain;

namespace SatbayevUniversity.Syllabus.Infrastructure.Services;

public enum IssueSeverity { Error, Warning }

public sealed record ValidationIssue(IssueSeverity Severity, string Section, string Message);

/// <summary>
/// Проверка силлабуса на соответствие шаблону перед отправкой на согласование.
/// Ошибки блокируют отправку, предупреждения — нет.
/// </summary>
public static class SyllabusValidator
{
    public const decimal RatingMax = 60;
    public const decimal FinalMax = 40;
    public const decimal TotalMax = 100;

    private static readonly Regex RefRegex = new(@"\[(\d+)\]", RegexOptions.Compiled);

    public static List<ValidationIssue> Validate(CourseSyllabus s)
    {
        var list = new List<ValidationIssue>();
        void Err(string sec, string msg) => list.Add(new(IssueSeverity.Error, sec, msg));
        void Warn(string sec, string msg) => list.Add(new(IssueSeverity.Warning, sec, msg));

        if (string.IsNullOrWhiteSpace(s.AcademicYear)) Err("Титульный лист", "Не указан учебный год.");
        if (s.Programs.Count == 0) Err("Титульный лист", "Не выбрана ни одна образовательная программа.");

        if (string.IsNullOrWhiteSpace(s.Lecturer.FullName)) Err("1. Преподаватель", "Не указан лектор.");
        if (string.IsNullOrWhiteSpace(s.Lecturer.Email)) Warn("1. Преподаватель", "Не указан e-mail лектора.");
        if (string.IsNullOrWhiteSpace(s.Lecturer.OfficeHours)) Warn("1. Преподаватель", "Не указаны офис-часы лектора.");

        if (!s.Requisites.Any(r => r.Kind == RequisiteKind.Prerequisite))
            Warn("2. Пре- и постреквизиты", "Не указаны пререквизиты.");
        if (!s.Requisites.Any(r => r.Kind == RequisiteKind.Postrequisite))
            Warn("2. Пре- и постреквизиты", "Не указаны постреквизиты.");

        if (string.IsNullOrWhiteSpace(s.Goal)) Err("3. Цель и задачи", "Не сформулирована цель дисциплины.");
        if (Lines(s.Tasks).Count == 0) Err("3. Цель и задачи", "Не указаны задачи дисциплины.");
        if (string.IsNullOrWhiteSpace(s.Description)) Err("4. Описание курса", "Отсутствует описание курса.");

        if (Lines(s.OutcomesKnow).Count == 0) Err("5. Результаты обучения", "Не заполнен блок «Знать».");
        if (Lines(s.OutcomesCan).Count == 0) Err("5. Результаты обучения", "Не заполнен блок «Уметь».");
        if (Lines(s.OutcomesSkills).Count == 0) Err("5. Результаты обучения", "Не заполнен блок «Владеть навыками».");

        // 6. Календарно-тематический план
        var weeks = s.CalendarRows.Where(r => r.Kind == CalendarRowKind.Week).ToList();
        if (weeks.Count == 0) Err("6. Календарно-тематический план", "План не содержит ни одной учебной недели.");
        foreach (var w in weeks.Where(w => w.Week is null || w.Week < 1 || w.Week > s.WeeksCount))
            Err("6. Календарно-тематический план", $"Неверный номер недели «{w.Week}» (допустимо 1–{s.WeeksCount}).");
        foreach (var w in weeks.Where(w => string.IsNullOrWhiteSpace(w.LectureTopic) && string.IsNullOrWhiteSpace(w.PracticeTopic)))
            Warn("6. Календарно-тематический план", $"Неделя {w.Week}: не указаны темы занятий.");
        var missingWeeks = Enumerable.Range(1, s.WeeksCount).Except(weeks.Where(w => w.Week.HasValue).Select(w => w.Week!.Value)).ToList();
        if (weeks.Count > 0 && missingWeeks.Count > 0)
            Warn("6. Календарно-тематический план", "Нет записей для недель: " + string.Join(", ", missingWeeks) + ".");

        // 7. Литература
        if (!s.Literature.Any(l => l.Kind == LiteratureKind.Main))
            Err("7. Литература", "Не указана основная литература.");
        var dupNumbers = s.Literature.GroupBy(l => l.Number).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupNumbers.Count > 0)
            Err("7. Литература", "Повторяющиеся номера источников: " + string.Join(", ", dupNumbers.Select(n => $"[{n}]")) + ".");
        var known = s.Literature.Select(l => l.Number).ToHashSet();
        var badRefs = s.CalendarRows
            .SelectMany(r => RefRegex.Matches(r.LiteratureRefs + " " + r.PracticeTopic).Select(m => int.Parse(m.Groups[1].Value)))
            .Where(n => !known.Contains(n)).Distinct().OrderBy(n => n).ToList();
        if (badRefs.Count > 0)
            Warn("6. Календарно-тематический план", "Ссылки на отсутствующие источники: " + string.Join(", ", badRefs.Select(n => $"[{n}]")) + ".");

        // 8. Рамка компетенций
        var filled = s.Competencies.Count(c => !string.IsNullOrWhiteSpace(c.Text));
        if (filled == 0) Err("8. Рамка компетенций", "Рамка компетенций не заполнена.");
        else if (filled < CompetencyFrame.CellCount)
            Warn("8. Рамка компетенций", $"Заполнено {filled} из {CompetencyFrame.CellCount} ячеек.");

        // 9. График сдачи работ
        var (rating, final) = Totals(s);
        if (s.Assessments.Count == 0) Err("9. График сдачи работ", "Не заполнен график сдачи работ.");
        else
        {
            if (rating != RatingMax) Err("9. График сдачи работ", $"Сумма баллов рейтинга допуска = {rating}, должно быть {RatingMax}.");
            if (final != FinalMax) Err("9. График сдачи работ", $"Итоговый контроль = {final} баллов, должно быть {FinalMax}.");
            foreach (var a in s.Assessments.Where(a => !a.IsFinal))
            {
                var pts = a.GetWeekPoints(s.WeeksCount);
                var allowed = ParseMax(a.MaxPerWeek);
                if (allowed.Count > 0)
                    foreach (var (p, i) in pts.Select((p, i) => (p, i)).Where(x => x.p.HasValue && !allowed.Contains(x.p!.Value)))
                        Warn("9. График сдачи работ", $"«{a.Name}», неделя {i + 1}: {p} балл(ов) не совпадает с макс. баллом «{a.MaxPerWeek}».");
            }
            var rk = s.Assessments.Where(a => a.Name.Contains("промежуточн", StringComparison.OrdinalIgnoreCase)).ToList();
            if (rk.Count < 2) Warn("9. График сдачи работ", "Ожидаются две промежуточные аттестации (РК1 и РК2).");
        }

        if (string.IsNullOrWhiteSpace(s.CompilerName)) Err("Подписи", "Не указан составитель.");
        if (string.IsNullOrWhiteSpace(s.HeadOfDepartmentName)) Warn("Подписи", "Не указан заведующий кафедрой.");

        return list;
    }

    public static (decimal Rating, decimal Final) Totals(CourseSyllabus s) =>
        (s.Assessments.Where(a => !a.IsFinal).Sum(a => a.Total(s.WeeksCount)),
         s.Assessments.Where(a => a.IsFinal).Sum(a => a.Total(s.WeeksCount)));

    public static decimal WeekTotal(CourseSyllabus s, int weekIndex) =>
        s.Assessments.Where(a => !a.IsFinal).Sum(a => a.GetWeekPoints(s.WeeksCount)[weekIndex] ?? 0);

    public static List<string> Lines(string? text) =>
        (text ?? "").Split('\n').Select(l => l.Trim().TrimStart('–', '-', '—', '•').Trim())
            .Where(l => l.Length > 0).ToList();

    private static HashSet<decimal> ParseMax(string? max) =>
        (max ?? "").Split('/', ';', ',').Select(x => x.Trim())
            .Select(x => decimal.TryParse(x, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToHashSet();
}
