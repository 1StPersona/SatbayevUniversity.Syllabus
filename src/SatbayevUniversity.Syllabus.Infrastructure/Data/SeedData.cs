using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SatbayevUniversity.Syllabus.Domain;
using SatbayevUniversity.Syllabus.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SatbayevUniversity.Syllabus.Infrastructure.Data;

public static class SeedData
{
    public const string DemoPassword = "Demo123!";

    public static async Task InitializeAsync(IServiceProvider sp, IConfiguration config)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var r in Roles.All)
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new IdentityRole(r));

        await SeedReferenceAsync(db);

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@esyllabus.local";
        var adminPassword = config["Seed:AdminPassword"];
        if (await users.FindByEmailAsync(adminEmail) == null && !string.IsNullOrEmpty(adminPassword))
            await CreateUserAsync(users, adminEmail, adminPassword, "Администратор системы", null, null, null, Roles.Admin);

        if (config.GetValue<bool>("Seed:DemoData"))
            await SeedDemoAsync(db, users);
    }

    private static async Task SeedReferenceAsync(AppDbContext db)
    {
        if (!await db.TemplateSettings.AnyAsync())
        {
            db.TemplateSettings.Add(new TemplateSettings
            {
                UniversityName = "Некоммерческое акционерное общество «Казахский национальный исследовательский технический университет имени К.И. Сатпаева»",
                UniversityShortName = "Satbayev University",
                FormCode = "Ф КазНИТУ 703-08",
                DefaultCriteriaNote = "",
                DefaultLatePolicy = Texts.LatePolicy,
                DefaultAcademicPolicy = Texts.AcademicPolicy,
                AcquaintanceNote = "В начале академического семестра обучающимся необходимо ознакомиться с содержанием силлабуса Ф КазНИТУ 401-03. Журнал ознакомления.doc."
            });
        }

        if (!await db.GradeScale.AnyAsync())
        {
            var rows = new (string L, decimal G, int Min, int Max, string C, string T)[]
            {
                ("A", 4m, 95, 100, "Показывает самые высокие стандарты знаний, превышающие объем преподаваемого курса", "«Отлично»"),
                ("A-", 3.67m, 90, 94, "Соответствует самым высоким стандартам знаний", "«Отлично»"),
                ("B+", 3.33m, 85, 89, "Очень хорошо и соответствует высоким стандартам знаний", "«Хорошо»"),
                ("B", 3m, 80, 84, "Хорошо и соответствует большинству высоких стандартов знаний", "«Хорошо»"),
                ("B-", 2.67m, 75, 79, "Более, чем достаточные знания, приближающиеся к высоким стандартам", "«Хорошо»"),
                ("C+", 2.33m, 70, 74, "Достаточные знания, соответствующие общим стандартам", "«Хорошо»"),
                ("C", 2m, 65, 69, "Удовлетворяет и соответствует большинству общих стандартов знаний", "«Удовлетворительно»"),
                ("C-", 1.67m, 60, 64, "Удовлетворяет, но по некоторым знаниям не соответствует стандартам", "«Удовлетворительно»"),
                ("D+", 1.33m, 55, 59, "Минимально удовлетворяет, но по большому спектру знаний не соответствует стандартам", "«Удовлетворительно»"),
                ("D", 1m, 50, 54, "Минимально удовлетворительный проходной балл с сомнительным соответствием стандартам", "«Удовлетворительно»"),
                ("FX", 0.5m, 25, 49, "Временная оценка: Неудовлетворительные низкие показатели, требуется пересдача экзамена", "«Неудовлетворительно»"),
                ("F", 0m, 0, 24, "Не пытался освоить дисциплину. Выставляется также при попытке студента получить оценку на экзамене обманом", "«Неудовлетворительно»"),
            };
            db.GradeScale.AddRange(rows.Select((r, i) => new GradeScaleRow
            {
                Order = i, Letter = r.L, Gpa = r.G, MinPercent = r.Min, MaxPercent = r.Max, Criterion = r.C, Traditional = r.T
            }));
        }

        if (!await db.UmkdComponentTypes.AnyAsync())
        {
            var types = new (string Name, string Desc, bool Req, bool Syl, bool Vis)[]
            {
                ("Силлабус дисциплины", "Формируется в системе; компонент закрывается после утверждения силлабуса.", true, true, true),
                ("Лекционный комплекс", "Конспекты (тезисы) лекций по темам календарно-тематического плана.", true, false, true),
                ("Презентационные материалы", "Слайды к лекциям и практическим занятиям.", true, false, true),
                ("Задания для практических занятий", "Задания и методические указания к практическим работам.", true, false, true),
                ("Задания для лабораторных работ", "Задания и методические указания к лабораторным работам.", false, false, true),
                ("Задания для СРО и СРОП", "Темы, требования к выполнению и критерии оценивания самостоятельной работы.", true, false, true),
                ("Контрольно-измерительные средства", "Вопросы и тесты к рубежным контролям (РК1, РК2).", true, false, false),
                ("Материалы итогового контроля", "Экзаменационные вопросы, билеты, требования к защите проекта.", true, false, false),
                ("Глоссарий", "Основные термины и определения дисциплины.", false, false, true),
                ("Карта обеспеченности литературой", "Наличие основной и дополнительной литературы в библиотеке и электронных базах.", true, false, true),
                ("Электронные ресурсы", "Видеолекции, онлайн-курсы, репозитории, ссылки на электронные библиотеки.", false, false, true),
            };
            db.UmkdComponentTypes.AddRange(types.Select((t, i) => new UmkdComponentType
            {
                Name = t.Name, Description = t.Desc, IsRequired = t.Req, IsSyllabus = t.Syl,
                VisibleToStudentsByDefault = t.Vis, Order = i + 1
            }));
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users, string email, string password,
        string fullName, string? regalia, int? departmentId, int? programId, params string[] roles)
    {
        var u = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, Regalia = regalia,
            DepartmentId = departmentId, EducationalProgramId = programId
        };
        var res = await users.CreateAsync(u, password);
        if (!res.Succeeded)
            throw new InvalidOperationException("Не удалось создать пользователя: " + string.Join("; ", res.Errors.Select(e => e.Description)));
        await users.AddToRolesAsync(u, roles);
        return u;
    }

    private static async Task SeedDemoAsync(AppDbContext db, UserManager<ApplicationUser> users)
    {
        if (await db.Institutes.AnyAsync()) return;

        var inst = new Institute
        {
            Name = "Институт Автоматики и информационных технологий", ShortName = "АиИТ",
            DirectorName = "Дайненко Евгения Александровна"
        };
        var dept = new Department { Name = "Программная инженерия", HeadName = "Утегенова А.У.", HeadTitle = "И.о зав. кафедрой", Institute = inst };
        db.Add(dept);
        var se = new EducationalProgram { Code = "7M06101", Name = "Software Engineering", Level = StudyLevel.Master, Department = dept };
        var ml = new EducationalProgram { Code = "7M06102", Name = "Machine Learning & Data Science", Level = StudyLevel.Master, Department = dept };
        db.AddRange(se, ml);
        var research = new Discipline { Code = "CSE7942", Name = "Reserch Project", Cycle = DisciplineCycle.PD, Credits = 4, CreditStructure = "2/0/1/1", Department = dept };
        var method = new Discipline { Code = "CSE770", Name = "Методология научного исследования и инновационная деятельность", Cycle = DisciplineCycle.BD, Credits = 5, CreditStructure = "2/0/1/2", Department = dept };
        db.AddRange(research, method);
        await db.SaveChangesAsync();

        var director = await CreateUserAsync(users, "director@esyllabus.local", DemoPassword, "Дайненко Евгения Александровна",
            "PhD, ассоциированный профессор", dept.Id, null, Roles.Director, Roles.Teacher);
        director.InstituteId = inst.Id;
        await users.UpdateAsync(director);
        await CreateUserAsync(users, "head@esyllabus.local", DemoPassword, "Утегенова Анар Уалиевна", "к.т.н., ассоциированный профессор",
            dept.Id, null, Roles.HeadOfDepartment, Roles.Teacher);
        await CreateUserAsync(users, "teacher@esyllabus.local", DemoPassword, "Иванов Иван Иванович", "магистр технических наук, старший преподаватель",
            dept.Id, null, Roles.Teacher);
        await CreateUserAsync(users, "student@esyllabus.local", DemoPassword, "Студентов Студент", null, null, se.Id, Roles.Student);
        if (await users.FindByEmailAsync("admin@esyllabus.local") == null)
            await CreateUserAsync(users, "admin@esyllabus.local", DemoPassword, "Администратор системы", null, null, null, Roles.Admin);

        var s = BuildResearchProject(research, director, se, ml);
        db.Syllabi.Add(s);
        await db.SaveChangesAsync();
        db.ApprovalLog.AddRange(
            new ApprovalLogEntry { SyllabusId = s.Id, FromStatus = SyllabusStatus.Draft, ToStatus = SyllabusStatus.OnDepartmentReview, Action = "Отправить на рассмотрение кафедры", UserId = director.Id, UserName = director.FullName, At = new DateTime(2026, 6, 15, 6, 0, 0, DateTimeKind.Utc) },
            new ApprovalLogEntry { SyllabusId = s.Id, FromStatus = SyllabusStatus.OnDepartmentReview, ToStatus = SyllabusStatus.DepartmentApproved, Action = "Одобрить на заседании кафедры", Comment = "Протокол № 11 от 22.06.2026", UserId = "", UserName = "Утегенова Анар Уалиевна", At = new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc) },
            new ApprovalLogEntry { SyllabusId = s.Id, FromStatus = SyllabusStatus.DepartmentApproved, ToStatus = SyllabusStatus.Approved, Action = "Утвердить", UserId = director.Id, UserName = director.FullName, At = new DateTime(2026, 6, 24, 9, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
    }

    private static CourseSyllabus BuildResearchProject(Discipline d, ApplicationUser author, EducationalProgram se, EducationalProgram ml)
    {
        var teacher = new TeacherInfo
        {
            FullName = "Дайненко Евгения Александровна",
            Regalia = "PhD, ассоциированный профессор",
            Office = "ККЦ, 401 кабинет",
            OfficeHours = "понедельник – пятница, 15:00 – 17:00",
            Email = "y.daineko@satbayev.university"
        };
        var s = new CourseSyllabus
        {
            Discipline = d, AcademicYear = "2026-2027", Course = 2, Semester = 3, StudyForm = "очное", Language = "русский", City = "Алматы",
            Lecturer = new TeacherInfo
            {
                FullName = teacher.FullName, Regalia = teacher.Regalia + ", директор Института автоматики и информационных технологий",
                Office = teacher.Office, OfficeHours = teacher.OfficeHours, Email = teacher.Email
            },
            PracticeTeacher = teacher,
            Goal = Texts.Goal, Tasks = Texts.Tasks, Description = Texts.Description,
            OutcomesKnow = Texts.Know, OutcomesCan = Texts.Can, OutcomesSkills = Texts.Skills,
            LatePolicy = Texts.LatePolicy, AcademicPolicy = Texts.AcademicPolicy,
            ProtocolNumber = "11", ProtocolDate = new DateOnly(2026, 6, 22),
            HeadOfDepartmentName = "Утегенова А.У.", HeadOfDepartmentTitle = "И.о зав. кафедрой",
            CompilerName = "Дайненко Е.А.", AgreedBy = "Маденова С.А.\nЖумагалиева А.С.",
            ApprovedByName = "Дайненко Е.А.", ApprovedByTitle = "Директор института АиИТ", ApprovedDate = new DateOnly(2026, 6, 24),
            Status = SyllabusStatus.Approved, AuthorId = author.Id, WeeksCount = 15,
            CreatedAt = new DateTime(2026, 6, 10, 6, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 6, 24, 9, 0, 0, DateTimeKind.Utc),
        };
        s.Programs.Add(new SyllabusProgram { EducationalProgram = se });
        s.Programs.Add(new SyllabusProgram { EducationalProgram = ml });
        s.Requisites.Add(new SyllabusRequisite { Kind = RequisiteKind.Prerequisite, Code = "CSE770", Name = "Методология научного исследования и инновационная деятельность" });
        s.Requisites.Add(new SyllabusRequisite { Kind = RequisiteKind.Postrequisite, Code = "ECA212", Name = "Оформление и защита магистерской диссертации" });

        var order = 0;
        foreach (var (w, lec, pr, sro, lit) in Texts.Calendar)
        {
            s.CalendarRows.Add(new CalendarRow { Order = order++, Kind = CalendarRowKind.Week, Week = w, LectureTopic = lec, PracticeTopic = pr, SroTopic = sro, LiteratureRefs = lit, Deadline = $"Неделя {w}" });
            if (w == 8) s.CalendarRows.Add(new CalendarRow { Order = order++, Kind = CalendarRowKind.Milestone, LectureTopic = "Первая промежуточная аттестация", Deadline = "Неделя 8" });
        }
        s.CalendarRows.Add(new CalendarRow { Order = order++, Kind = CalendarRowKind.Milestone, LectureTopic = "Вторая промежуточная аттестация", Deadline = "Неделя 15" });
        s.CalendarRows.Add(new CalendarRow { Order = order, Kind = CalendarRowKind.Milestone, LectureTopic = "Итоговый экзамен — устная форма (публичная защита исследовательского проекта).", Deadline = "по расписанию" });

        foreach (var (n, kind, text) in Texts.Literature)
            s.Literature.Add(new LiteratureItem { Number = n, Kind = kind, Text = text });

        for (var di = 0; di < 5; di++)
            for (var ci = 0; ci < 5; ci++)
                s.Competencies.Add(new CompetencyCell { DescriptorIndex = di, CategoryIndex = ci, Text = Texts.Competencies[di, ci] });

        var i = 0;
        foreach (var a in SyllabusService.DefaultAssessments(15))
        {
            var item = new AssessmentItem { Order = i++, Name = a.Name!, MaxPerWeek = a.MaxPerWeek!, IsFinal = a.IsFinal, FinalPoints = a.FinalPoints };
            item.SetWeekPoints(a.Weeks);
            if (a.IsFinal) item.Name = "Итоговый экзамен";
            s.Assessments.Add(item);
        }
        return s;
    }

    /// <summary>Тексты демонстрационного силлабуса CSE7942 (из шаблона кафедры).</summary>
    public static class Texts
    {
        public const string Goal = "формирование у магистрантов компетенций самостоятельного планирования, проведения и представления научного исследования в области программной инженерии, машинного обучения и анализа данных — от постановки исследовательской проблемы до публикации воспроизводимых результатов, отвечающих принципам академической честности, ответственного искусственного интеллекта и Целей устойчивого развития ООН (ЦУР 4, 8, 9, 10).";

        public const string Tasks =
            "научить выявлять исследовательский пробел (research gap) и формулировать проблему, цель, задачи, объект и предмет исследования;\n" +
            "сформировать навыки проведения систематического обзора литературы по базам Scopus, Web of Science, IEEE Xplore, ACM DL и Springer Link;\n" +
            "освоить методологию эмпирических исследований в программной инженерии: контролируемый эксперимент, кейс-стади, опрос, симуляция, исследование действием;\n" +
            "выработать умение проектировать эксперимент, подбирать baseline и метрики, проверять статистические гипотезы и оценивать размер эффекта;\n" +
            "обеспечить владение практиками воспроизводимых и открытых исследований (версионирование, контейнеризация, FAIR-принципы, публикация данных и кода);\n" +
            "развить навыки междисциплинарной командной работы над проектами в области ИТ и искусственного интеллекта, в том числе с учётом принципов инклюзивности и доступности цифровых решений;\n" +
            "сформировать культуру академической честности, корректного использования генеративного ИИ и этичного обращения с данными;\n" +
            "научить представлять результаты в формате научной статьи (IMRaD), доклада и защиты исследовательского проекта.";

        public const string Description =
            "Курс предназначен для обучающихся по образовательным программам 7M06101 «Software Engineering» и 7M06102 «Machine Learning & Data Science» (2 год обучения, научно-педагогическое направление магистратуры).\n" +
            "«Reserch Project» — интегрирующая дисциплина, в рамках которой магистрант проходит полный цикл научного исследования на материале собственной темы магистерской диссертации. Лекционная часть даёт методологический каркас: философию и логику научного познания, типологию исследовательских дизайнов, стандарты эмпирических исследований ACM SIGSOFT, требования к валидности и воспроизводимости. Практическая часть построена по принципу проектного обучения: каждая работа — это очередной артефакт реального исследования (карта литературы, research proposal, протокол эксперимента, репозиторий с данными и кодом, черновик статьи, презентация).\n" +
            "Курс имеет выраженный междисциплинарный характер: тематика проектов связывает программную инженерию и науку о данных с прикладными областями (образование, здравоохранение, энергетика, промышленность, городская среда). Отдельное внимание уделяется новым методам обучения — работе с научными репозиториями открытого доступа, применению генеративного ИИ как вспомогательного инструмента при соблюдении требований прозрачности, а также инклюзивному дизайну исследований и цифровых артефактов.\n" +
            "Пререквизиты: «Методы научных исследований», «Философия и история науки», «Анализ данных», владение английским языком на уровне не ниже B2.\n" +
            "Постреквизиты: «Научно-исследовательская работа магистранта (НИРМ)», выполнение и защита магистерской диссертации.";

        public const string Know =
            "логику и структуру научного исследования, соотношение проблемы, цели, задач, гипотез и исследовательских вопросов;\n" +
            "типологию исследовательских дизайнов в программной инженерии и науке о данных, их сильные стороны и ограничения;\n" +
            "требования эмпирических стандартов ACM SIGSOFT и принципы открытой науки (FAIR, воспроизводимость, открытый доступ);\n" +
            "нормы академической этики, правила соавторства и требования к оформлению научных работ и магистерской диссертации в Республике Казахстан.";

        public const string Can =
            "проводить систематический обзор литературы и обосновывать научную новизну собственного исследования;\n" +
            "выбирать и обосновывать исследовательский дизайн, адекватный поставленным вопросам;\n" +
            "планировать эксперимент, подбирать метрики и baseline, проводить статистический анализ и корректно интерпретировать результаты;\n" +
            "выявлять и описывать угрозы валидности, оценивать этические риски и потенциальную предвзятость моделей;\n" +
            "готовить научную статью, доклад и постер, соответствующие требованиям рецензируемых изданий.";

        public const string Skills =
            "работы с научными базами данных, менеджерами библиографии (Zotero, Mendeley) и системами проверки на заимствования;\n" +
            "организации воспроизводимого вычислительного эксперимента (Git, виртуальные окружения, контейнеры, публикация артефактов с DOI);\n" +
            "визуализации и представления научных данных, публичной защиты результатов исследования;\n" +
            "научной коммуникации и командной работы в междисциплинарном исследовательском коллективе.";

        public const string LatePolicy =
            "Обучающийся должен прийти подготовленным к лекционным и практическим (лабораторным) занятиям. Требуется своевременная защита и полное выполнение всех видов работ (практических, и самостоятельных). Обучающийся не должен опаздывать и пропускать занятия, быть пунктуальным и обязательным. Предусматривается уменьшение максимального балла на 10% за несвоевременно сданные работы. Если Вы вынуждены пропустить промежуточную аттестацию по уважительным причинам, Вы должны предупредить преподавателя заранее до нее, чтобы была возможность сдать рубежный контроль заранее. Пропуск экзамена по неуважительной причине лишает Вас права на его сдачу. При пропуске экзамена по уважительной причине оформляется специальное разрешение и назначается дата, время и место сдачи экзамена.";

        public const string AcademicPolicy =
            "Будьте толерантны, уважайте чужое мнение. Возражения формулируйте в корректной форме. Плагиат и другие формы нечестной работы недопустимы. Недопустимы подсказывание и списывание во время экзаменов, сдача экзамена за другого студента. Студент, уличенный в фальсификации любой информации курса, получит итоговую оценку «F».\n" +
            "Активность на лекционных и практических занятиях обязательна и является одной из составляющих Вашего итогового балла / оценки. Многие теоретические вопросы, подкрепляющие лекционный материал, будут представлены лишь на лекциях. Следовательно, пропуск занятия может повлиять на Вашу успеваемость и итоговую оценку. Однако посещение занятий само по себе еще не означает увеличение баллов. Необходимо Ваше постоянное активное участие на занятиях. Обязательным требованием курса является подготовка к каждому занятию.\n" +
            "Помощь: За консультациями по выполнению самостоятельных работ, их сдачей и защитой, а также за дополнительной информацией по пройденному материалу и всеми другими возникающими вопросами по читаемому курсу обращайтесь к преподавателю в период его офис часов или через электронные средства связи в рабочее время.\n" +
            "При обучении обязательное участие на учебных занятиях согласно расписанию, которая определяет готовность к занятию. В случае отсутствия на занятии студент обязан в течение суток известить преподавателя и объяснить план самостоятельного изучения занятия: обязательное прочтение представленных материалов до занятия; сдача заданий вовремя; 20% неучастия в аудиториях (по уважительной причине с подтверждающими документами) - оценка «F (Fail)»; плагиатизм и списывание при выполнении задания не допустимы; обязательное использование электронных гаджетов на занятии, что приветствуется, но недопустимо использование на экзамене.\n" +
            "В рамках обучения по дисциплине недопустимы любые появления академической нечестности, академической непорядочности, академического обмана и коррупций в любой форме. Организатор таких действий (преподаватель, обучающиеся или третьи лица по их поручению) несут полную ответственность за нарушение законов Республики Казахстан.";

        public static readonly (int Week, string Lecture, string Practice, string Sro, string Lit)[] Calendar =
        {
            (1, "Введение в академические исследования в области компьютерных наук", "Классификация опубликованных работ на теоретические, экспериментальные и прикладные исследования", "Сравнение различных парадигм исследований в области компьютерных наук (теоретические, прикладные и экспериментальные)", "[3], [7]"),
            (2, "Обзор литературы по компьютерным наукам", "Формулирование одного исследовательского вопроса и соответствующей гипотезы", "Разработка 3 возможных исследовательских вопросов в вашей специализации и преобразование их в гипотезы", "[2], [8]"),
            (3, "Исследовательские вопросы и гипотезы", "Проведение поиска литературы и подготовка краткой аннотированной библиографии", "Подготовка структурированного обзора литературы (не менее 10 источников) по выбранной теме исследования", "[1], [7]"),
            (4, "Структура научной статьи: IMRaD", "Создание плана статьи в соответствии со структурой IMRaD", "Анализ 2 статей с ведущих конференций (ACM/IEEE) и составление карты их структуры", "[1], [7]"),
            (5, "Методы исследования в компьютерных науках", "Выбор наиболее подходящего метода исследования (эксперимент, прототип, модель) для данного случая", "Сравнение преимуществ и ограничений экспериментального, прототипного и аналитического методов", "[3], [4]"),
            (6, "Управление данными и артефактами в исследованиях", "Организуйте набор данных и контролируйте его версии с помощью Git (или аналогичных инструментов)", "Создайте контрольный список воспроизводимости для вашего собственного исследовательского проекта", "[1], [9]"),
            (7, "План эксперимента и метрики оценки", "Определите независимые и зависимые переменные для выборочного эксперимента", "Разработайте план эксперимента, включая переменные, контрольные элементы и оценочные метрики", "[5], [3]"),
            (8, "Эксперименты в области системных и бэкенд-исследований", "", "", "[4], [3]"),
            (9, "Инженерные практики в исследованиях", "Создание репозитория GitHub с базовой CI и одним модульным тестом", "Настройка CI/CD и автоматизированного тестирования для небольшого исследовательского проекта", "[1], [810]"),
            (10, "Анализ результатов и визуализация", "Анализ небольшого набора данных и создание наглядного графика результатов", "Проведение статистического анализа открытого набора данных и визуализация результатов", "[2], [7]"),
            (11, "Научное письмо: структура, стиль и аргументация", "Переписывание чернового абзаца для улучшения ясности и аргументации", "Написание разделов «Введение» и «Вклад» для вашей проектной работы", "[10], [1]"),
            (12, "Научный стиль и форматирование: шаблоны LaTeX, ACM и IEEE", "Форматирование краткого документа в LaTeX с использованием шаблона ACM или IEEE", "Форматирование черновика статьи в LaTeX с использованием шаблона ACM или IEEE, включая ссылки с помощью BibTeX", "[8], [2]"),
            (13, "Процесс рецензирования и подачи работы", "Модели и риски облачных сервисов", "Моделирование процесса рецензирования: рецензирование черновика коллеги и написание ответа на полученные комментарии", "[2], [1]"),
            (14, "Презентация исследований: доклады, постеры и вопросы-ответы", "Подготовка 5-слайдовой презентации, summarizing проект", "Подготовка черновика исследовательского постера (формат A1), summarizing ваш проект", "[10], [1]"),
            (15, "Этика, воспроизводимость и долгосрочное влияние исследований", "[7], [2]", "", ""),
        };

        public static readonly (int N, LiteratureKind Kind, string Text)[] Literature =
        {
            (1, LiteratureKind.Main, "RMIT University Library – Research and Writing Skills for Academic and Graduate Researchers (2022)\nPublisher: RMIT Open Press\nISBN: 978-0-6453261-3-1"),
            (2, LiteratureKind.Main, "Guilford Press; Routledge – Research Methods FreeBook (2020)\nPublisher: Routledge / Guilford Press"),
            (3, LiteratureKind.Main, "Serge Demeyer – Research Methods in Computer Science (2022)\nPublisher: University of Antwerp"),
            (4, LiteratureKind.Main, "M. M. Ayash – Research Methodologies in Computer Science and Information Systems (2019)\nPublisher: Alquds Open University"),
            (5, LiteratureKind.Main, "Брюс П., Брюс Э., Гедек П. Практическая статистика для специалистов Data Science: 50+ важнейших понятий с использованием R и Python. — 2-е изд. — Санкт-Петербург : БХВ-Петербург, 2021. — 352 с. — ISBN 978-5-9775-6705-3"),
            (6, LiteratureKind.Main, "Мокий М.С., Никифоров А.Л., Мокий В.С. Методология научных исследований : учебник для вузов / под ред. М.С. Мокия. — 3-е изд., перераб. и доп. — Москва : Юрайт, 2024. — 254 с. — ISBN 978-5-534-18527-0. — URL: https://urait.ru"),
            (7, LiteratureKind.Additional, "Geoff Cumming, Robert Calin-Jageman – Introduction to the New Statistics: Estimation, Open Science, and Beyond (2017)\nPublisher: Routledge\nISBN: 978-1138825529"),
            (8, LiteratureKind.Additional, "James Jaccard, Jacob Jacoby – Theory Construction and Model-Building Skills (2010)\nPublisher: Guilford Press\nISBN: 978-1606233406"),
            (9, LiteratureKind.Additional, "Rex B. Kline – Becoming a Behavioral Science Researcher: A Guide to Producing Research That Matters (2009)\nPublisher: Guilford Press\nISBN: 978-1606233420"),
            (10, LiteratureKind.Additional, "Angelika H. Hofmann – Scientific Writing and Communication: Papers, Proposals, and Presentations, 3rd Edition (2020)\nPublisher: Oxford University Press\nISBN: 978-0190278540"),
            (11, LiteratureKind.Additional, "Правила присуждения степеней : утв. приказом Министра науки и высшего образования Республики Казахстан (с изменениями и дополнениями). — URL: https://adilet.zan.kz"),
            (12, LiteratureKind.Additional, "Кузнецов И.Н. Основы научных исследований : учебное пособие. — 5-е изд. — Москва : Дашков и К°, 2021. — 284 с. — ISBN 978-5-394-04170-4"),
        };

        public static readonly string[,] Competencies =
        {
            {
                "Понимает логику научного познания, соотношение теории, гипотезы и эмпирического факта",
                "Знает нормы академической этики, правила соавторства и требования законодательства РК о персональных данных",
                "Знает типологию исследовательских дизайнов и эмпирические стандарты в инженерных науках",
                "Владеет терминологией научного английского языка, понимает структуру статьи IMRaD",
                "Знает методы эмпирических исследований в программной инженерии, машинном обучении и анализе данных"
            },
            {
                "Применяет общенаучные методы (анализ, синтез, моделирование, абстрагирование) к постановке задачи",
                "Применяет принципы академической честности и ответственного ИИ при работе с данными и текстами",
                "Планирует эксперимент, подбирает метрики и baseline, организует сбор и обработку данных",
                "Готовит аннотацию, доклад и постер на английском языке для международной аудитории",
                "Реализует воспроизводимый вычислительный эксперимент с публикацией кода и данных"
            },
            {
                "Критически оценивает достоверность научных источников и обоснованность выводов",
                "Оценивает социальные, этические и экологические последствия предлагаемых ИТ-решений в контексте ЦУР",
                "Выявляет и описывает угрозы валидности, обосновывает границы применимости результатов",
                "Аргументированно защищает позицию в научной дискуссии и рецензировании",
                "Сопоставляет альтернативные модели и алгоритмы по метрикам качества, стоимости и объяснимости"
            },
            {
                "Формулирует оригинальную исследовательскую идею и обосновывает её новизну",
                "Работает в междисциплинарной команде с учётом принципов инклюзивности и уважения к различиям",
                "Оформляет результаты в виде технического отчёта и научной статьи",
                "Ведёт научную переписку, участвует в слепом рецензировании, выступает на конференции",
                "Представляет результаты средствами научной визуализации данных"
            },
            {
                "Самостоятельно осваивает новые методы и инструменты научного исследования",
                "Планирует индивидуальную траекторию профессионального и научного развития",
                "Использует системы контроля версий, среды виртуализации и CI для организации исследования",
                "Работает с международными научными базами данных и открытыми репозиториями",
                "Применяет Python/R, менеджеры библиографии и генеративный ИИ как вспомогательный инструмент с указанием источника"
            }
        };
    }
}
