using DocumentFormat.OpenXml.Packaging;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Data;
using SatbayevUniversity.Syllabus.Infrastructure.Export;
using SatbayevUniversity.Syllabus.Infrastructure.Services;

namespace SatbayevUniversity.Syllabus.Tests;

/// <summary>Модульные тесты бизнес-правил без БД и HTTP.</summary>
public class DomainTests
{
    private static CourseSyllabus Sample(SyllabusStatus status = SyllabusStatus.Draft, string authorId = "author")
    {
        var inst = new Institute { Id = 1, Name = "Институт", ShortName = "АиИТ", DirectorName = "Директоров Директор Директорович" };
        var dept = new Department { Id = 10, Name = "Программная инженерия", InstituteId = 1, Institute = inst };
        var disc = new Discipline { Id = 5, Code = "CSE1", Name = "Тест", Credits = 4, CreditStructure = "2/0/1/1", DepartmentId = 10, Department = dept };
        var s = new CourseSyllabus
        {
            Id = 42, Discipline = disc, DisciplineId = 5, AcademicYear = "2026-2027", Status = status, AuthorId = authorId,
            Lecturer = new TeacherInfo { FullName = "Иванов Иван Иванович", Email = "i@x.kz", OfficeHours = "пн 10-12" },
            Goal = "Цель", Tasks = "Задача", Description = "Описание", OutcomesKnow = "a", OutcomesCan = "b", OutcomesSkills = "c",
            CompilerName = "Иванов И.И.", HeadOfDepartmentName = "Петров П.П.", WeeksCount = 15,
        };
        s.Programs.Add(new SyllabusProgram { EducationalProgramId = 1, EducationalProgram = new EducationalProgram { Id = 1, Code = "7M06101", Name = "SE" } });
        s.Requisites.Add(new SyllabusRequisite { Kind = RequisiteKind.Prerequisite, Code = "A", Name = "A" });
        s.Requisites.Add(new SyllabusRequisite { Kind = RequisiteKind.Postrequisite, Code = "B", Name = "B" });
        for (var w = 1; w <= 15; w++)
            s.CalendarRows.Add(new CalendarRow { Order = w, Kind = CalendarRowKind.Week, Week = w, LectureTopic = $"Тема {w}", LiteratureRefs = "[1]" });
        s.Literature.Add(new LiteratureItem { Number = 1, Kind = LiteratureKind.Main, Text = "Книга" });
        for (var i = 0; i < 25; i++) s.Competencies.Add(new CompetencyCell { DescriptorIndex = i / 5, CategoryIndex = i % 5, Text = "x" });
        var order = 0;
        foreach (var a in SyllabusService.DefaultAssessments(15))
        {
            var item = new AssessmentItem { Order = order++, Name = a.Name!, MaxPerWeek = a.MaxPerWeek!, IsFinal = a.IsFinal, FinalPoints = a.FinalPoints };
            item.SetWeekPoints(a.Weeks);
            s.Assessments.Add(item);
        }
        return s;
    }

    private static UserContext User(string id, int? dept, int? inst, params string[] roles) =>
        new(id, "Пользователь " + id, roles.ToHashSet(), dept, inst, null);

    // ---------------------------------------------------------------- Проверка по шаблону

    [Fact]
    public void Complete_syllabus_has_no_issues()
    {
        Assert.Empty(SyllabusValidator.Validate(Sample()));
    }

    [Fact]
    public void Default_assessment_schedule_is_60_plus_40()
    {
        var (rating, final) = SyllabusValidator.Totals(Sample());
        Assert.Equal(60, rating);
        Assert.Equal(40, final);
    }

    [Fact]
    public void Wrong_points_total_is_an_error()
    {
        var s = Sample();
        s.Assessments[1].SetWeekPoints(Enumerable.Repeat<decimal?>(null, 15));
        var issues = SyllabusValidator.Validate(s);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("рейтинга допуска"));
    }

    [Fact]
    public void Missing_required_sections_are_errors()
    {
        var s = Sample();
        s.Goal = "";
        s.Literature.Clear();
        s.Programs.Clear();
        var errors = SyllabusValidator.Validate(s).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Section).ToList();
        Assert.Contains("3. Цель и задачи", errors);
        Assert.Contains("7. Литература", errors);
        Assert.Contains("Титульный лист", errors);
    }

    [Fact]
    public void Reference_to_unknown_literature_is_a_warning()
    {
        var s = Sample();
        s.CalendarRows[8].LiteratureRefs = "[1], [810]"; // опечатка из исходного шаблона
        var issue = Assert.Single(SyllabusValidator.Validate(s));
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Contains("[810]", issue.Message);
    }

    [Fact]
    public void Week_out_of_range_is_an_error()
    {
        var s = Sample();
        s.CalendarRows[0].Week = 16;
        Assert.Contains(SyllabusValidator.Validate(s), i => i.Severity == IssueSeverity.Error && i.Message.Contains("16"));
    }

    [Theory]
    [InlineData("Дайненко Евгения Александровна", "Дайненко Е.А.")]
    [InlineData("Иванов Иван", "Иванов И.")]
    [InlineData("Моноимя", "Моноимя")]
    public void Short_name(string full, string expected) => Assert.Equal(expected, WorkflowService.ShortName(full));

    [Theory]
    [InlineData(1, "кредит")] [InlineData(4, "кредита")] [InlineData(5, "кредитов")] [InlineData(11, "кредитов")] [InlineData(22, "кредита")]
    public void Credit_word(int n, string expected) => Assert.Equal(expected, SyllabusDocxExporter.CreditWord(n));

    [Theory]
    [InlineData(1, "января")] [InlineData(6, "июня")] [InlineData(12, "декабря")]
    public void Month_is_genitive(int month, string expected) =>
        Assert.Equal(expected, SyllabusDocxExporter.MonthGenitive(new DateOnly(2026, month, 1)));

    [Fact]
    public void Next_academic_year() => Assert.Equal("2027-2028", SyllabusService.NextAcademicYear("2026-2027"));

    // ---------------------------------------------------------------- Права доступа

    [Fact]
    public void Author_edits_only_draft_or_returned()
    {
        var author = User("author", 10, 1, Roles.Teacher);
        Assert.True(AccessPolicy.CanEdit(author, Sample(SyllabusStatus.Draft)));
        Assert.True(AccessPolicy.CanEdit(author, Sample(SyllabusStatus.Returned)));
        Assert.False(AccessPolicy.CanEdit(author, Sample(SyllabusStatus.OnDepartmentReview)));
        Assert.False(AccessPolicy.CanEdit(author, Sample(SyllabusStatus.Approved)));
    }

    [Fact]
    public void Colleague_sees_but_cannot_edit()
    {
        var colleague = User("other", 10, 1, Roles.Teacher);
        Assert.True(AccessPolicy.CanView(colleague, Sample()));
        Assert.False(AccessPolicy.CanEdit(colleague, Sample()));
    }

    [Fact]
    public void Teacher_of_other_department_cannot_view()
    {
        Assert.False(AccessPolicy.CanView(User("x", 99, 1, Roles.Teacher), Sample()));
    }

    [Fact]
    public void Head_reviews_only_own_department()
    {
        var s = Sample(SyllabusStatus.OnDepartmentReview);
        Assert.True(AccessPolicy.CanDepartmentReview(User("h", 10, 1, Roles.HeadOfDepartment), s));
        Assert.False(AccessPolicy.CanDepartmentReview(User("h2", 11, 1, Roles.HeadOfDepartment), s));
    }

    [Fact]
    public void Director_approves_only_own_institute()
    {
        var s = Sample(SyllabusStatus.DepartmentApproved);
        Assert.True(AccessPolicy.CanFinalApprove(User("d", null, 1, Roles.Director), s));
        Assert.False(AccessPolicy.CanFinalApprove(User("d2", null, 2, Roles.Director), s));
    }

    [Fact]
    public void Student_sees_only_approved_syllabus_of_own_program()
    {
        var student = new UserContext("st", "Студент", new HashSet<string> { Roles.Student }, null, null, 1);
        var other = new UserContext("st2", "Студент", new HashSet<string> { Roles.Student }, null, null, 2);
        Assert.False(AccessPolicy.CanView(student, Sample(SyllabusStatus.Draft)));
        Assert.True(AccessPolicy.CanView(student, Sample(SyllabusStatus.Approved)));
        Assert.False(AccessPolicy.CanView(other, Sample(SyllabusStatus.Approved)));
    }

    [Fact]
    public void Student_sees_only_accepted_visible_materials()
    {
        var student = new UserContext("st", "Студент", new HashSet<string> { Roles.Student }, null, null, 1);
        var s = Sample(SyllabusStatus.Approved);
        Assert.False(AccessPolicy.CanViewMaterial(student, s, new UmkdMaterial { ReviewStatus = MaterialReviewStatus.Pending, VisibleToStudents = true }));
        Assert.False(AccessPolicy.CanViewMaterial(student, s, new UmkdMaterial { ReviewStatus = MaterialReviewStatus.Accepted, VisibleToStudents = false }));
        Assert.True(AccessPolicy.CanViewMaterial(student, s, new UmkdMaterial { ReviewStatus = MaterialReviewStatus.Accepted, VisibleToStudents = true }));
    }

    [Fact]
    public void Workflow_actions_by_role()
    {
        var author = User("author", 10, 1, Roles.Teacher);
        Assert.Equal(new[] { WorkflowAction.Submit }, WorkflowService.AvailableActions(author, Sample()).ToArray());
        Assert.Equal(new[] { WorkflowAction.Withdraw }, WorkflowService.AvailableActions(author, Sample(SyllabusStatus.OnDepartmentReview)).ToArray());
        var head = User("h", 10, 1, Roles.HeadOfDepartment);
        Assert.Contains(WorkflowAction.DepartmentApprove, WorkflowService.AvailableActions(head, Sample(SyllabusStatus.OnDepartmentReview)));
        Assert.Empty(WorkflowService.AvailableActions(head, Sample(SyllabusStatus.DepartmentApproved)));
    }

    // ---------------------------------------------------------------- УМКД

    [Fact]
    public void Umkd_completeness_counts_only_required_accepted_components()
    {
        var types = new List<UmkdComponentType>
        {
            new() { Id = 1, Name = "Силлабус", IsRequired = true, IsSyllabus = true, Order = 1 },
            new() { Id = 2, Name = "Лекции", IsRequired = true, Order = 2 },
            new() { Id = 3, Name = "Глоссарий", IsRequired = false, Order = 3 },
        };
        var s = Sample(SyllabusStatus.Approved);
        s.Materials.Add(new UmkdMaterial { ComponentTypeId = 2, ReviewStatus = MaterialReviewStatus.Pending });
        var sum = UmkdService.Summarize(s, types);
        Assert.Equal((1, 2), (sum.RequiredDone, sum.RequiredTotal));
        s.Materials[0].ReviewStatus = MaterialReviewStatus.Accepted;
        Assert.True(UmkdService.Summarize(s, types).IsComplete);
    }

    // ---------------------------------------------------------------- DOCX

    [Fact]
    public void Docx_export_contains_all_sections_and_is_valid()
    {
        var s = Sample(SyllabusStatus.Approved);
        s.ProtocolNumber = "11";
        s.ProtocolDate = new DateOnly(2026, 6, 22);
        var settings = new TemplateSettings { UniversityName = "Университет", UniversityShortName = "Satbayev University", FormCode = "Ф КазНИТУ 703-08" };
        var bytes = new SyllabusDocxExporter().Export(s, settings, new List<GradeScaleRow> { new() { Letter = "A", Gpa = 4, MinPercent = 95, MaxPercent = 100 } });

        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;
        foreach (var heading in new[] { "СИЛЛАБУС", "УТВЕРЖДАЮ", "1. Информация о преподавателе", "6. Календарно-тематический план",
                     "8. Рамка компетенций", "9. График сдачи требуемых работ", "13. Политика академического поведения и этики", "протокол № 11 от «22» июня 2026" })
            Assert.Contains(heading, text);
        Assert.Contains("Ф КазНИТУ 703-08", doc.MainDocumentPart.FooterParts.Single().Footer!.InnerText);

        var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator();
        var errors = validator.Validate(doc).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(5).Select(e => $"{e.Path?.XPath}: {e.Description}")));
    }

    [Fact]
    public void Seed_texts_match_template_structure()
    {
        Assert.Equal(15, SeedData.Texts.Calendar.Length);
        Assert.Equal(12, SeedData.Texts.Literature.Length);
        Assert.Equal(25, SeedData.Texts.Competencies.Length);
    }
}
