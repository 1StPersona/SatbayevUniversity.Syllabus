#!/usr/bin/env python3
"""
Системный тест API на поднятом стеке docker compose (через nginx).
Только стандартная библиотека Python. Запуск:
    python3 backend/tests/system/api_system_test.py http://localhost:8080
Ожидает демо-данные (SEED_DEMO_DATA=true) и пароль демо-пользователей Demo123!.
"""
import io
import json
import sys
import time
import urllib.error
import urllib.request
import uuid
import zipfile

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080").rstrip("/")
PASSWORD = "Demo123!"
results = []


class Resp:
    def __init__(self, status, headers, body):
        self.status, self.headers, self.body = status, headers, body

    def json(self):
        return json.loads(self.body.decode("utf-8")) if self.body else None


def call(method, path, token=None, body=None, raw=None, content_type=None):
    headers = {}
    data = None
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    if raw is not None:
        data = raw
        headers["Content-Type"] = content_type
    req = urllib.request.Request(BASE + path, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return Resp(r.status, dict(r.headers), r.read())
    except urllib.error.HTTPError as e:
        return Resp(e.code, dict(e.headers), e.read())


def check(name, cond, detail=""):
    results.append((name, bool(cond), detail))
    print(("  OK   " if cond else "  FAIL ") + name + (f" — {detail}" if detail and not cond else ""))


def multipart(fields, file_field=None, file_name=None, file_bytes=None):
    boundary = uuid.uuid4().hex
    out = io.BytesIO()
    for k, v in fields.items():
        out.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n".encode())
    if file_field:
        out.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{file_field}\"; filename=\"{file_name}\"\r\n"
                  f"Content-Type: application/octet-stream\r\n\r\n".encode())
        out.write(file_bytes)
        out.write(b"\r\n")
    out.write(f"--{boundary}--\r\n".encode())
    return out.getvalue(), f"multipart/form-data; boundary={boundary}"


def login(email):
    r = call("POST", "/api/auth/login", body={"email": email, "password": PASSWORD})
    assert r.status == 200, f"login {email}: {r.status} {r.body[:200]}"
    return r.json()["token"]


def main():
    print(f"Системный тест API: {BASE}")

    print("\n[Инфраструктура]")
    r = call("GET", "/health")
    check("health = ok, БД доступна", r.status == 200 and r.json()["checks"]["database"] == "Healthy", r.body)
    r = call("GET", "/swagger/v1/swagger.json")
    check("Swagger-описание API доступно", r.status == 200 and "/api/syllabi" in r.body.decode())
    r = call("GET", "/syllabi/1")
    check("SPA: глубокая ссылка отдаёт index.html", r.status == 200 and b"<app-root>" in r.body)

    print("\n[Аутентификация и ошибки доступа]")
    tokens = {role: login(f"{role}@esyllabus.local") for role in ["teacher", "head", "director", "student", "admin"]}
    check("вход всех 5 ролей", len(tokens) == 5)
    r = call("POST", "/api/auth/login", body={"email": "teacher@esyllabus.local", "password": "wrong"})
    check("неверный пароль → 401 с русским сообщением", r.status == 401 and r.json()["title"] == "Неверный e-mail или пароль.")
    r = call("GET", "/api/syllabi")
    check("без токена → 401", r.status == 401)
    r = call("GET", "/api/syllabi", token="garbage.token.value")
    check("поддельный токен → 401", r.status == 401)
    r = call("GET", "/api/admin/users", token=tokens["teacher"])
    check("преподаватель в админке → 403", r.status == 403)
    r = call("GET", "/api/syllabi/new", token=tokens["student"])
    check("обучающийся создаёт силлабус → 403", r.status == 403)
    r = call("GET", "/api/syllabi/987654", token=tokens["teacher"])
    check("несуществующий силлабус → 404", r.status == 404)
    r = call("POST", "/api/syllabi", token=tokens["teacher"], raw=b"{ broken", content_type="application/json")
    p = r.json() or {}
    check("битый JSON → 400 ValidationProblem с traceId", r.status == 400 and "errors" in p and "traceId" in p, r.body[:200])
    r = call("POST", "/api/syllabi", token=tokens["teacher"], raw=b"x=1", content_type="text/plain")
    check("неподдерживаемый Content-Type → 415", r.status == 415)
    r = call("DELETE", "/api/reference", token=tokens["teacher"])
    check("неподдерживаемый метод → 405", r.status == 405)

    print("\n[Демо-силлабус из шаблона]")
    r = call("GET", "/api/syllabi/1", token=tokens["director"])
    s = r.json()
    check("CSE7942 утверждён, 60+40 баллов", s["status"] == "Approved" and s["ratingTotal"] == 60 and s["finalTotal"] == 40)
    check("проверка нашла опечатку [810] из исходного PDF", any("[810]" in i["message"] for i in s["issues"]))
    r = call("GET", "/api/syllabi/1/docx", token=tokens["student"])
    ok_docx = False
    if r.status == 200:
        z = zipfile.ZipFile(io.BytesIO(r.body))
        xml = z.read("word/document.xml").decode("utf-8")
        ok_docx = all(t in xml for t in ["СИЛЛАБУС", "УТВЕРЖДАЮ", "Рамка компетенций", "протокол № 11", "июня"])
    check("обучающийся скачивает DOCX; в нём разделы шаблона и русская дата", ok_docx)

    print("\n[Жизненный цикл нового силлабуса]")
    t = tokens["teacher"]
    m = call("GET", "/api/syllabi/new?disciplineId=2", token=t).json()
    r = call("POST", "/api/syllabi", token=t, body=m)
    draft_id = r.json()["id"]
    r = call("POST", f"/api/syllabi/{draft_id}/workflow", token=t, body={"action": "Submit"})
    check("незаполненный силлабус не отправляется (400 + список ошибок)", r.status == 400 and len(r.json().get("issues", [])) > 0)
    check("черновик удаляется", call("DELETE", f"/api/syllabi/{draft_id}", token=t).status == 204)

    m.update({"programIds": [1], "goal": "Цель", "tasks": "Задача 1\nЗадача 2", "description": "Описание курса",
              "outcomesKnow": "знать", "outcomesCan": "уметь", "outcomesSkills": "владеть"})
    for row in m["calendarRows"]:
        if row["kind"] == "Week":
            row["lectureTopic"] = f"Лекция {row['week']}"
            row["literatureRefs"] = "[1]"
    m["literature"] = [{"number": 1, "kind": "Main", "text": "Учебник"}]
    m["competencies"] = ["компетенция"] * 25
    m["requisites"] = [{"kind": "Prerequisite", "code": "A1", "name": "A"}, {"kind": "Postrequisite", "code": "B1", "name": "B"}]
    r = call("POST", "/api/syllabi", token=t, body=m)
    check("преподаватель создаёт заполненный силлабус → 201", r.status == 201)
    sid = r.json()["id"]
    issues = call("POST", "/api/syllabi/validate", token=t, body=m).json()
    check("проверка по шаблону без ошибок", not [i for i in issues if i["severity"] == "error"], issues)

    def wf(role, **body):
        return call("POST", f"/api/syllabi/{sid}/workflow", token=tokens[role], body=body)

    check("отправка на кафедру", wf("teacher", action="Submit").json()["status"] == "OnDepartmentReview")
    check("редактирование на рассмотрении → 403", call("PUT", f"/api/syllabi/{sid}", token=t, body=m).status == 403)
    check("директор не может действовать до кафедры → 400", wf("director", action="Approve").status == 400)
    check("возврат без комментария → 400", wf("head", action="Return").status == 400)
    check("возврат с комментарием", wf("head", action="Return", comment="Доработайте цель").json()["status"] == "Returned")
    m["goal"] = "Уточнённая цель"
    check("автор правит возвращённый → 204", call("PUT", f"/api/syllabi/{sid}", token=t, body=m).status == 204)
    wf("teacher", action="Submit")
    check("одобрение кафедрой без протокола → 400", wf("head", action="DepartmentApprove").status == 400)
    r = wf("head", action="DepartmentApprove", protocolNumber="3", protocolDate="2026-10-30")
    check("одобрение кафедрой с протоколом", r.json()["status"] == "DepartmentApproved")
    r = wf("director", action="Approve")
    a = r.json()
    check("утверждение директором, гриф заполнен", a["status"] == "Approved" and a["approvedByName"] == "Дайненко Е.А.")
    check("в истории все 6 шагов (создан, отправлен, возвращён, отправлен, одобрен, утверждён)", len(a["history"]) == 6, len(a["history"]))
    check("обучающийся видит утверждённый силлабус", call("GET", f"/api/syllabi/{sid}", token=tokens["student"]).status == 200)

    print("\n[УМКД]")
    body, ct = multipart({"componentTypeId": "2", "title": "Лекция 1", "visibleToStudents": "true"}, "file", "lecture1.pdf", b"%PDF-1.4 demo")
    r = call("POST", f"/api/syllabi/{sid}/umkd", token=t, raw=body, content_type=ct)
    check("загрузка материала", r.status == 200 and r.json()["reviewStatus"] == "Pending")
    mid = r.json()["id"]
    body, ct = multipart({"componentTypeId": "11", "title": "Онлайн-курс", "url": "https://openedu.kz/course", "visibleToStudents": "true"})
    check("материал-ссылка", call("POST", f"/api/syllabi/{sid}/umkd", token=t, raw=body, content_type=ct).status == 200)
    body, ct = multipart({"componentTypeId": "2"}, "file", "run.exe", b"MZ")
    r = call("POST", f"/api/syllabi/{sid}/umkd", token=t, raw=body, content_type=ct)
    check("недопустимый тип файла → 400", r.status == 400 and "Недопустимый тип" in r.json()["title"])
    body, ct = multipart({"componentTypeId": "2"}, "file", "big.pdf", b"0" * (51 * 1024 * 1024))
    r = call("POST", f"/api/syllabi/{sid}/umkd", token=t, raw=body, content_type=ct)
    check("файл 51 МБ → 400 (лимит 50 МБ)", r.status == 400, f"{r.status} {r.body[:150]}")
    body, ct = multipart({"componentTypeId": "2"}, "file", "huge.pdf", b"0" * (62 * 1024 * 1024))
    r = call("POST", f"/api/syllabi/{sid}/umkd", token=t, raw=body, content_type=ct)
    check("файл 62 МБ → 413 (лимит запроса)", r.status == 413, f"{r.status}")
    body, ct = multipart({"componentTypeId": "2"})
    check("ни файла, ни ссылки → 400", call("POST", f"/api/syllabi/{sid}/umkd", token=t, raw=body, content_type=ct).status == 400)

    def student_materials():
        u = call("GET", f"/api/syllabi/{sid}/umkd", token=tokens["student"]).json()
        return sum(len(c["materials"]) for c in u["components"])

    check("до проверки обучающийся не видит материалы", student_materials() == 0)
    check("скачивание непроверенного материала обучающимся → 403",
          call("GET", f"/api/umkd/materials/{mid}/download", token=tokens["student"]).status == 403)
    check("преподаватель не проверяет материалы → 403",
          call("POST", f"/api/umkd/materials/{mid}/review", token=t, body={"status": "Accepted"}).status == 403)
    check("заведующий принимает материал",
          call("POST", f"/api/umkd/materials/{mid}/review", token=tokens["head"], body={"status": "Accepted"}).status == 200)
    check("после проверки обучающийся видит 1 материал", student_materials() == 1)
    r = call("GET", f"/api/umkd/materials/{mid}/download", token=tokens["student"])
    check("обучающийся скачивает файл без искажений", r.status == 200 and r.body == b"%PDF-1.4 demo")
    r = call("GET", f"/api/syllabi/{sid}/umkd/zip", token=t)
    names = zipfile.ZipFile(io.BytesIO(r.body)).namelist() if r.status == 200 else []
    check("ZIP УМКД: силлабус + материалы + опись", any(n.endswith(".docx") for n in names) and any("lecture1.pdf" in n for n in names) and any("Опись" in n for n in names), names)
    ov = call("GET", "/api/umkd/overview?year=2026-2027", token=tokens["head"]).json()
    check("сводка комплектности УМКД", len(ov["rows"]) >= 2 and len(ov["types"]) == 11)

    print("\n[Копирование и администрирование]")
    r = call("POST", f"/api/syllabi/{sid}/clone", token=t, body={"academicYear": "2027-2028"})
    check("копия на 2027-2028 → черновик", r.status == 201 and call("GET", f"/api/syllabi/{r.json()['id']}", token=t).json()["status"] == "Draft")
    check("копия с неверным годом → 400", call("POST", f"/api/syllabi/{sid}/clone", token=t, body={"academicYear": "2027"}).status == 400)
    adm = tokens["admin"]
    code = "SYS" + uuid.uuid4().hex[:5].upper()
    d = {"id": 0, "code": code, "name": "Системный тест", "cycle": "BD", "credits": 5, "creditStructure": "1/1/1/0", "departmentId": 1}
    check("админ создаёт дисциплину", call("POST", "/api/admin/disciplines", token=adm, body=d).status == 200)
    r = call("POST", "/api/admin/disciplines", token=adm, body=d)
    check("дубль кода дисциплины → 400 с понятным сообщением", r.status == 400 and "уже есть" in r.json()["title"])
    r = call("POST", "/api/admin/users", token=adm, body={"email": f"{code.lower()}@esyllabus.local", "fullName": "Тест Тестов", "roles": ["Teacher"], "departmentId": 1, "password": "1"})
    check("слабый пароль нового пользователя → 400", r.status == 400)
    check("настройки шаблона читаются", call("GET", "/api/admin/settings", token=adm).json()["formCode"] == "Ф КазНИТУ 703-08")

    print("\n[Защита от перебора паролей]")
    statuses = [call("POST", "/api/auth/login", body={"email": "nobody@x.kz", "password": "x"}).status for _ in range(25)]
    check("после 20 попыток в минуту → 429", 429 in statuses, statuses)

    failed = [r for r in results if not r[1]]
    print(f"\nИтого: {len(results) - len(failed)} из {len(results)} проверок пройдено")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
