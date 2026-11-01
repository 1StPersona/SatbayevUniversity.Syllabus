using SatbayevUniversity.Syllabus.Infrastructure.Services;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SatbayevUniversity.Syllabus.Domain;

namespace SatbayevUniversity.Syllabus.Infrastructure.Export;

/// <summary>
/// Формирует силлабус в формате DOCX по форме Ф КазНИТУ 703-08:
/// титульный лист с грифом «УТВЕРЖДАЮ», разделы 1–13, лист подписей.
/// </summary>
public class SyllabusDocxExporter
{
    private const string Font = "Times New Roman";
    private const int BodySize = 28;   // 14 pt (в полупунктах)
    private const int TableSize = 22;  // 11 pt
    private const int ContentWidth = 9638; // A4 минус поля 2/1,5 см, в twips

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public byte[] Export(CourseSyllabus s, TemplateSettings settings, IReadOnlyList<GradeScaleRow> scale)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            AddStyles(main);
            var body = main.Document.Body!;

            var headerId = AddHeader(main, settings);
            var footerId = AddFooter(main, settings);

            TitlePage(body, s, settings);
            Sections(body, s, settings, scale);
            Signatures(body, s);

            body.Append(new SectionProperties(
                new HeaderReference { Type = HeaderFooterValues.Default, Id = headerId },
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1134, Bottom = 1134, Left = 1418, Right = 850, Header = 567, Footer = 567, Gutter = 0 },
                new TitlePage()));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ Титульный лист

    private void TitlePage(Body body, CourseSyllabus s, TemplateSettings settings)
    {
        var d = s.Discipline!;
        var dept = d.Department!;
        var inst = dept.Institute!;

        body.Append(Para(settings.UniversityShortName, bold: true, align: JustificationValues.Center, size: 32, after: 360));
        body.Append(Para(inst.Name, bold: true, align: JustificationValues.Center, after: 0));
        body.Append(Para($"Кафедра «{dept.Name}»", bold: true, align: JustificationValues.Center, after: 600));

        var approvedDate = s.ApprovedDate is { } ad
            ? $"«{ad.Day:00}» {MonthGenitive(ad)} {ad.Year} г."
            : "«____» ______________ 20___ г.";
        foreach (var (line, bold) in new[]
                 {
                     ("УТВЕРЖДАЮ", true),
                     ($"Директор института {inst.ShortName}", false),
                     ($"_______________ {s.ApprovedByName ?? WorkflowService.ShortName(inst.DirectorName)}", false),
                     (approvedDate, false)
                 })
            body.Append(Para(line, bold: bold, indentLeft: 5000, after: 0));

        body.Append(Para("", after: 1400));
        body.Append(Para("СИЛЛАБУС", bold: true, align: JustificationValues.Center, size: 32, after: 240));
        body.Append(Para(d.Display, bold: true, align: JustificationValues.Center, after: 240));
        foreach (var p in s.Programs.Select(p => p.EducationalProgram!).OrderBy(p => p.Code))
            body.Append(Para(p.Display, bold: true, align: JustificationValues.Center, after: 0));
        body.Append(Para("", after: 240));
        body.Append(Para($"Цикл дисциплины {Display(d.Cycle)}, {d.Credits} ({d.CreditStructure}) {CreditWord(d.Credits)}",
            align: JustificationValues.Center, after: 240));
        body.Append(Para($"Курс {s.Course}, семестр {s.Semester}", align: JustificationValues.Center, after: 240));
        body.Append(Para($"{s.AcademicYear.Replace("-", "- ")} учебный год", align: JustificationValues.Center, after: 3200));
        body.Append(Para($"{s.City} {s.AcademicYear.Split('-')[0]}", bold: true, align: JustificationValues.Center, after: 0));
        body.Append(PageBreak());
    }

    // ------------------------------------------------------------------ Разделы 1–13

    private void Sections(Body body, CourseSyllabus s, TemplateSettings settings, IReadOnlyList<GradeScaleRow> scale)
    {
        // 1. Информация о преподавателе
        body.Append(Heading("1. Информация о преподавателе"));
        body.Append(SubHeading("1.1 Лектор:"));
        TeacherBlock(body, s.Lecturer);
        body.Append(SubHeading("1.2 Преподаватель, ведущий практические/лабораторные занятия"));
        TeacherBlock(body, s.PracticeTeacher);
        body.Append(Mixed(("*Форма обучения – ", true, false), (s.StudyForm, true, true)));

        // 2. Пре- и постреквизиты
        body.Append(Heading("2. Пререквизиты и постреквизиты"));
        string Req(RequisiteKind k) => string.Join("\n", s.Requisites.Where(r => r.Kind == k)
            .Select(r => string.IsNullOrEmpty(r.Code) ? r.Name : $"{r.Code} - {r.Name}"));
        body.Append(Table(new[] { 2700, ContentWidth - 2700 }, header: null, rows: new[]
        {
            new[] { "Пререквизиты", Req(RequisiteKind.Prerequisite) },
            new[] { "Постреквизиты", Req(RequisiteKind.Postrequisite) }
        }, size: BodySize));

        // 3. Цель и задачи
        body.Append(Heading("3. Цель и задачи дисциплины"));
        body.Append(Mixed(("Цель: ", true, false), (s.Goal, false, false)));
        body.Append(Para("Задачи:", bold: true, indentFirst: 709, after: 0));
        Dashes(body, s.Tasks);

        // 4. Описание курса
        body.Append(Heading("4. Описание курса:"));
        foreach (var para in Paragraphs(s.Description)) body.Append(Para(para, indentFirst: 709, after: 0, justify: true));

        // 5. Результаты обучения
        body.Append(Heading("5. Результаты обучения"));
        body.Append(Para("После завершения курса обучающийся должен:", indentFirst: 709, after: 0));
        body.Append(Para("Знать:", bold: true, indentFirst: 709, after: 0));
        Dashes(body, s.OutcomesKnow);
        body.Append(Para("Уметь:", bold: true, indentFirst: 709, after: 0));
        Dashes(body, s.OutcomesCan);
        body.Append(Para("Владеть навыками:", bold: true, indentFirst: 709, after: 0));
        Dashes(body, s.OutcomesSkills);

        // 6. Календарно-тематический план
        body.Append(Heading("6. Календарно-тематический план"));
        var calWidths = new[] { 600, 2100, 2100, 2338, 1250, 1250 };
        var cal = Table(calWidths, new[] { "Неделя", "Тема лекции", "Тема практической работы", "СРО/СРОП", "Ссылка на литературу", "Срок сдачи" }, Array.Empty<string[]>());
        foreach (var r in s.CalendarRows.OrderBy(r => r.Order))
        {
            if (r.Kind == CalendarRowKind.Milestone)
                cal.Append(Row(calWidths, new[] { ("", 1), (r.LectureTopic, 3), ("", 1), (r.Deadline, 1) }, boldAll: true));
            else
                cal.Append(Row(calWidths, new[] { (r.Week?.ToString() ?? "", 1), (r.LectureTopic, 1), (r.PracticeTopic, 1), (r.SroTopic, 1), (r.LiteratureRefs, 1), (r.Deadline, 1) }));
        }
        body.Append(cal);

        // 7. Литература
        body.Append(Heading("7. Литература"));
        var main = s.Literature.Where(l => l.Kind == LiteratureKind.Main).OrderBy(l => l.Number).ToList();
        var add = s.Literature.Where(l => l.Kind == LiteratureKind.Additional).OrderBy(l => l.Number).ToList();
        var litRows = Enumerable.Range(0, Math.Max(main.Count, add.Count)).Select(i => new[]
        {
            i < main.Count ? $"[{main[i].Number}] {main[i].Text}" : "",
            i < add.Count ? $"[{add[i].Number}] {add[i].Text}" : ""
        }).ToArray();
        body.Append(Table(new[] { ContentWidth / 2, ContentWidth / 2 }, new[] { "Основная литература", "Дополнительная литература" }, litRows));

        // 8. Рамка компетенций
        body.Append(Heading("8. Рамка компетенций"));
        var compWidths = new[] { 1638, 1600, 1600, 1600, 1600, 1600 };
        var comp = NewTable(compWidths);
        comp.Append(Row(compWidths, new[] { ("Дескрипторы обучения", 1), ("Компетенции", 5) }, boldAll: true, header: true));
        comp.Append(Row(compWidths, new[] { ("", 1) }.Concat(CompetencyFrame.Categories.Select(c => (c, 1))).ToArray(), header: true, center: true));
        for (var di = 0; di < CompetencyFrame.Descriptors.Length; di++)
        {
            var cells = new List<(string, int)> { (CompetencyFrame.Descriptors[di], 1) };
            for (var ci = 0; ci < CompetencyFrame.Categories.Length; ci++)
                cells.Add((s.Competencies.FirstOrDefault(c => c.DescriptorIndex == di && c.CategoryIndex == ci)?.Text ?? "", 1));
            comp.Append(Row(compWidths, cells.ToArray()));
        }
        body.Append(comp);

        // 9. График сдачи требуемых работ
        body.Append(Heading("9. График сдачи требуемых работ"));
        body.Append(AssessmentTable(s));

        // 10. Оценивание итогового контроля
        body.Append(Heading("10. Оценивания итогового контроля"));
        var scaleWidths = new[] { 1200, 1300, 1400, 3538, 2200 };
        body.Append(Table(scaleWidths,
            new[] { "Оценка по буквенной системе", "Цифровой эквивалент баллов", "Процентное содержание", "Критерий", "Оценка по традиционной системе" },
            scale.OrderBy(r => r.Order).Select(r => new[]
            {
                r.Letter, r.Gpa.ToString("0.##", Ru), $"{r.MinPercent}-{r.MaxPercent}", r.Criterion, r.Traditional
            }).ToArray()));

        // 11. Критерии оценивания
        body.Append(Heading("11. Критерии оценивания"));
        foreach (var para in Paragraphs(CriteriaIntro)) body.Append(Para(para, indentFirst: 709, after: 0, justify: true));
        body.Append(Table(new[] { 1838, 1950, 1950, 1950, 1950 },
            new[] { "Критерии", "Отлично (0.9-1.0)", "Хорошо (0.7-0.9)", "Удовлетворительно (0.4-0.7)", "Неудовлетворительно (0-0.4)" },
            CriteriaRows));
        body.Append(Para("Общая оценка будет рассчитана по формуле:", indentFirst: 709, after: 0));
        body.Append(Para("Оценка = (А + Т + З)×О", bold: true, italic: true, indentFirst: 709));
        foreach (var para in Paragraphs(s.CriteriaNote)) body.Append(Para(para, indentFirst: 709, after: 0, justify: true));

        // 12–13. Политики
        body.Append(Heading("12. Политика поздней сдачи работ"));
        foreach (var para in Paragraphs(s.LatePolicy)) body.Append(Para(para, indentFirst: 709, after: 0, justify: true));
        body.Append(Heading("13. Политика академического поведения и этики"));
        foreach (var para in Paragraphs(s.AcademicPolicy)) body.Append(Para(para, indentFirst: 709, after: 0, justify: true));
        if (!string.IsNullOrWhiteSpace(settings.AcquaintanceNote))
            body.Append(Para(settings.AcquaintanceNote, indentFirst: 709, justify: true));
    }

    private Table AssessmentTable(CourseSyllabus s)
    {
        var weeks = s.WeeksCount;
        var widths = new List<int> { 450, 2300, 800 };
        var weekW = (ContentWidth - 450 - 2300 - 800 - 1000) / weeks;
        widths.AddRange(Enumerable.Repeat(weekW, weeks));
        widths.Add(1000);
        var w = widths.ToArray();
        var t = NewTable(w);

        var h1 = new List<(string, int)> { ("№ п/п", 1), ("Виды контроля", 1), ("Макс балл недели", 1), ("Недели", weeks), ("Итого макс баллов", 1) };
        t.Append(Row(w, h1.ToArray(), header: true, boldAll: true, size: 18));
        var h2 = new List<(string, int)> { ("", 1), ("", 1), ("", 1) };
        h2.AddRange(Enumerable.Range(1, weeks).Select(i => (i.ToString(), 1)));
        h2.Add(("", 1));
        t.Append(Row(w, h2.ToArray(), header: true, boldAll: true, size: 18));

        var (rating, final) = SyllabusValidator.Totals(s);
        var rowNo = 0;
        foreach (var a in s.Assessments.OrderBy(a => a.Order))
        {
            rowNo++;
            var cells = new List<(string, int)> { (rowNo.ToString(), 1), (a.Name + (a.IsFinal ? "*" : ""), 1), (a.MaxPerWeek, 1) };
            if (a.IsFinal)
            {
                cells.AddRange(Enumerable.Repeat(("", 1), weeks));
                cells.Add((Num(a.FinalPoints ?? 0), 1));
            }
            else
            {
                cells.AddRange(a.GetWeekPoints(weeks).Select(p => (p.HasValue ? Num(p.Value) : "", 1)));
                cells.Add((rowNo == 1 ? Num(rating) : "", 1));
            }
            t.Append(Row(w, cells.ToArray(), size: 18, center: true, leftCols: 2));
        }
        var total = new List<(string, int)> { ("", 1), ("Итого", 1), ("", 1) };
        total.AddRange(Enumerable.Repeat(("", 1), weeks));
        total.Add((Num(rating + final), 1));
        t.Append(Row(w, total.ToArray(), boldAll: true, size: 18, center: true));
        return t;
    }

    private void Signatures(Body body, CourseSyllabus s)
    {
        var protocol = s.ProtocolDate is { } pd
            ? $"протокол № {s.ProtocolNumber} от «{pd.Day:00}» {MonthGenitive(pd)} {pd.Year}г."
            : "протокол № ____ от «____» ____________ 20___г.";
        body.Append(Para($"Рассмотрено и одобрено на заседании кафедры «{s.Discipline!.Department!.Name}» {protocol}",
            indentFirst: 709, justify: true, before: 240, after: 480));

        var widths = new[] { 3800, 2200, 3638 };
        var t = NewTable(widths, borders: false);
        t.Append(Row(widths, new[] { (s.HeadOfDepartmentTitle, 1), ("", 1), (s.HeadOfDepartmentName, 1) }, boldAll: true, size: BodySize, noBorders: true));
        t.Append(Row(widths, new[] { ("Составитель", 1), ("", 1), (s.CompilerName, 1) }, boldAll: true, size: BodySize, noBorders: true));
        body.Append(t);

        var agreed = SyllabusValidator.Lines(s.AgreedBy);
        if (agreed.Count > 0)
        {
            body.Append(Para("Согласовано:", bold: true, before: 480));
            foreach (var (name, i) in agreed.Select((n, i) => (n, i)))
                body.Append(Mixed(($"{i + 1}. _______________   ", false, false), (name, true, false)));
        }
    }

    // ------------------------------------------------------------------ Стандартные тексты раздела 11

    public const string CriteriaIntro =
        "Каждая работа кроме тестов оценивается по 4 критериям:\n" +
        "– аккуратность и точность (А) – 30% (как точно и аккуратно рассчитана работа);\n" +
        "– творчество и креативность (Т) – 30% (как и каким образом представлена работа);\n" +
        "– полнота и зрелость (З) – 40% (как глубоко, логично и структурно решена работа);\n" +
        "– оригинальность (О) – используется специальный коэффициент 1.0; 0.5 или 0.";

    public static readonly string[][] CriteriaRows =
    {
        new[] { "Аккуратность и точность",
            "Абсолютная точность расчетов с изложением хода решения, полная широта аналитических и графических способов решения уравнений",
            "Имеются незначительные неточности (минус 0.1 – за каждый некорректный ответ; минус 0.5 – за неясность изложения аналитического решения)",
            "Имеются значительные неточности (минус 0.1 – за каждый некорректный ответ; минус 0.5 – за неясность изложения аналитического решения)",
            "Работа рассчитана неверно (минус 0.1 – за каждый некорректный ответ; минус 0.5 – за неясность изложения аналитического решения)" },
        new[] { "Творчество и креативность",
            "Авторский творческий и нестандартный подход с ясным сценарным планом изложения",
            "Стандартный подход в рамках методических указаний с ясным сценарным планом изложения",
            "Стандартный подход в рамках методических указаний неясным или сумбурным сценарным планом изложения",
            "Отклонение от минимального стандарта изложения" },
        new[] { "Полнота и зрелость",
            "Поставленные задачи не только выполнены полностью, но и представлены иные подходы на решение",
            "Задачи выполнены полностью с незначительными погрешностями, например не проставлена размерность (минус 0.1 – за каждый пропущенный случай)",
            "Задачи выполнены неполностью или со значительными погрешностями, например не проставлена размерность (минус 0.5 – за каждый пропущенный случай)",
            "Задачи в основной массе не выполнены" },
        new[] { "Оригинальность",
            "1.0 - работа полностью оригинальна, аутентична и выполнена путем собственных усилий заявителя",
            "0.5 - работа списана у коллеги (коэффициент выставляется обоим)",
            "0.5 - работа списана у коллеги (коэффициент выставляется обоим)",
            "0 – работа и ее значительные фрагменты заимствованы из других источников без указания оригинальных ссылок" },
    };

    // ------------------------------------------------------------------ Хелперы OpenXML

    private static void AddStyles(MainDocumentPart main)
    {
        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                    new FontSize { Val = BodySize.ToString() },
                    new FontSizeComplexScript { Val = BodySize.ToString() },
                    new Languages { Val = "ru-RU" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }))));
        part.Styles.Save();
    }

    private static string AddHeader(MainDocumentPart main, TemplateSettings settings)
    {
        var part = main.AddNewPart<HeaderPart>();
        part.Header = new Header(Para(settings.UniversityName.ToUpper(Ru), align: JustificationValues.Center, size: 20, after: 120));
        part.Header.Save();
        return main.GetIdOfPart(part);
    }

    private static string AddFooter(MainDocumentPart main, TemplateSettings settings)
    {
        var part = main.AddNewPart<FooterPart>();
        var p = new Paragraph(
            new ParagraphProperties(new Tabs(new TabStop { Val = TabStopValues.Right, Position = ContentWidth })),
            TextRun($"{settings.FormCode}. Силлабус", size: 20),
            new Run(new TabChar()),
            new Run(new RunProperties(new FontSize { Val = "20" }), new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new RunProperties(new FontSize { Val = "20" }), new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new RunProperties(new FontSize { Val = "20" }), new FieldChar { FieldCharType = FieldCharValues.End }));
        part.Footer = new Footer(p);
        part.Footer.Save();
        return main.GetIdOfPart(part);
    }

    private static Paragraph Heading(string text) =>
        Para(text, bold: true, indentFirst: 709, before: 240, after: 120, keepNext: true);

    private static Paragraph SubHeading(string text) =>
        Para(text, bold: true, indentFirst: 709, after: 0, keepNext: true);

    private static void TeacherBlock(Body body, TeacherInfo t)
    {
        var line = string.IsNullOrWhiteSpace(t.Regalia) ? t.FullName : $"{t.FullName}, {t.Regalia}";
        body.Append(Para(line, indentFirst: 709, justify: true, after: 120));
        var widths = new[] { 4600, ContentWidth - 4600 };
        var table = NewTable(widths, borders: false);
        var right = string.Join("\n", new[]
        {
            string.IsNullOrWhiteSpace(t.OfficeHours) ? null : $"Офис-часы: {t.OfficeHours}",
            string.IsNullOrWhiteSpace(t.Email) ? null : $"e-mail: {t.Email}"
        }.Where(x => x != null));
        table.Append(Row(widths, new[] { (string.IsNullOrWhiteSpace(t.Office) ? "" : $"офис: {t.Office}", 1), (right, 1) }, size: BodySize, noBorders: true));
        body.Append(table);
        body.Append(Para("", after: 120));
    }

    private static void Dashes(Body body, string text)
    {
        foreach (var line in SyllabusValidator.Lines(text))
            body.Append(Para("– " + line, justify: true, after: 0));
    }

    private static IEnumerable<string> Paragraphs(string? text) =>
        (text ?? "").Replace("\r\n", "\n").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0);

    private static Paragraph Para(string text, bool bold = false, bool italic = false, JustificationValues? align = null,
        int? size = null, int indentFirst = 0, int indentLeft = 0, int before = 0, int after = 120,
        bool justify = false, bool keepNext = false)
    {
        var pp = new ParagraphProperties();
        if (keepNext) pp.Append(new KeepNext());
        pp.Append(new SpacingBetweenLines { Before = before.ToString(), After = after.ToString() });
        if (indentFirst > 0 || indentLeft > 0)
        {
            var ind = new Indentation();
            if (indentFirst > 0) ind.FirstLine = indentFirst.ToString();
            if (indentLeft > 0) ind.Left = indentLeft.ToString();
            pp.Append(ind);
        }
        var j = align ?? (justify ? JustificationValues.Both : (JustificationValues?)null);
        if (j != null) pp.Append(new Justification { Val = j.Value });
        var p = new Paragraph(pp);
        AppendText(p, text, bold, italic, size);
        return p;
    }

    private static Paragraph Mixed(params (string Text, bool Bold, bool Underline)[] parts)
    {
        var p = new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { After = "120" },
            new Indentation { FirstLine = "709" },
            new Justification { Val = JustificationValues.Both }));
        foreach (var (text, bold, underline) in parts)
        {
            var r = TextRun(text, bold);
            if (underline) r.RunProperties!.Append(new Underline { Val = UnderlineValues.Single });
            p.Append(r);
        }
        return p;
    }

    private static void AppendText(Paragraph p, string text, bool bold, bool italic, int? size)
    {
        var lines = (text ?? "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var r = TextRun(lines[i], bold, italic, size);
            if (i > 0) r.InsertAfter(new Break(), r.RunProperties!);
            p.Append(r);
        }
    }

    private static Run TextRun(string text, bool bold = false, bool italic = false, int? size = null)
    {
        var rp = new RunProperties();
        if (bold) rp.Append(new Bold());
        if (italic) rp.Append(new Italic());
        if (size != null)
        {
            rp.Append(new FontSize { Val = size.Value.ToString() });
            rp.Append(new FontSizeComplexScript { Val = size.Value.ToString() });
        }
        return new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph PageBreak() => new(new Run(new Break { Type = BreakValues.Page }));

    private static Table NewTable(int[] widths, bool borders = true) =>
        new(TableProps(widths, borders), new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })));

    private static TableProperties TableProps(int[] widths, bool borders = true)
    {
        // Порядок дочерних элементов tblPr задан схемой: tblW → tblBorders → tblLayout → tblCellMar.
        var props = new TableProperties(new TableWidth { Width = widths.Sum().ToString(), Type = TableWidthUnitValues.Dxa });
        if (borders)
            props.Append(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));
        props.Append(new TableLayout { Type = TableLayoutValues.Fixed });
        props.Append(new TableCellMarginDefault(
            new TopMargin { Width = "20", Type = TableWidthUnitValues.Dxa },
            new TableCellLeftMargin { Width = 70, Type = TableWidthValues.Dxa },
            new BottomMargin { Width = "20", Type = TableWidthUnitValues.Dxa },
            new TableCellRightMargin { Width = 70, Type = TableWidthValues.Dxa }));
        return props;
    }

    private static Table Table(int[] widths, string[]? header, string[][] rows, int size = TableSize)
    {
        var t = NewTable(widths);
        if (header != null) t.Append(Row(widths, header.Select(h => (h, 1)).ToArray(), header: true, boldAll: true, size: size, center: true));
        foreach (var r in rows) t.Append(Row(widths, r.Select(c => (c, 1)).ToArray(), size: size));
        return t;
    }

    private static TableRow Row(int[] widths, (string Text, int Span)[] cells, bool header = false, bool boldAll = false,
        int size = TableSize, bool center = false, int leftCols = int.MaxValue, bool noBorders = false)
    {
        var tr = new TableRow();
        var trp = new TableRowProperties(new CantSplit());
        if (header) trp.Append(new TableHeader());
        tr.Append(trp);
        var col = 0;
        var idx = 0;
        foreach (var (text, span) in cells)
        {
            var width = widths.Skip(col).Take(span).Sum();
            var tcp = new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
            if (span > 1) tcp.Append(new GridSpan { Val = span });
            if (noBorders) tcp.Append(new TableCellBorders(new TopBorder { Val = BorderValues.Nil }, new LeftBorder { Val = BorderValues.Nil },
                new BottomBorder { Val = BorderValues.Nil }, new RightBorder { Val = BorderValues.Nil }));
            if (header) tcp.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = "D9D9D9" });
            tcp.Append(new TableCellVerticalAlignment { Val = header ? TableVerticalAlignmentValues.Center : TableVerticalAlignmentValues.Top });
            var alignCenter = header || (center && idx >= Math.Min(leftCols, cells.Length));
            var p = Para(text, bold: boldAll, size: size, after: 0, align: alignCenter ? JustificationValues.Center : null);
            tr.Append(new TableCell(tcp, p));
            col += span;
            idx++;
        }
        return tr;
    }

    private static string Num(decimal v) => v.ToString("0.##", Ru);

    private static string Display(DisciplineCycle c) => c switch
    {
        DisciplineCycle.OOD => "ООД", DisciplineCycle.BD => "БД", DisciplineCycle.PD => "ПД", _ => c.ToString()
    };

    /// <summary>Месяц в родительном падеже: «22 июня», а не «22 июнь».</summary>
    public static string MonthGenitive(DateOnly d) => Ru.DateTimeFormat.MonthGenitiveNames[d.Month - 1];

    public static string CreditWord(int n)
    {
        var m10 = n % 10; var m100 = n % 100;
        if (m10 == 1 && m100 != 11) return "кредит";
        if (m10 is >= 2 and <= 4 && m100 is < 12 or > 14) return "кредита";
        return "кредитов";
    }
}
