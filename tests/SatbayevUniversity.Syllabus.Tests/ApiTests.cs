using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SatbayevUniversity.Syllabus.Infrastructure.Data;

namespace SatbayevUniversity.Syllabus.Tests;

/// <summary>Хост API на SQLite в памяти с демо-данными.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    public string StoragePath { get; } = Path.Combine(Path.GetTempPath(), "syllabus-tests-" + Guid.NewGuid().ToString("N"));
    public int LoginLimit { get; init; } = 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Server=unused");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-0123456789-0123456789-abcdef");
        builder.UseSetting("Seed:DemoData", "true");
        builder.UseSetting("Umkd:StoragePath", StoragePath);
        builder.UseSetting("Umkd:MaxFileSizeBytes", "1048576");
        builder.UseSetting("RateLimit:LoginPerMinute", LoginLimit.ToString());
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        builder.ConfigureServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) || d.ServiceType == typeof(DbContextOptions)).ToList())
                services.Remove(d);
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
        try { Directory.Delete(StoragePath, true); } catch { /* ignore */ }
    }
}

public class ApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> As(string email)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Demo123!" });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonObject> Problem(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<JsonObject>())!;

    private static async Task<JsonObject> FilledTemplate(HttpClient teacher)
    {
        var m = (await teacher.GetFromJsonAsync<JsonObject>("/api/syllabi/new?disciplineId=2"))!;
        m["programIds"] = new JsonArray(1);
        m["goal"] = "Цель"; m["tasks"] = "Задача"; m["description"] = "Описание";
        m["outcomesKnow"] = "a"; m["outcomesCan"] = "b"; m["outcomesSkills"] = "c";
        foreach (var r in m["calendarRows"]!.AsArray())
            if (r!["kind"]!.GetValue<string>() == "Week") { r["lectureTopic"] = "Тема"; r["literatureRefs"] = "[1]"; }
        m["literature"] = new JsonArray(new JsonObject { ["number"] = 1, ["kind"] = "Main", ["text"] = "Книга" });
        m["competencies"] = new JsonArray(Enumerable.Range(0, 25).Select(_ => (JsonNode)JsonValue.Create("x")!).ToArray());
        m["requisites"] = new JsonArray(
            new JsonObject { ["kind"] = "Prerequisite", ["code"] = "A", ["name"] = "A" },
            new JsonObject { ["kind"] = "Postrequisite", ["code"] = "B", ["name"] = "B" });
        return m;
    }

    // ---------------------------------------------------------------- Инфраструктура

    [Fact]
    public async Task Health_is_ok_and_anonymous()
    {
        var res = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("ok", (await res.Content.ReadFromJsonAsync<JsonObject>())!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Anonymous_request_gets_401_problem_with_trace_id()
    {
        var res = await _factory.CreateClient().GetAsync("/api/syllabi");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Wrong_password_gets_401_with_russian_message()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "teacher@esyllabus.local", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("Неверный e-mail или пароль.", (await Problem(res))["title"]!.GetValue<string>());
    }

    [Fact]
    public async Task Malformed_json_gets_400_validation_problem()
    {
        var teacher = await As("teacher@esyllabus.local");
        var res = await teacher.PostAsync("/api/syllabi", new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var p = await Problem(res);
        Assert.NotNull(p["errors"]);
        Assert.NotNull(p["traceId"]);
    }

    [Fact]
    public async Task Unknown_syllabus_gets_404()
    {
        var teacher = await As("teacher@esyllabus.local");
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync("/api/syllabi/999999")).StatusCode);
    }

    [Fact]
    public async Task Non_admin_gets_403_on_admin_api()
    {
        var teacher = await As("teacher@esyllabus.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/admin/users")).StatusCode);
        var student = await As("student@esyllabus.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/syllabi/new")).StatusCode);
    }

    // ---------------------------------------------------------------- Сквозной сценарий

    [Fact]
    public async Task Full_lifecycle_teacher_head_director_student()
    {
        var teacher = await As("teacher@esyllabus.local");
        var head = await As("head@esyllabus.local");
        var director = await As("director@esyllabus.local");
        var student = await As("student@esyllabus.local");

        var created = await teacher.PostAsJsonAsync("/api/syllabi", await FilledTemplate(teacher));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();

        // Студент не видит черновик
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/syllabi/{id}")).StatusCode);

        async Task<HttpResponseMessage> Wf(HttpClient c, object body) => await c.PostAsJsonAsync($"/api/syllabi/{id}/workflow", body);

        Assert.Equal(HttpStatusCode.OK, (await Wf(teacher, new { action = "Submit" })).StatusCode);
        // Автор не может редактировать на рассмотрении
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PutAsJsonAsync($"/api/syllabi/{id}", await FilledTemplate(teacher))).StatusCode);
        // Преподаватель не может одобрить
        Assert.Equal(HttpStatusCode.BadRequest, (await Wf(teacher, new { action = "DepartmentApprove", protocolNumber = "1", protocolDate = "2026-11-01" })).StatusCode);
        // Без протокола — ошибка
        Assert.Equal(HttpStatusCode.BadRequest, (await Wf(head, new { action = "DepartmentApprove" })).StatusCode);
        // Возврат без комментария — ошибка, с комментарием — ок
        Assert.Equal(HttpStatusCode.BadRequest, (await Wf(head, new { action = "Return" })).StatusCode);
        var returned = await (await Wf(head, new { action = "Return", comment = "Уточните цель" })).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Returned", returned!["status"]!.GetValue<string>());
        Assert.Equal("Уточните цель", returned["returnComment"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.OK, (await Wf(teacher, new { action = "Submit" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Wf(head, new { action = "DepartmentApprove", protocolNumber = "7", protocolDate = "2026-11-01" })).StatusCode);
        var approved = await (await Wf(director, new { action = "Approve" })).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Approved", approved!["status"]!.GetValue<string>());
        Assert.Equal("7", approved["protocolNumber"]!.GetValue<string>());
        Assert.Equal("Дайненко Е.А.", approved["approvedByName"]!.GetValue<string>());

        // Студент программы 7M06101 видит утверждённый силлабус и может скачать DOCX
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/syllabi/{id}")).StatusCode);
        var docx = await student.GetAsync($"/api/syllabi/{id}/docx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx.Content.Headers.ContentType!.MediaType);

        // История содержит все шаги
        var details = await teacher.GetFromJsonAsync<JsonObject>($"/api/syllabi/{id}");
        Assert.True(details!["history"]!.AsArray().Count >= 6);
    }

    [Fact]
    public async Task Incomplete_syllabus_cannot_be_submitted()
    {
        var teacher = await As("teacher@esyllabus.local");
        var m = (await teacher.GetFromJsonAsync<JsonObject>("/api/syllabi/new?disciplineId=2"))!;
        var id = (await (await teacher.PostAsJsonAsync("/api/syllabi", m)).Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
        var res = await teacher.PostAsJsonAsync($"/api/syllabi/{id}/workflow", new { action = "Submit" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True((await Problem(res))["issues"]!.AsArray().Count > 0);
        // Черновик можно удалить
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/syllabi/{id}")).StatusCode);
    }

    [Fact]
    public async Task Validate_endpoint_reports_issues_without_saving()
    {
        var teacher = await As("teacher@esyllabus.local");
        var issues = await (await teacher.PostAsJsonAsync("/api/syllabi/validate", await FilledTemplate(teacher))).Content.ReadFromJsonAsync<JsonArray>();
        Assert.DoesNotContain(issues!, i => i!["severity"]!.GetValue<string>() == "error");
    }

    [Fact]
    public async Task Clone_creates_draft_for_next_year()
    {
        var head = await As("head@esyllabus.local");
        var res = await head.PostAsJsonAsync("/api/syllabi/1/clone", new { academicYear = "2027-2028" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (await res.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
        var copy = await head.GetFromJsonAsync<JsonObject>($"/api/syllabi/{id}");
        Assert.Equal("Draft", copy!["status"]!.GetValue<string>());
        Assert.Equal("2027-2028", copy["data"]!["academicYear"]!.GetValue<string>());
        Assert.Equal(12, copy["data"]!["literature"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync("/api/syllabi/1/clone", new { academicYear = "2027/28" })).StatusCode);
    }

    // ---------------------------------------------------------------- УМКД

    [Fact]
    public async Task Umkd_upload_review_and_student_visibility()
    {
        var director = await As("director@esyllabus.local"); // автор демо-силлабуса 1
        var head = await As("head@esyllabus.local");
        var student = await As("student@esyllabus.local");

        using var form = new MultipartFormDataContent
        {
            { new StringContent("2"), "componentTypeId" },
            { new StringContent("Лекция 1"), "title" },
            { new StringContent("true"), "visibleToStudents" },
            { new ByteArrayContent("%PDF-1.4 test"u8.ToArray()), "file", "lecture1.pdf" }
        };
        var up = await director.PostAsync("/api/syllabi/1/umkd", form);
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        var materialId = (await up.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();

        int StudentCount(JsonObject u) => u["components"]!.AsArray().Sum(c => c!["materials"]!.AsArray().Count);
        Assert.Equal(0, StudentCount((await student.GetFromJsonAsync<JsonObject>("/api/syllabi/1/umkd"))!));
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/umkd/materials/{materialId}/download")).StatusCode);

        // Преподаватель не может проверять материалы
        Assert.Equal(HttpStatusCode.Forbidden, (await (await As("teacher@esyllabus.local"))
            .PostAsJsonAsync($"/api/umkd/materials/{materialId}/review", new { status = "Accepted" })).StatusCode);
        // Возврат без комментария — 400
        Assert.Equal(HttpStatusCode.BadRequest, (await head.PostAsJsonAsync($"/api/umkd/materials/{materialId}/review", new { status = "Rejected" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await head.PostAsJsonAsync($"/api/umkd/materials/{materialId}/review", new { status = "Accepted" })).StatusCode);

        Assert.Equal(1, StudentCount((await student.GetFromJsonAsync<JsonObject>("/api/syllabi/1/umkd"))!));
        var file = await student.GetAsync($"/api/umkd/materials/{materialId}/download");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("%PDF-1.4 test", await file.Content.ReadAsStringAsync());

        var zip = await student.GetAsync("/api/syllabi/1/umkd/zip");
        Assert.Equal("application/zip", zip.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Umkd_rejects_bad_uploads()
    {
        var director = await As("director@esyllabus.local");
        async Task<HttpStatusCode> Upload(string fileName, byte[] content, string type = "2")
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(type), "componentTypeId" },
                { new ByteArrayContent(content), "file", fileName }
            };
            return (await director.PostAsync("/api/syllabi/1/umkd", form)).StatusCode;
        }
        Assert.Equal(HttpStatusCode.BadRequest, await Upload("virus.exe", new byte[10]));             // недопустимое расширение
        Assert.Equal(HttpStatusCode.BadRequest, await Upload("big.pdf", new byte[1_100_000]));        // больше лимита (1 МБ в тестах)
        Assert.Equal(HttpStatusCode.BadRequest, await Upload("ok.pdf", new byte[10], type: "1"));     // компонент «Силлабус» формируется автоматически
        Assert.Equal(HttpStatusCode.BadRequest, await Upload("ok.pdf", new byte[10], type: "999"));   // неизвестный компонент
    }

    // ---------------------------------------------------------------- Администрирование

    [Fact]
    public async Task Admin_manages_reference_data_with_validation()
    {
        var admin = await As("admin@esyllabus.local");
        var ok = await admin.PostAsJsonAsync("/api/admin/disciplines", new { id = 0, code = "TST100", name = "Тестовая", cycle = "BD", credits = 5, creditStructure = "1/1/1/0", departmentId = 1 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var dup = await admin.PostAsJsonAsync("/api/admin/disciplines", new { id = 0, code = "TST100", name = "Дубль", cycle = "BD", credits = 5, creditStructure = "", departmentId = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Equal("Дисциплина с таким кодом уже есть.", (await Problem(dup))["title"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/admin/disciplines",
            new { id = 9999, code = "X1", name = "X", cycle = "BD", credits = 5, creditStructure = "", departmentId = 1 })).StatusCode);

        var user = await admin.PostAsJsonAsync("/api/admin/users", new { email = "new@esyllabus.local", fullName = "Новый Пользователь", roles = new[] { "Teacher" }, departmentId = 1, password = "Password123" });
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
        var weak = await admin.PostAsJsonAsync("/api/admin/users", new { email = "weak@esyllabus.local", fullName = "Слабый", roles = new[] { "Teacher" }, password = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var noRole = await admin.PostAsJsonAsync("/api/admin/users", new { email = "nr@esyllabus.local", fullName = "Без роли", roles = Array.Empty<string>(), password = "Password123" });
        Assert.Equal(HttpStatusCode.BadRequest, noRole.StatusCode);
    }

    [Fact]
    public async Task Dashboard_and_reference_work_for_every_role()
    {
        foreach (var email in new[] { "teacher", "head", "director", "student", "admin" })
        {
            var c = await As($"{email}@esyllabus.local");
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/reference")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/syllabi")).StatusCode);
        }
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task Login_is_rate_limited_with_429()
    {
        await using var factory = new ApiFactory { LoginLimit = 3 };
        var client = factory.CreateClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
            last = await client.PostAsJsonAsync("/api/auth/login", new { email = "teacher@esyllabus.local", password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        var p = await last.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Contains("Слишком много", p!["title"]!.GetValue<string>());
    }
}
