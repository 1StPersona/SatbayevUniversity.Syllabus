import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from '../core/api.service';
import { NotifyService } from '../core/notify.service';
import { AssessmentRow, IssueDto, ReferenceDto, SyllabusEditModel } from '../core/models';
import { fmt } from '../shared/ui';

@Component({
  selector: 'app-syllabus-edit',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
  @if (m(); as d) { @if (ref(); as r) {
    <div class="d-flex flex-wrap justify-content-between align-items-center mb-3 gap-2">
      <div>
        <h1 class="h3 mb-0">{{ isNew() ? 'Новый силлабус' : 'Редактирование силлабуса' }}</h1>
        <div class="small text-muted">Форма {{ r.settings.formCode }}. Разделы 10 (шкала оценок) и 11 (критерии) — общие для всех силлабусов.</div>
      </div>
      @if (!isNew()) { <a class="btn btn-light" [routerLink]="['/syllabi', d.id]">← К силлабусу</a> }
    </div>

    <form (ngSubmit)="save(false)" (input)="touch()" (change)="touch()">
      <div class="row g-4">
        <div class="col-lg-3 d-none d-lg-block">
          <nav class="sticky-top section-nav" style="top:1rem">
            <div class="list-group small">
              @for (sec of sections; track sec[0]) { <a class="list-group-item list-group-item-action" href="" (click)="$event.preventDefault(); scroll(sec[0])">{{ sec[1] }}</a> }
            </div>
            <div class="card mt-3"><div class="card-body small p-2">
              <div class="fw-semibold mb-1">Проверка по шаблону</div>
              @if (issues(); as list) {
                @if (!list.length) { <div class="text-success">Замечаний нет</div> }
                @else {
                  <div><span class="badge text-bg-danger">Ошибок: {{ errorCount() }}</span><span class="badge text-bg-warning ms-1">Замечаний: {{ list.length - errorCount() }}</span></div>
                  <ul class="ps-3 mb-0 mt-1 issue-list">@for (i of list; track $index) { <li [class.text-danger]="i.severity==='error'">{{ i.message }}</li> }</ul>
                }
              } @else { <div class="text-muted">…</div> }
            </div></div>
            <div class="d-grid gap-2 mt-3">
              <button type="submit" class="btn btn-primary" [disabled]="busy()" data-test="save">Сохранить</button>
              @if (!isNew()) { <button type="button" class="btn btn-outline-primary" [disabled]="busy()" (click)="save(true)">Сохранить и продолжить</button> }
            </div>
          </nav>
        </div>

        <div class="col-lg-9">
          <section class="card mb-4" id="s-title">
            <div class="card-header bg-white fw-semibold">Титульный лист</div>
            <div class="card-body row g-3">
              <div class="col-md-8"><label class="form-label" for="disc">Дисциплина</label>
                <select class="form-select" id="disc" name="disc" [(ngModel)]="d.disciplineId" required>
                  <option [ngValue]="0" disabled>— выберите —</option>
                  @for (x of r.disciplines; track x.id) { <option [ngValue]="x.id">{{ x.code }} {{ x.name }} — {{ x.credits }} кр.</option> }
                </select>
                <div class="form-text">Код, название, цикл и кредиты берутся из справочника дисциплин.</div></div>
              <div class="col-md-4"><label class="form-label" for="year">Учебный год</label>
                <input class="form-control" id="year" name="year" [(ngModel)]="d.academicYear" pattern="\\d{4}-\\d{4}" placeholder="2026-2027" required /></div>
              <div class="col-6 col-md-2"><label class="form-label" for="course">Курс</label><input class="form-control" type="number" min="1" max="6" id="course" name="course" [(ngModel)]="d.course" /></div>
              <div class="col-6 col-md-2"><label class="form-label" for="sem">Семестр</label><input class="form-control" type="number" min="1" max="12" id="sem" name="sem" [(ngModel)]="d.semester" /></div>
              <div class="col-md-3"><label class="form-label" for="sf">Форма обучения</label><input class="form-control" id="sf" name="sf" [(ngModel)]="d.studyForm" list="studyForms" /></div>
              <datalist id="studyForms"><option value="очное"></option><option value="очное (с применением ДОТ)"></option><option value="заочное"></option></datalist>
              <div class="col-md-3"><label class="form-label" for="lang">Язык обучения</label><input class="form-control" id="lang" name="lang" [(ngModel)]="d.language" list="langs" /></div>
              <datalist id="langs"><option value="русский"></option><option value="казахский"></option><option value="английский"></option></datalist>
              <div class="col-md-2"><label class="form-label" for="city">Город</label><input class="form-control" id="city" name="city" [(ngModel)]="d.city" /></div>
              <div class="col-12"><label class="form-label">Образовательные программы</label>
                <div class="row">@for (p of r.programs; track p.id) {
                  <div class="col-md-6"><div class="form-check">
                    <input class="form-check-input" type="checkbox" [id]="'prog' + p.id" [checked]="d.programIds.includes(p.id)" (change)="toggleProgram(p.id)" />
                    <label class="form-check-label" [for]="'prog' + p.id">{{ p.code }} «{{ p.name }}»</label>
                  </div></div>
                }</div></div>
            </div>
          </section>

          <section class="card mb-4" id="s-1">
            <div class="card-header bg-white fw-semibold">1. Информация о преподавателе</div>
            <div class="card-body">
              @for (t of teacherBlocks(); track t.key) {
                <h3 class="h6 mt-2">{{ t.title }}</h3>
                <div class="row g-2 mb-3">
                  <div class="col-md-6"><label class="form-label small">ФИО полностью</label><input class="form-control" [name]="t.key + 'fn'" [(ngModel)]="t.info.fullName" /></div>
                  <div class="col-md-6"><label class="form-label small">Степень, звание, должность</label><input class="form-control" [name]="t.key + 'rg'" [(ngModel)]="t.info.regalia" /></div>
                  <div class="col-md-4"><label class="form-label small">Офис</label><input class="form-control" [name]="t.key + 'of'" [(ngModel)]="t.info.office" placeholder="ККЦ, 401 кабинет" /></div>
                  <div class="col-md-4"><label class="form-label small">Офис-часы</label><input class="form-control" [name]="t.key + 'oh'" [(ngModel)]="t.info.officeHours" placeholder="понедельник – пятница, 15:00 – 17:00" /></div>
                  <div class="col-md-4"><label class="form-label small">E-mail</label><input class="form-control" type="email" [name]="t.key + 'em'" [(ngModel)]="t.info.email" /></div>
                </div>
              }
              <button type="button" class="btn btn-sm btn-outline-secondary" (click)="copyLecturer()">Скопировать данные лектора в п. 1.2</button>
            </div>
          </section>

          <section class="card mb-4" id="s-2">
            <div class="card-header bg-white fw-semibold">2. Пререквизиты и постреквизиты</div>
            <div class="card-body">
              <table class="table table-sm align-middle"><thead><tr><th style="width:170px">Тип</th><th style="width:130px">Код</th><th>Название дисциплины</th><th></th></tr></thead>
                <tbody>@for (rq of d.requisites; track $index; let i = $index) {
                  <tr>
                    <td><select class="form-select form-select-sm" [name]="'rqk' + i" [(ngModel)]="rq.kind"><option value="Prerequisite">Пререквизит</option><option value="Postrequisite">Постреквизит</option></select></td>
                    <td><input class="form-control form-control-sm" [name]="'rqc' + i" [(ngModel)]="rq.code" /></td>
                    <td><input class="form-control form-control-sm" [name]="'rqn' + i" [(ngModel)]="rq.name" /></td>
                    <td><button type="button" class="btn btn-sm btn-link text-danger" (click)="removeAt(d.requisites, i)" title="Удалить">✕</button></td>
                  </tr>
                }</tbody></table>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="d.requisites.push({ kind: 'Prerequisite', code: '', name: '' }); touch()">+ Добавить</button>
            </div>
          </section>

          <section class="card mb-4" id="s-3">
            <div class="card-header bg-white fw-semibold">3. Цель и задачи дисциплины</div>
            <div class="card-body">
              <label class="form-label" for="goal">Цель</label><textarea class="form-control mb-3" id="goal" name="goal" rows="4" [(ngModel)]="d.goal"></textarea>
              <label class="form-label" for="tasks">Задачи</label><textarea class="form-control" id="tasks" name="tasks" rows="8" [(ngModel)]="d.tasks"></textarea>
              <div class="form-text">Каждая задача — с новой строки. Тире в начале добавляется автоматически.</div>
            </div>
          </section>
          <section class="card mb-4" id="s-4">
            <div class="card-header bg-white fw-semibold">4. Описание курса</div>
            <div class="card-body"><textarea class="form-control" name="desc" rows="10" [(ngModel)]="d.description" aria-label="Описание курса"></textarea>
              <div class="form-text">Абзацы разделяются переводом строки.</div></div>
          </section>
          <section class="card mb-4" id="s-5">
            <div class="card-header bg-white fw-semibold">5. Результаты обучения</div>
            <div class="card-body">
              <p class="small text-muted mb-2">После завершения курса обучающийся должен… (каждый результат — с новой строки)</p>
              <label class="form-label fw-semibold" for="ok">Знать</label><textarea class="form-control mb-3" id="ok" name="ok" rows="5" [(ngModel)]="d.outcomesKnow"></textarea>
              <label class="form-label fw-semibold" for="oc">Уметь</label><textarea class="form-control mb-3" id="oc" name="oc" rows="5" [(ngModel)]="d.outcomesCan"></textarea>
              <label class="form-label fw-semibold" for="os">Владеть навыками</label><textarea class="form-control" id="os" name="os" rows="5" [(ngModel)]="d.outcomesSkills"></textarea>
            </div>
          </section>

          <section class="card mb-4" id="s-6">
            <div class="card-header bg-white fw-semibold d-flex justify-content-between"><span>6. Календарно-тематический план</span><span class="small text-muted">ссылки на литературу — в формате [1], [7]</span></div>
            <div class="card-body">
              <div class="table-responsive"><table class="table table-sm align-top calendar-edit">
                <thead><tr><th style="width:110px">Тип</th><th style="width:70px">Неделя</th><th>Тема лекции</th><th>Тема практической работы</th><th>СРО/СРОП</th><th style="width:100px">Литература</th><th style="width:110px">Срок сдачи</th><th></th></tr></thead>
                <tbody>@for (row of d.calendarRows; track $index; let i = $index) {
                  <tr [class.table-light]="row.kind === 'Milestone'">
                    <td><select class="form-select form-select-sm" [name]="'ck' + i" [(ngModel)]="row.kind"><option value="Week">Неделя</option><option value="Milestone">Контроль</option></select></td>
                    <td><input class="form-control form-control-sm" type="number" min="1" max="20" [name]="'cw' + i" [(ngModel)]="row.week" [disabled]="row.kind === 'Milestone'" /></td>
                    <td><textarea class="form-control form-control-sm" rows="2" [name]="'cl' + i" [(ngModel)]="row.lectureTopic"></textarea></td>
                    <td><textarea class="form-control form-control-sm" rows="2" [name]="'cp' + i" [(ngModel)]="row.practiceTopic" [disabled]="row.kind === 'Milestone'"></textarea></td>
                    <td><textarea class="form-control form-control-sm" rows="2" [name]="'cs' + i" [(ngModel)]="row.sroTopic" [disabled]="row.kind === 'Milestone'"></textarea></td>
                    <td><input class="form-control form-control-sm" [name]="'cr' + i" [(ngModel)]="row.literatureRefs" /></td>
                    <td><input class="form-control form-control-sm" [name]="'cd' + i" [(ngModel)]="row.deadline" /></td>
                    <td class="text-nowrap">
                      <button type="button" class="btn btn-sm btn-link px-1" (click)="move(d.calendarRows, i, -1)" title="Выше">↑</button><button type="button" class="btn btn-sm btn-link px-1" (click)="move(d.calendarRows, i, 1)" title="Ниже">↓</button><button type="button" class="btn btn-sm btn-link text-danger px-1" (click)="removeAt(d.calendarRows, i)" title="Удалить">✕</button>
                    </td>
                  </tr>
                }</tbody></table></div>
              <button type="button" class="btn btn-sm btn-outline-primary me-2" (click)="addWeek()">+ Учебная неделя</button>
              <button type="button" class="btn btn-sm btn-outline-secondary" (click)="d.calendarRows.push({ kind: 'Milestone', lectureTopic: '', deadline: '' }); touch()">+ Контрольная точка</button>
              <div class="form-text">«Контроль» — строка на всю ширину (промежуточная аттестация, итоговый экзамен): текст берётся из поля «Тема лекции».</div>
            </div>
          </section>

          <section class="card mb-4" id="s-7">
            <div class="card-header bg-white fw-semibold">7. Литература</div>
            <div class="card-body">
              <table class="table table-sm align-top"><thead><tr><th style="width:80px">№</th><th style="width:170px">Вид</th><th>Библиографическое описание</th><th></th></tr></thead>
                <tbody>@for (l of d.literature; track $index; let i = $index) {
                  <tr>
                    <td><input class="form-control form-control-sm" type="number" min="1" [name]="'ln' + i" [(ngModel)]="l.number" /></td>
                    <td><select class="form-select form-select-sm" [name]="'lk' + i" [(ngModel)]="l.kind"><option value="Main">Основная</option><option value="Additional">Дополнительная</option></select></td>
                    <td><textarea class="form-control form-control-sm" rows="2" [name]="'lt' + i" [(ngModel)]="l.text"></textarea></td>
                    <td><button type="button" class="btn btn-sm btn-link text-danger" (click)="removeAt(d.literature, i)">✕</button></td>
                  </tr>
                }</tbody></table>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addLiterature()">+ Добавить источник</button>
            </div>
          </section>

          <section class="card mb-4" id="s-8">
            <div class="card-header bg-white fw-semibold">8. Рамка компетенций</div>
            <div class="card-body"><div class="table-responsive">
              <table class="table table-sm table-bordered align-top competency-edit">
                <thead class="table-light"><tr><th>Дескрипторы</th>@for (c of r.competencyCategories; track c) { <th class="small">{{ c }}</th> }</tr></thead>
                <tbody>@for (desc of r.competencyDescriptors; track desc; let di = $index) {
                  <tr><th class="small table-light">{{ desc }}</th>
                    @for (c of r.competencyCategories; track c; let ci = $index) {
                      <td><textarea class="form-control form-control-sm" rows="4" [name]="'cmp' + (di * r.competencyCategories.length + ci)" [(ngModel)]="d.competencies[di * r.competencyCategories.length + ci]" [attr.aria-label]="desc + ' / ' + c"></textarea></td>
                    }</tr>
                }</tbody>
              </table>
            </div></div>
          </section>

          <section class="card mb-4" id="s-9">
            <div class="card-header bg-white fw-semibold d-flex flex-wrap justify-content-between gap-2">
              <span>9. График сдачи требуемых работ</span>
              <span class="small" data-test="totals">Рейтинг: <strong [class]="totals().rating === 60 ? 'text-success' : 'text-danger'">{{ f.num(totals().rating) }}</strong>/60 ·
                Итоговый: <strong [class]="totals().final === 40 ? 'text-success' : 'text-danger'">{{ f.num(totals().final) }}</strong>/40 ·
                Всего: <strong [class]="totals().rating + totals().final === 100 ? 'text-success' : 'text-danger'">{{ f.num(totals().rating + totals().final) }}</strong>/100</span>
            </div>
            <div class="card-body">
              <div class="d-flex align-items-end gap-2 mb-2"><div><label class="form-label small mb-0" for="wc">Количество недель</label>
                <input class="form-control form-control-sm" style="width:90px" type="number" min="1" max="20" id="wc" name="wc" [ngModel]="d.weeksCount" (ngModelChange)="setWeeks($event)" /></div></div>
              <div class="table-responsive"><table class="table table-sm table-bordered align-middle assessment-edit">
                <thead class="table-light"><tr><th style="min-width:200px">Вид контроля</th><th style="width:60px">Макс</th><th title="Итоговый контроль">Итог.</th>
                  @for (w of weekIdx(); track w) { <th class="text-center">{{ w + 1 }}</th> }<th>Σ</th><th></th></tr></thead>
                <tbody>@for (a of d.assessments; track $index; let i = $index) {
                  <tr>
                    <td><input class="form-control form-control-sm" [name]="'an' + i" [(ngModel)]="a.name" /></td>
                    <td><input class="form-control form-control-sm" [name]="'am' + i" [(ngModel)]="a.maxPerWeek" /></td>
                    <td class="text-center"><input class="form-check-input" type="checkbox" [name]="'af' + i" [(ngModel)]="a.isFinal" /></td>
                    @for (w of weekIdx(); track w) {
                      <td><input class="form-control form-control-sm pts" type="number" step="0.5" min="0" [name]="'aw' + i + '_' + w" [(ngModel)]="a.weeks[w]" [disabled]="a.isFinal" /></td>
                    }
                    <td>@if (a.isFinal) { <input class="form-control form-control-sm pts" type="number" step="0.5" min="0" [name]="'ap' + i" [(ngModel)]="a.finalPoints" /> }
                        @else { <span class="small">{{ f.num(rowSum(a)) }}</span> }</td>
                    <td><button type="button" class="btn btn-sm btn-link text-danger" (click)="removeAt(d.assessments, i)">✕</button></td>
                  </tr>
                }</tbody>
                <tfoot><tr class="table-light"><th colspan="3" class="text-end">Итого по неделям</th>@for (w of weekIdx(); track w) { <th class="text-center small">{{ f.num(weekSum(w)) || '' }}</th> }<th></th><th></th></tr></tfoot>
              </table></div>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addAssessment()">+ Добавить вид контроля</button>
              <div class="form-text">Для итогового контроля отметьте «Итог.» и укажите баллы в колонке Σ. Рейтинг допуска — 60 баллов, итоговый контроль — 40.</div>
            </div>
          </section>

          <section class="card mb-4" id="s-11">
            <div class="card-header bg-white fw-semibold">11–13. Критерии оценивания и политики курса</div>
            <div class="card-body">
              <div class="alert alert-light border small">Разделы 10 «Оценивание итогового контроля» и 11 «Критерии оценивания» (А, Т, З, О) включаются в силлабус автоматически по стандарту университета.</div>
              <label class="form-label" for="cn">Дополнение к критериям оценивания</label><textarea class="form-control mb-3" id="cn" name="cn" rows="3" [(ngModel)]="d.criteriaNote" placeholder="Необязательно"></textarea>
              <label class="form-label" for="lp">Политика поздней сдачи работ</label><textarea class="form-control mb-3" id="lp" name="lp" rows="6" [(ngModel)]="d.latePolicy"></textarea>
              <label class="form-label" for="ap">Политика академического поведения и этики</label><textarea class="form-control" id="ap" name="ap" rows="10" [(ngModel)]="d.academicPolicy"></textarea>
            </div>
          </section>

          <section class="card mb-4" id="s-sign">
            <div class="card-header bg-white fw-semibold">Лист подписей</div>
            <div class="card-body row g-3">
              <div class="col-md-4"><label class="form-label" for="ht">Должность в подписи</label><input class="form-control" id="ht" name="ht" [(ngModel)]="d.headOfDepartmentTitle" list="headTitles" /></div>
              <datalist id="headTitles"><option value="Зав. кафедрой"></option><option value="И.о зав. кафедрой"></option></datalist>
              <div class="col-md-4"><label class="form-label" for="hn">Заведующий кафедрой (ФИО)</label><input class="form-control" id="hn" name="hn" [(ngModel)]="d.headOfDepartmentName" placeholder="Фамилия И.О." /></div>
              <div class="col-md-4"><label class="form-label" for="cmpn">Составитель (ФИО)</label><input class="form-control" id="cmpn" name="cmpn" [(ngModel)]="d.compilerName" placeholder="Фамилия И.О." /></div>
              <div class="col-md-6"><label class="form-label" for="ag">Согласовано (по одному на строку)</label><textarea class="form-control" id="ag" name="ag" rows="3" [(ngModel)]="d.agreedBy"></textarea></div>
              <div class="col-md-6 small text-muted">Номер и дата протокола заседания кафедры, а также гриф «УТВЕРЖДАЮ» заполняются автоматически при согласовании.</div>
            </div>
          </section>

          <div class="d-flex gap-2 mb-5">
            <button type="submit" class="btn btn-primary" [disabled]="busy()">Сохранить</button>
            @if (!isNew()) { <button type="button" class="btn btn-outline-primary" [disabled]="busy()" (click)="save(true)">Сохранить и продолжить</button> }
          </div>
        </div>
      </div>
    </form>
  } } @else { <div class="text-muted">Загрузка…</div> }`
})
export class SyllabusEditComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private notify = inject(NotifyService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  f = fmt;

  m = signal<SyllabusEditModel | null>(null);
  ref = signal<ReferenceDto | null>(null);
  issues = signal<IssueDto[] | null>(null);
  busy = signal(false);
  version = signal(0);
  dirty = false;
  private validateTimer?: ReturnType<typeof setTimeout>;
  private beforeUnload = (e: BeforeUnloadEvent) => { if (this.dirty) { e.preventDefault(); e.returnValue = ''; } };

  sections = [
    ['s-title', 'Титульный лист'], ['s-1', '1. Преподаватели'], ['s-2', '2. Пре- и постреквизиты'], ['s-3', '3. Цель и задачи'],
    ['s-4', '4. Описание курса'], ['s-5', '5. Результаты обучения'], ['s-6', '6. Календарный план'], ['s-7', '7. Литература'],
    ['s-8', '8. Рамка компетенций'], ['s-9', '9. График работ'], ['s-11', '11–13. Критерии и политики'], ['s-sign', 'Подписи']
  ];

  isNew = computed(() => !this.m()?.id);
  weekIdx = computed(() => { this.version(); return Array.from({ length: this.m()?.weeksCount ?? 0 }, (_, i) => i); });
  errorCount = computed(() => this.issues()?.filter(i => i.severity === 'error').length ?? 0);
  teacherBlocks = computed(() => {
    const d = this.m();
    return d ? [
      { key: 'lec', title: '1.1 Лектор', info: d.lecturer },
      { key: 'pr', title: '1.2 Преподаватель, ведущий практические/лабораторные занятия', info: d.practiceTeacher }
    ] : [];
  });
  totals = computed(() => {
    this.version();
    const list = this.m()?.assessments ?? [];
    return {
      rating: list.filter(a => !a.isFinal).reduce((s, a) => s + this.rowSum(a), 0),
      final: list.filter(a => a.isFinal).reduce((s, a) => s + (Number(a.finalPoints) || 0), 0)
    };
  });

  async ngOnInit() {
    window.addEventListener('beforeunload', this.beforeUnload);
    this.ref.set(await this.api.reference());
    const id = this.route.snapshot.paramMap.get('id');
    let model: SyllabusEditModel;
    if (id) {
      const s = await this.api.syllabus(+id);
      if (!s.permissions.canEdit) {
        this.notify.warning('Силлабус недоступен для редактирования в текущем статусе.');
        this.router.navigate(['/syllabi', id]);
        return;
      }
      model = s.data;
    } else {
      const disc = this.route.snapshot.queryParamMap.get('disciplineId');
      model = await this.api.template(disc ? +disc : undefined);
    }
    model.competencies = Array.from({ length: 25 }, (_, i) => model.competencies[i] ?? '');
    model.assessments.forEach(a => a.weeks = Array.from({ length: model.weeksCount }, (_, i) => a.weeks[i] ?? null));
    this.m.set(model);
    this.validateSoon(0);
  }

  ngOnDestroy() {
    window.removeEventListener('beforeunload', this.beforeUnload);
    clearTimeout(this.validateTimer);
  }

  canDeactivate() { return !this.dirty || confirm('Есть несохранённые изменения. Уйти со страницы?'); }

  touch() { this.dirty = true; this.version.update(v => v + 1); this.validateSoon(); }

  rowSum(a: AssessmentRow) { return a.weeks.reduce<number>((s, v) => s + (Number(v) || 0), 0); }
  weekSum(w: number) {
    this.version();
    return (this.m()?.assessments ?? []).filter(a => !a.isFinal).reduce((s, a) => s + (Number(a.weeks[w]) || 0), 0);
  }

  toggleProgram(id: number) {
    const d = this.m()!;
    d.programIds = d.programIds.includes(id) ? d.programIds.filter(x => x !== id) : [...d.programIds, id];
    this.touch();
  }
  copyLecturer() { const d = this.m()!; d.practiceTeacher = { ...d.lecturer }; this.m.set({ ...d }); this.touch(); }
  removeAt<T>(list: T[], i: number) { list.splice(i, 1); this.touch(); }
  move<T>(list: T[], i: number, dir: number) {
    const j = i + dir;
    if (j < 0 || j >= list.length) return;
    [list[i], list[j]] = [list[j], list[i]];
    this.touch();
  }
  addWeek() {
    const d = this.m()!;
    const max = Math.max(0, ...d.calendarRows.filter(r => r.kind === 'Week').map(r => r.week ?? 0));
    d.calendarRows.push({ kind: 'Week', week: max + 1, deadline: `Неделя ${max + 1}` });
    this.touch();
  }
  addLiterature() {
    const d = this.m()!;
    d.literature.push({ number: Math.max(0, ...d.literature.map(l => l.number)) + 1, kind: 'Main', text: '' });
    this.touch();
  }
  addAssessment() {
    const d = this.m()!;
    d.assessments.push({ name: '', maxPerWeek: '', isFinal: false, finalPoints: null, weeks: Array(d.weeksCount).fill(null) });
    this.touch();
  }
  setWeeks(n: number) {
    const d = this.m()!;
    const count = Math.min(20, Math.max(1, Number(n) || 1));
    d.weeksCount = count;
    d.assessments.forEach(a => a.weeks = Array.from({ length: count }, (_, i) => a.weeks[i] ?? null));
    this.touch();
  }
  scroll(id: string) { document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' }); }

  private validateSoon(delay = 800) {
    clearTimeout(this.validateTimer);
    this.validateTimer = setTimeout(async () => {
      const d = this.m();
      if (!d) return;
      try { this.issues.set(await this.api.validate(this.normalized(d))); } catch { /* не критично */ }
    }, delay);
  }

  /** Числовые поля из input type=number могут прийти строкой или пустой строкой. */
  private normalized(d: SyllabusEditModel): SyllabusEditModel {
    const n = (v: unknown) => v === '' || v === null || v === undefined || isNaN(Number(v)) ? null : Number(v);
    return {
      ...d,
      course: Number(d.course) || 1, semester: Number(d.semester) || 1,
      calendarRows: d.calendarRows.map(r => ({ ...r, week: r.kind === 'Week' ? n(r.week) : null })),
      literature: d.literature.map(l => ({ ...l, number: Number(l.number) || 0 })),
      assessments: d.assessments.map(a => ({ ...a, finalPoints: n(a.finalPoints), weeks: a.weeks.map(n) }))
    };
  }

  async save(stay: boolean) {
    const d = this.m();
    if (!d) return;
    if (!d.disciplineId) { this.notify.warning('Выберите дисциплину.'); this.scroll('s-title'); return; }
    if (!/^\d{4}-\d{4}$/.test(d.academicYear.trim())) { this.notify.warning('Учебный год указывается в формате 2026-2027.'); this.scroll('s-title'); return; }
    this.busy.set(true);
    try {
      const body = this.normalized(d);
      if (this.isNew()) {
        const res = await this.api.create(body);
        this.dirty = false;
        this.notify.success('Силлабус создан. Проверьте замечания и отправьте его на рассмотрение кафедры.');
        this.router.navigate(['/syllabi', res.id]);
      } else {
        await this.api.update(d.id, body);
        this.dirty = false;
        this.notify.success('Изменения сохранены.');
        if (!stay) this.router.navigate(['/syllabi', d.id]);
        else this.validateSoon(0);
      }
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 403) this.router.navigate(['/syllabi', d.id]);
    } finally { this.busy.set(false); }
  }
}
