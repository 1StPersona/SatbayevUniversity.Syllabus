import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { NotifyService } from '../core/notify.service';
import {
  AdminUserDto, CYCLE_TITLES, DepartmentDto, DisciplineCycle, DisciplineDto, InstituteDto, LEVEL_TITLES, ProgramDto,
  ROLE_TITLES, ROLES, SaveUserRequest, SettingsDto, StudyLevel, UmkdTypeDto
} from '../core/models';
import { ModalComponent } from '../shared/ui';

type Tab = 'users' | 'structure' | 'programs' | 'disciplines' | 'umkd' | 'settings';

/** Администрирование: пользователи, оргструктура, ОП, дисциплины, компоненты УМКД, настройки шаблона. */
@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [FormsModule, ModalComponent],
  template: `
  <h1 class="h3 mb-3">Администрирование</h1>
  <ul class="nav nav-pills mb-4 flex-wrap">
    @for (t of tabs; track t[0]) { <li class="nav-item"><button class="nav-link" [class.active]="tab() === t[0]" (click)="tab.set(t[0])">{{ t[1] }}</button></li> }
  </ul>

  @switch (tab()) {
    @case ('users') {
      <div class="mb-3"><button class="btn btn-primary btn-sm" (click)="editUser(null)">+ Пользователь</button></div>
      <div class="card"><div class="table-responsive"><table class="table table-hover mb-0 align-middle">
        <thead class="table-light"><tr><th>ФИО</th><th>E-mail</th><th>Роли</th><th>Кафедра / ОП</th><th></th></tr></thead>
        <tbody>@for (u of users(); track u.id) {
          <tr><td>{{ u.fullName }}<div class="small text-muted">{{ u.regalia }}</div></td><td>{{ u.email }}</td>
            <td>@for (r of u.roles; track r) { <span class="badge text-bg-light border me-1">{{ roleTitles[r] }}</span> }</td>
            <td class="small">{{ u.departmentName }} {{ u.programCode }}</td>
            <td class="text-end"><button class="btn btn-sm btn-link" (click)="editUser(u)">Изменить</button></td></tr>
        }</tbody></table></div></div>
    }
    @case ('structure') {
      <div class="mb-3 d-flex gap-2">
        <button class="btn btn-primary btn-sm" (click)="edit('institutes', { id: 0, name: '', shortName: '', directorName: '' })">+ Институт</button>
        <button class="btn btn-outline-primary btn-sm" (click)="edit('departments', { id: 0, name: '', headName: '', headTitle: 'Зав. кафедрой', instituteId: institutes()[0]?.id ?? 0 })">+ Кафедра</button>
      </div>
      @for (i of institutes(); track i.id) {
        <div class="card mb-3">
          <div class="card-header bg-white d-flex justify-content-between align-items-center">
            <div><span class="fw-semibold">{{ i.name }}</span><span class="text-muted ms-1">({{ i.shortName }})</span> · директор: {{ i.directorName }}</div>
            <button class="btn btn-sm btn-link" (click)="edit('institutes', i)">Изменить</button>
          </div>
          <ul class="list-group list-group-flush">@for (d of deptsOf(i.id); track d.id) {
            <li class="list-group-item d-flex justify-content-between"><span>Кафедра «{{ d.name }}» · {{ d.headTitle }}: {{ d.headName }}</span>
              <button class="btn btn-sm btn-link" (click)="edit('departments', d)">Изменить</button></li>
          }</ul>
        </div>
      }
    }
    @case ('programs') {
      <div class="mb-3"><button class="btn btn-primary btn-sm" (click)="edit('programs', { id: 0, code: '', name: '', level: 'Master', departmentId: departments()[0]?.id ?? 0 })">+ Программа</button></div>
      <div class="card"><table class="table table-hover mb-0"><thead class="table-light"><tr><th>Шифр</th><th>Название</th><th>Уровень</th><th>Кафедра</th><th></th></tr></thead>
        <tbody>@for (p of programs(); track p.id) {
          <tr><td>{{ p.code }}</td><td>{{ p.name }}</td><td>{{ levels[p.level] }}</td><td>{{ deptName(p.departmentId) }}</td><td class="text-end"><button class="btn btn-sm btn-link" (click)="edit('programs', p)">Изменить</button></td></tr>
        }</tbody></table></div>
    }
    @case ('disciplines') {
      <div class="mb-3"><button class="btn btn-primary btn-sm" (click)="edit('disciplines', { id: 0, code: '', name: '', cycle: 'PD', credits: 5, creditStructure: '', departmentId: departments()[0]?.id ?? 0 })">+ Дисциплина</button></div>
      <div class="card"><table class="table table-hover mb-0"><thead class="table-light"><tr><th>Код</th><th>Название</th><th>Цикл</th><th>Кредиты</th><th>Кафедра</th><th></th></tr></thead>
        <tbody>@for (d of disciplines(); track d.id) {
          <tr><td>{{ d.code }}</td><td>{{ d.name }}</td><td>{{ cycles[d.cycle] }}</td><td>{{ d.credits }} ({{ d.creditStructure }})</td><td>{{ d.departmentName }}</td><td class="text-end"><button class="btn btn-sm btn-link" (click)="edit('disciplines', d)">Изменить</button></td></tr>
        }</tbody></table></div>
    }
    @case ('umkd') {
      <div class="mb-3"><button class="btn btn-primary btn-sm" (click)="edit('umkd-types', { id: 0, name: '', description: '', isRequired: true, order: umkdTypes().length + 1, isSyllabus: false, visibleToStudentsByDefault: true })">+ Компонент</button></div>
      <div class="card"><table class="table table-hover mb-0"><thead class="table-light"><tr><th>№</th><th>Компонент</th><th>Обязательный</th><th>Видимость</th><th></th></tr></thead>
        <tbody>@for (t of umkdTypes(); track t.id) {
          <tr><td>{{ t.order }}</td><td>{{ t.name }}<div class="small text-muted">{{ t.description }}</div></td><td>{{ t.isRequired ? 'да' : 'нет' }}{{ t.isSyllabus ? ' (силлабус)' : '' }}</td>
            <td>{{ t.visibleToStudentsByDefault ? 'видно обучающимся' : 'скрыто' }}</td><td class="text-end"><button class="btn btn-sm btn-link" (click)="edit('umkd-types', t)">Изменить</button></td></tr>
        }</tbody></table></div>
    }
    @case ('settings') {
      @if (settings(); as s) {
        <form class="card card-body" (ngSubmit)="saveSettings(s)">
          <div class="mb-3"><label class="form-label" for="un">Полное название (верхний колонтитул)</label><input class="form-control" id="un" name="un" [(ngModel)]="s.universityName" /></div>
          <div class="row g-2 mb-3">
            <div class="col-md-6"><label class="form-label" for="us">Краткое название (титульный лист)</label><input class="form-control" id="us" name="us" [(ngModel)]="s.universityShortName" /></div>
            <div class="col-md-6"><label class="form-label" for="fc">Код формы (нижний колонтитул)</label><input class="form-control" id="fc" name="fc" [(ngModel)]="s.formCode" /></div>
          </div>
          <div class="mb-3"><label class="form-label" for="dc">Типовое дополнение к критериям</label><textarea class="form-control" id="dc" name="dc" rows="2" [(ngModel)]="s.defaultCriteriaNote"></textarea></div>
          <div class="mb-3"><label class="form-label" for="dl">Типовая политика поздней сдачи работ</label><textarea class="form-control" id="dl" name="dl" rows="5" [(ngModel)]="s.defaultLatePolicy"></textarea></div>
          <div class="mb-3"><label class="form-label" for="da">Типовая политика академического поведения</label><textarea class="form-control" id="da" name="da" rows="8" [(ngModel)]="s.defaultAcademicPolicy"></textarea></div>
          <div class="mb-3"><label class="form-label" for="aq">Отметка об ознакомлении</label><textarea class="form-control" id="aq" name="aq" rows="2" [(ngModel)]="s.acquaintanceNote"></textarea></div>
          <div><button class="btn btn-primary">Сохранить</button></div>
        </form>
      }
    }
  }

  @if (editing(); as e) {
    <app-modal [title]="(e.item.id ? 'Изменить: ' : 'Создать: ') + kindTitles[e.kind]" (closed)="editing.set(null)">
      <form (ngSubmit)="saveItem()">
        <div class="modal-body">
          @switch (e.kind) {
            @case ('institutes') {
              <div class="mb-2"><label class="form-label" for="f1">Полное название</label><input class="form-control" id="f1" name="f1" [(ngModel)]="e.item.name" required /></div>
              <div class="mb-2"><label class="form-label" for="f2">Краткое название</label><input class="form-control" id="f2" name="f2" [(ngModel)]="e.item.shortName" /></div>
              <div class="mb-2"><label class="form-label" for="f3">ФИО директора</label><input class="form-control" id="f3" name="f3" [(ngModel)]="e.item.directorName" /></div>
            }
            @case ('departments') {
              <div class="mb-2"><label class="form-label" for="f4">Институт</label><select class="form-select" id="f4" name="f4" [(ngModel)]="e.item.instituteId">@for (i of institutes(); track i.id) { <option [ngValue]="i.id">{{ i.name }}</option> }</select></div>
              <div class="mb-2"><label class="form-label" for="f5">Название кафедры</label><input class="form-control" id="f5" name="f5" [(ngModel)]="e.item.name" required /></div>
              <div class="mb-2"><label class="form-label" for="f6">Должность в подписи</label><input class="form-control" id="f6" name="f6" [(ngModel)]="e.item.headTitle" /></div>
              <div class="mb-2"><label class="form-label" for="f7">Заведующий (Фамилия И.О.)</label><input class="form-control" id="f7" name="f7" [(ngModel)]="e.item.headName" /></div>
            }
            @case ('programs') {
              <div class="mb-2"><label class="form-label" for="f8">Шифр</label><input class="form-control" id="f8" name="f8" [(ngModel)]="e.item.code" required /></div>
              <div class="mb-2"><label class="form-label" for="f9">Название</label><input class="form-control" id="f9" name="f9" [(ngModel)]="e.item.name" required /></div>
              <div class="mb-2"><label class="form-label" for="f10">Уровень</label><select class="form-select" id="f10" name="f10" [(ngModel)]="e.item.level">@for (l of levelKeys; track l) { <option [value]="l">{{ levels[l] }}</option> }</select></div>
              <div class="mb-2"><label class="form-label" for="f11">Кафедра</label><select class="form-select" id="f11" name="f11" [(ngModel)]="e.item.departmentId">@for (d of departments(); track d.id) { <option [ngValue]="d.id">{{ d.name }}</option> }</select></div>
            }
            @case ('disciplines') {
              <div class="row g-2 mb-2"><div class="col-4"><label class="form-label" for="f12">Код</label><input class="form-control" id="f12" name="f12" [(ngModel)]="e.item.code" required /></div>
                <div class="col-8"><label class="form-label" for="f13">Название</label><input class="form-control" id="f13" name="f13" [(ngModel)]="e.item.name" required /></div></div>
              <div class="row g-2 mb-2"><div class="col-4"><label class="form-label" for="f14">Цикл</label><select class="form-select" id="f14" name="f14" [(ngModel)]="e.item.cycle">@for (c of cycleKeys; track c) { <option [value]="c">{{ cycles[c] }}</option> }</select></div>
                <div class="col-4"><label class="form-label" for="f15">Кредиты</label><input class="form-control" type="number" min="1" max="30" id="f15" name="f15" [(ngModel)]="e.item.credits" /></div>
                <div class="col-4"><label class="form-label" for="f16">лек/лаб/пр/СРОП</label><input class="form-control" id="f16" name="f16" [(ngModel)]="e.item.creditStructure" placeholder="2/0/1/1" /></div></div>
              <div class="mb-2"><label class="form-label" for="f17">Кафедра</label><select class="form-select" id="f17" name="f17" [(ngModel)]="e.item.departmentId">@for (d of departments(); track d.id) { <option [ngValue]="d.id">{{ d.name }}</option> }</select></div>
            }
            @case ('umkd-types') {
              <div class="row g-2 mb-2"><div class="col-3"><label class="form-label" for="f18">№</label><input class="form-control" type="number" id="f18" name="f18" [(ngModel)]="e.item.order" /></div>
                <div class="col-9"><label class="form-label" for="f19">Название</label><input class="form-control" id="f19" name="f19" [(ngModel)]="e.item.name" required /></div></div>
              <div class="mb-2"><label class="form-label" for="f20">Описание / требования</label><textarea class="form-control" id="f20" name="f20" rows="3" [(ngModel)]="e.item.description"></textarea></div>
              <div class="form-check"><input class="form-check-input" type="checkbox" id="f21" name="f21" [(ngModel)]="e.item.isRequired" /><label class="form-check-label" for="f21">Обязательный компонент</label></div>
              <div class="form-check"><input class="form-check-input" type="checkbox" id="f22" name="f22" [(ngModel)]="e.item.visibleToStudentsByDefault" /><label class="form-check-label" for="f22">Материалы по умолчанию видны обучающимся</label></div>
            }
          }
        </div>
        <div class="modal-footer"><button type="button" class="btn btn-light" (click)="editing.set(null)">Отмена</button><button type="submit" class="btn btn-primary">Сохранить</button></div>
      </form>
    </app-modal>
  }

  @if (userForm(); as u) {
    <app-modal [title]="u.id ? 'Пользователь' : 'Новый пользователь'" size="lg" (closed)="userForm.set(null)">
      <form (ngSubmit)="saveUser(u)">
        <div class="modal-body">
          <div class="row g-2 mb-2">
            <div class="col-md-6"><label class="form-label" for="u1">ФИО полностью</label><input class="form-control" id="u1" name="u1" [(ngModel)]="u.req.fullName" required /></div>
            <div class="col-md-6"><label class="form-label" for="u2">E-mail (логин)</label><input class="form-control" type="email" id="u2" name="u2" [(ngModel)]="u.req.email" required /></div>
          </div>
          <div class="mb-2"><label class="form-label" for="u3">Учёная степень, должность</label><input class="form-control" id="u3" name="u3" [(ngModel)]="u.req.regalia" /></div>
          <div class="mb-2"><label class="form-label">Роли</label><div class="d-flex flex-wrap gap-3">
            @for (r of roles; track r) { <div class="form-check"><input class="form-check-input" type="checkbox" [id]="'r' + r" [checked]="u.req.roles.includes(r)" (change)="toggleRole(u.req, r)" /><label class="form-check-label" [for]="'r' + r">{{ roleTitles[r] }}</label></div> }
          </div></div>
          <div class="row g-2 mb-2">
            <div class="col-md-4"><label class="form-label" for="u4">Кафедра</label><select class="form-select" id="u4" name="u4" [(ngModel)]="u.req.departmentId"><option [ngValue]="null">—</option>@for (d of departments(); track d.id) { <option [ngValue]="d.id">{{ d.name }}</option> }</select></div>
            <div class="col-md-4"><label class="form-label" for="u5">Институт (для директора)</label><select class="form-select" id="u5" name="u5" [(ngModel)]="u.req.instituteId"><option [ngValue]="null">—</option>@for (i of institutes(); track i.id) { <option [ngValue]="i.id">{{ i.shortName || i.name }}</option> }</select></div>
            <div class="col-md-4"><label class="form-label" for="u6">ОП (для обучающегося)</label><select class="form-select" id="u6" name="u6" [(ngModel)]="u.req.educationalProgramId"><option [ngValue]="null">—</option>@for (p of programs(); track p.id) { <option [ngValue]="p.id">{{ p.code }}</option> }</select></div>
          </div>
          <div style="max-width:360px"><label class="form-label" for="u7">Пароль</label><input class="form-control" type="password" id="u7" name="u7" [(ngModel)]="u.req.password" autocomplete="new-password" />
            @if (u.id) { <div class="form-text">Оставьте пустым, чтобы не менять пароль.</div> }</div>
        </div>
        <div class="modal-footer"><button type="button" class="btn btn-light" (click)="userForm.set(null)">Отмена</button><button type="submit" class="btn btn-primary">Сохранить</button></div>
      </form>
    </app-modal>
  }`
})
export class AdminComponent implements OnInit {
  private api = inject(ApiService);
  private notify = inject(NotifyService);

  tabs: [Tab, string][] = [['users', 'Пользователи'], ['structure', 'Институты и кафедры'], ['programs', 'Образовательные программы'],
    ['disciplines', 'Дисциплины'], ['umkd', 'Компоненты УМКД'], ['settings', 'Настройки шаблона']];
  kindTitles: Record<string, string> = { institutes: 'институт', departments: 'кафедра', programs: 'образовательная программа', disciplines: 'дисциплина', 'umkd-types': 'компонент УМКД' };
  roles = ROLES; roleTitles = ROLE_TITLES; cycles = CYCLE_TITLES; levels = LEVEL_TITLES;
  cycleKeys = Object.keys(CYCLE_TITLES) as DisciplineCycle[];
  levelKeys = Object.keys(LEVEL_TITLES) as StudyLevel[];

  tab = signal<Tab>('users');
  users = signal<AdminUserDto[]>([]);
  institutes = signal<InstituteDto[]>([]);
  departments = signal<DepartmentDto[]>([]);
  programs = signal<ProgramDto[]>([]);
  disciplines = signal<DisciplineDto[]>([]);
  umkdTypes = signal<UmkdTypeDto[]>([]);
  settings = signal<SettingsDto | null>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  editing = signal<{ kind: 'institutes' | 'departments' | 'programs' | 'disciplines' | 'umkd-types'; item: any } | null>(null);
  userForm = signal<{ id: string | null; req: SaveUserRequest } | null>(null);
  deptMap = computed(() => new Map(this.departments().map(d => [d.id, d.name])));

  async ngOnInit() { await this.loadAll(); }

  async loadAll() {
    const [u, i, d, p, dis, t, s] = await Promise.all([
      this.api.users(), this.api.adminList<InstituteDto>('institutes'), this.api.adminList<DepartmentDto>('departments'),
      this.api.adminList<ProgramDto>('programs'), this.api.adminList<DisciplineDto>('disciplines'),
      this.api.adminList<UmkdTypeDto>('umkd-types'), this.api.settings()]);
    this.users.set(u); this.institutes.set(i); this.departments.set(d); this.programs.set(p);
    this.disciplines.set(dis); this.umkdTypes.set(t); this.settings.set(s);
  }

  deptsOf(instituteId: number) { return this.departments().filter(d => d.instituteId === instituteId); }
  deptName(id: number) { return this.deptMap().get(id) ?? ''; }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  edit(kind: 'institutes' | 'departments' | 'programs' | 'disciplines' | 'umkd-types', item: any) {
    this.editing.set({ kind, item: structuredClone(item) });
  }

  async saveItem() {
    const e = this.editing();
    if (!e) return;
    await this.api.adminSave(e.kind, e.item);
    this.editing.set(null);
    this.api.resetReference();
    this.notify.success('Сохранено.');
    await this.loadAll();
  }

  async saveSettings(s: SettingsDto) {
    this.settings.set(await this.api.saveSettings(s));
    this.api.resetReference();
    this.notify.success('Настройки шаблона сохранены. Новые тексты применяются к вновь создаваемым силлабусам.');
  }

  editUser(u: AdminUserDto | null) {
    this.userForm.set({
      id: u?.id ?? null,
      req: {
        email: u?.email ?? '', fullName: u?.fullName ?? '', regalia: u?.regalia ?? '', roles: u ? [...u.roles] : ['Teacher'],
        departmentId: u?.departmentId ?? null, instituteId: u?.instituteId ?? null, educationalProgramId: u?.educationalProgramId ?? null, password: ''
      }
    });
  }

  toggleRole(req: SaveUserRequest, role: string) {
    req.roles = req.roles.includes(role) ? req.roles.filter(r => r !== role) : [...req.roles, role];
  }

  async saveUser(u: { id: string | null; req: SaveUserRequest }) {
    await this.api.saveUser(u.id, { ...u.req, password: u.req.password || null });
    this.userForm.set(null);
    this.notify.success(`Пользователь ${u.req.fullName} сохранён.`);
    this.users.set(await this.api.users());
  }
}
