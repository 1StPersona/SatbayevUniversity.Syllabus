// Сквозной UI-тест на поднятом стеке docker compose.
// Запуск: node e2e/system.e2e.mjs http://localhost:8080 [каталог-для-скриншотов]
// Требуется playwright (npm i -D playwright && npx playwright install chromium) и демо-данные.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const BASE = (process.argv[2] ?? 'http://localhost:8080').replace(/\/$/, '');
const SHOTS = process.argv[3] ?? join(tmpdir(), 'syllabus-e2e');
mkdirSync(SHOTS, { recursive: true });
const PASSWORD = 'Demo123!';
const results = [];
const consoleErrors = [];

function check(name, ok, detail = '') {
  results.push({ name, ok });
  console.log(`${ok ? '  OK  ' : '  FAIL'} ${name}${!ok && detail ? ' — ' + detail : ''}`);
}

async function step(name, fn) {
  try { await fn(); } catch (e) { check(name, false, e.message.split('\n').slice(0, 3).join(' ')); }
}

// UTF-8 локаль нужна headless-Chromium для кириллических имён скачиваемых файлов
const browser = await chromium.launch({ env: { ...process.env, LANG: 'C.UTF-8', LC_ALL: 'C.UTF-8' } });
const ctx = await browser.newContext({ viewport: { width: 1400, height: 900 }, locale: 'ru-RU', acceptDownloads: true });

async function newPage() {
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error' && !/status of 4\d\d/.test(m.text())) consoleErrors.push(m.text()); });
  page.on('pageerror', e => consoleErrors.push(e.message));
  return page;
}

async function login(page, role) {
  await page.goto(`${BASE}/login`);
  await page.evaluate(() => localStorage.clear());
  await page.goto(`${BASE}/login`);
  await page.fill('#email', `${role}@esyllabus.local`);
  await page.fill('#password', PASSWORD);
  await page.click('button[type=submit]');
  await page.waitForURL(u => !u.pathname.startsWith('/login'), { timeout: 15000 });
}

const shot = (page, name) => page.screenshot({ path: join(SHOTS, `${name}.png`), fullPage: false });

let syllabusUrl = '';
const page = await newPage();
console.log(`UI E2E: ${BASE}`);

console.log('\n[Вход и ошибки]');
await step('неверный пароль', async () => {
  await page.goto(`${BASE}/login`);
  await shot(page, '01-login');
  await page.fill('#email', 'teacher@esyllabus.local');
  await page.fill('#password', 'wrong-password');
  await page.click('button[type=submit]');
  await page.waitForSelector('[data-test=login-error]');
  check('неверный пароль → сообщение «Неверный e-mail или пароль»', (await page.textContent('[data-test=login-error]')).includes('Неверный'));
});
await step('защищённый маршрут без входа', async () => {
  await page.evaluate(() => localStorage.clear());
  await page.goto(`${BASE}/syllabi`);
  await page.waitForURL(/\/login\?returnUrl=/);
  check('без входа → редирект на /login с returnUrl', true);
});

console.log('\n[Преподаватель: создание силлабуса в редакторе]');
await step('преподаватель', async () => {
  await login(page, 'teacher');
  await page.waitForSelector('text=Мои силлабусы');
  await shot(page, '02-teacher-dashboard');
  check('рабочий стол преподавателя', true);

  await page.click('text=+ Новый силлабус');
  await page.waitForSelector('#disc');
  const value = await page.$eval('#disc', sel => [...sel.options].find(o => o.text.startsWith('CSE770'))?.value);
  await page.selectOption('#disc', value);
  await page.check('#prog1');
  await page.fill('#goal', 'Сформировать навыки организации научного исследования.');
  await page.fill('#tasks', 'Освоить методы исследования\nНаучиться писать научные тексты');
  await page.fill('textarea[name=desc]', 'Курс знакомит магистрантов с методологией научного исследования.');
  await page.fill('#ok', 'методологию науки');
  await page.fill('#oc', 'планировать исследование');
  await page.fill('#os', 'работы с научными базами');
  // Офис-часы лектора (раздел 1)
  await page.locator('#s-1 input').nth(3).fill('понедельник – пятница, 15:00 – 17:00');
  // Пре- и постреквизиты (раздел 2)
  const addReq = page.locator('#s-2 button:has-text("+ Добавить")');
  await addReq.click();
  await addReq.click();
  const req = page.locator('#s-2 tbody tr');
  await req.nth(0).locator('input').nth(0).fill('CSE100');
  await req.nth(0).locator('input').nth(1).fill('Введение в специальность');
  await req.nth(1).locator('select').selectOption('Postrequisite');
  await req.nth(1).locator('input').nth(0).fill('CSE7942');
  await req.nth(1).locator('input').nth(1).fill('Reserch Project');
  // Темы учебных недель (раздел 6)
  const rows = page.locator('#s-6 tbody tr');
  const n = await rows.count();
  for (let i = 0; i < n; i++) {
    const row = rows.nth(i);
    if (await row.locator('select').inputValue() === 'Week') {
      await row.locator('textarea').nth(0).fill(`Тема лекции ${i + 1}`);
      await row.locator('input').nth(1).fill('[1]');
    }
  }
  // Литература (раздел 7)
  await page.click('text=+ Добавить источник');
  await page.locator('#s-7 tbody textarea').first().fill('Мокий М.С. Методология научных исследований. — М.: Юрайт, 2024.');
  // Рамка компетенций (раздел 8)
  const cells = page.locator('#s-8 textarea');
  for (let i = 0; i < await cells.count(); i++) await cells.nth(i).fill(`Компетенция ${i + 1}`);
  // Подсчёт баллов
  const totals = await page.textContent('[data-test=totals]');
  check('живой подсчёт баллов: 60 / 40 / 100', /60\s*\/60.*40\s*\/40.*100\s*\/100/s.test(totals.replace(/\s+/g, ' ')), totals);
  await page.waitForTimeout(1200);
  await shot(page, '03-editor');
  await page.click('[data-test=save]');
  await page.waitForURL(/\/syllabi\/\d+$/);
  syllabusUrl = page.url();
  await page.waitForSelector('text=Силлабус заполнен полностью');
  check('силлабус сохранён и прошёл проверку по шаблону', true);
  await shot(page, '04-details-draft');

  await page.click('[data-test=wf-Submit]');
  await page.click('[data-test=wf-confirm]');
  await page.waitForSelector('.badge:has-text("На рассмотрении кафедры")');
  check('отправлен на рассмотрение кафедры', true);

  // УМКД: загрузка файла
  await page.click('[data-test=tab-umkd]');
  await page.click('[data-test=umkd-add]');
  await page.fill('#title', 'Лекция 1. Введение');
  await page.setInputFiles('#file', { name: 'lecture1.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 e2e') });
  await page.click('button:has-text("Загрузить")');
  await page.waitForSelector('.alert-success:has-text("загружен")');
  check('материал УМКД загружен через интерфейс', true);
});

console.log('\n[Заведующий кафедрой]');
await step('заведующий', async () => {
  await login(page, 'head');
  await page.waitForSelector('text=Требуют вашего решения');
  check('в «Требуют вашего решения» есть силлабус', await page.isVisible('text=CSE770'));
  await page.goto(syllabusUrl);
  await page.click('[data-test=wf-DepartmentApprove]');
  await page.fill('#pn', '5');
  await page.click('[data-test=wf-confirm]');
  await page.waitForSelector('.badge:has-text("Одобрен кафедрой")');
  check('одобрен кафедрой с протоколом', true);
  await page.click('[data-test=tab-umkd]');
  await page.click('text=Лекционный комплекс');
  await page.click('button:has-text("Принять")');
  await page.waitForSelector('.alert-success:has-text("принят")');
  check('материал УМКД принят', true);
  await shot(page, '05-umkd-head');
  await page.goto(`${BASE}/umkd`);
  await page.waitForSelector('table tbody tr td a');
  await shot(page, '06-umkd-overview');
  check('сводка комплектности УМКД открывается', await page.locator('table tbody tr').count() > 0);
});

console.log('\n[Директор института]');
await step('директор', async () => {
  await login(page, 'director');
  await page.goto(syllabusUrl);
  await page.click('[data-test=wf-Approve]');
  await page.click('[data-test=wf-confirm]');
  await page.waitForSelector('.badge:has-text("Утверждён")');
  check('утверждён директором', true);
  check('гриф «УТВЕРЖДАЮ» заполнен', (await page.textContent('.tp-approve')).includes('Дайнеко Е.А.'));
  await shot(page, '07-approved');

  const [download] = await Promise.all([page.waitForEvent('download'), page.click('text=Скачать DOCX')]);
  check('скачивание DOCX из интерфейса', (await download.suggestedFilename()).endsWith('.docx'));

  const print = await ctx.newPage();
  await print.goto(syllabusUrl + '/print');
  await print.waitForSelector('.tp-title');
  check('печатная форма отображает титульный лист', (await print.textContent('.tp-title')) === 'СИЛЛАБУС');
  await print.pdf?.({ path: join(SHOTS, 'syllabus-print.pdf'), format: 'A4', printBackground: true }).catch(() => {});
  await print.close();
});

console.log('\n[Обучающийся]');
await step('обучающийся', async () => {
  await login(page, 'student');
  await page.waitForSelector('.card-body .h5');
  const cards = await page.locator('.card-body .small.text-muted').allTextContents();
  check('видит утверждённые дисциплины своей ОП', cards.some(t => t.includes('CSE770')) && cards.some(t => t.includes('CSE7942')), cards.join(' | '));
  await shot(page, '08-student');
  await page.goto(syllabusUrl + '?tab=umkd');
  await page.waitForSelector('text=Лекция 1. Введение');
  check('видит принятый материал УМКД', true);
  check('не видит кнопок согласования и редактирования', !(await page.isVisible('text=Редактировать')) && !(await page.isVisible('text=Согласование:')));
  await page.goto(`${BASE}/admin`);
  await page.waitForSelector('text=Недостаточно прав');
  check('/admin для обучающегося → «Недостаточно прав»', true);
  await page.goto(`${BASE}/no-such-page`);
  await page.waitForSelector('text=Страница не найдена');
  check('несуществующая страница → «Страница не найдена»', true);
});

console.log('\n[Администратор]');
await step('администратор', async () => {
  await login(page, 'admin');
  await page.goto(`${BASE}/admin`);
  await page.waitForSelector('text=teacher@esyllabus.local');
  check('список пользователей', true);
  await page.click('button.nav-link:has-text("Дисциплины")');
  await page.waitForSelector('text=CSE7942');
  await shot(page, '09-admin');
  check('справочник дисциплин', true);
});

console.log('\n[Истёкший сеанс]');
await step('истёкший сеанс', async () => {
  await page.evaluate(() => {
    const s = JSON.parse(localStorage.getItem('syllabus.auth'));
    s.token = 'expired.or.revoked';
    localStorage.setItem('syllabus.auth', JSON.stringify(s));
  });
  await page.goto(`${BASE}/syllabi`);
  await page.waitForURL(/\/login/, { timeout: 10000 });
  check('недействительный токен → выход и уведомление «Сеанс истёк»', await page.isVisible('text=Сеанс истёк'));
});

check('нет необработанных JS-ошибок в консоли', consoleErrors.length === 0, consoleErrors.slice(0, 3).join(' | '));

await browser.close();
const failed = results.filter(r => !r.ok);
console.log(`\nИтого: ${results.length - failed.length} из ${results.length} проверок пройдено. Скриншоты: ${SHOTS}`);
writeFileSync(join(SHOTS, 'results.json'), JSON.stringify(results, null, 2));
process.exit(failed.length ? 1 : 0);
