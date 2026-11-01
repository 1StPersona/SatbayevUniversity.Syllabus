import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { NotifyService } from '../core/notify.service';
import { ReferenceDto, SyllabusDetails, WorkflowAction } from '../core/models';
import { fmt, ModalComponent, StatusBadgeComponent } from '../shared/ui';
import { SyllabusDocumentComponent } from './syllabus-document.component';
import { UmkdTabComponent } from './umkd-tab.component';

@Component({
  selector: 'app-syllabus-details',
  standalone: true,
  imports: [FormsModule, RouterLink, StatusBadgeComponent, ModalComponent, SyllabusDocumentComponent, UmkdTabComponent],
  template: `
  @if (s(); as s) {
    <nav aria-label="breadcrumb"><ol class="breadcrumb small">
      <li class="breadcrumb-item"><a routerLink="/syllabi">Силлабусы</a></li>
      <li class="breadcrumb-item active">{{ s.discipline.code }}, {{ s.data.academicYear }}</li>
    </ol></nav>

    <div class="d-flex flex-wrap justify-content-between align-items-start gap-3 mb-3">
      <div>
        <h1 class="h3 mb-1">{{ s.discipline.code }} {{ s.discipline.name }}</h1>
        <div class="text-muted">{{ s.data.academicYear }} уч. год · курс {{ s.data.course }}, семестр {{ s.data.semester }} ·
          {{ programCodes() }} · <app-status [status]="s.status" /></div>
      </div>
      <div class="d-flex flex-wrap gap-2">
        @if (s.permissions.canEdit) { <a class="btn btn-primary" [routerLink]="['/syllabi', s.id, 'edit']">Редактировать</a> }
        <div class="btn-group">
          <button class="btn btn-outline-secondary" (click)="api.docx(s.id)">Скачать DOCX</button>
          <a class="btn btn-outline-secondary" [routerLink]="['/syllabi', s.id, 'print']" target="_blank">Печать / PDF</a>
        </div>
        @if (s.permissions.canClone) { <button class="btn btn-outline-secondary" (click)="openClone()">Копировать на новый год</button> }
        @if (s.permissions.canDelete) { <button class="btn btn-outline-danger" (click)="remove()">Удалить</button> }
      </div>
    </div>

    @if (s.returnComment) {
      <div class="alert alert-danger"><strong>Возвращён на доработку:</strong><div class="pre-line mt-1">{{ s.returnComment }}</div></div>
    }

    @if (s.actions.length) {
      <div class="card border-primary-subtle mb-3"><div class="card-body d-flex flex-wrap align-items-center gap-2">
        <span class="me-2 fw-semibold">Согласование:</span>
        @for (a of s.actions; track a.action) {
          <button class="btn" [class.btn-success]="isPositive(a.action)" [class.btn-outline-danger]="a.action==='Return'"
                  [class.btn-outline-secondary]="a.action==='Withdraw' || a.action==='Archive'" (click)="startAction(a.action, a.title)"
                  [attr.data-test]="'wf-' + a.action">{{ a.title }}</button>
        }
        @if (hasSubmit() && errors() > 0) { <span class="small text-danger ms-2">Перед отправкой исправьте ошибки ({{ errors() }}).</span> }
      </div></div>
    }

    @if (auth.isStaff() && s.issues.length && ['Draft','Returned','OnDepartmentReview'].includes(s.status)) {
      <div class="card mb-3" [class.border-danger-subtle]="errors() > 0" [class.border-warning-subtle]="errors() === 0">
        <div class="card-header bg-white d-flex justify-content-between">
          <span class="fw-semibold">Проверка на соответствие шаблону</span>
          <span class="small">
            @if (errors()) { <span class="badge text-bg-danger">Ошибок: {{ errors() }}</span> }
            @if (warnings()) { <span class="badge text-bg-warning ms-1">Замечаний: {{ warnings() }}</span> }
          </span>
        </div>
        <ul class="list-group list-group-flush small">
          @for (i of sortedIssues(); track $index) {
            <li class="list-group-item"><span class="badge me-2" [class.text-bg-danger]="i.severity==='error'" [class.text-bg-warning]="i.severity==='warning'">{{ i.severity === 'error' ? 'Ошибка' : 'Замечание' }}</span>
              <span class="text-muted">{{ i.section }}:</span> {{ i.message }}</li>
          }
        </ul>
      </div>
    } @else if (auth.isStaff() && !s.issues.length && s.permissions.canEdit) {
      <div class="alert alert-success py-2">Силлабус заполнен полностью и соответствует шаблону.</div>
    }

    <ul class="nav nav-tabs">
      <li class="nav-item"><button class="nav-link" [class.active]="tab()==='syllabus'" (click)="setTab('syllabus')">Силлабус</button></li>
      <li class="nav-item"><button class="nav-link" [class.active]="tab()==='umkd'" (click)="setTab('umkd')" data-test="tab-umkd">УМКД
        @if (auth.isStaff()) { <span class="badge rounded-pill ms-1" [class.text-bg-success]="s.umkd.isComplete" [class.text-bg-secondary]="!s.umkd.isComplete">{{ s.umkd.percent }}%</span> }</button></li>
      @if (auth.isStaff()) { <li class="nav-item"><button class="nav-link" [class.active]="tab()==='history'" (click)="setTab('history')">История согласования</button></li> }
    </ul>
    <div class="border border-top-0 bg-white rounded-bottom p-3 p-md-4">
      @switch (tab()) {
        @case ('syllabus') { @if (ref(); as r) { <app-syllabus-document [syllabus]="s" [ref]="r" /> } }
        @case ('umkd') { <app-umkd-tab [syllabusId]="s.id" (changed)="reload()" /> }
        @case ('history') {
          <div class="table-responsive"><table class="table table-sm">
            <thead><tr><th>Дата</th><th>Действие</th><th>Статус</th><th>Пользователь</th><th>Комментарий</th></tr></thead>
            <tbody>@for (h of s.history; track $index) {
              <tr><td class="text-nowrap small">{{ f.dateTime(h.at) }}</td><td>{{ h.action }}</td><td><app-status [status]="h.toStatus" /></td><td>{{ h.userName }}</td><td class="pre-line small">{{ h.comment }}</td></tr>
            }</tbody>
          </table></div>
          <div class="small text-muted">Создан: {{ f.dateTime(s.createdAt) }} · Изменён: {{ f.dateTime(s.updatedAt) }} · Автор: {{ s.authorName }}</div>
        }
      }
    </div>

    @if (action(); as a) {
      <app-modal [title]="a.title" (closed)="action.set(null)">
        <form (ngSubmit)="execute()">
          <div class="modal-body">
            @switch (a.action) {
              @case ('Submit') { <p>Силлабус будет передан заведующему кафедрой. До решения кафедры редактирование будет недоступно.</p> }
              @case ('DepartmentApprove') {
                <p>Укажите реквизиты протокола заседания кафедры — они попадут в лист подписей.</p>
                <div class="row g-2">
                  <div class="col-5"><label class="form-label" for="pn">№ протокола</label><input id="pn" name="pn" class="form-control" [(ngModel)]="wf.protocolNumber" required /></div>
                  <div class="col-7"><label class="form-label" for="pd">Дата заседания</label><input id="pd" name="pd" type="date" class="form-control" [(ngModel)]="wf.protocolDate" required /></div>
                </div>
                <label class="form-label mt-2" for="c1">Комментарий</label><textarea id="c1" name="c" class="form-control" rows="2" [(ngModel)]="wf.comment"></textarea>
              }
              @case ('Return') {
                <label class="form-label" for="c2">Что нужно доработать <span class="text-danger">*</span></label>
                <textarea id="c2" name="c" class="form-control" rows="4" [(ngModel)]="wf.comment" required></textarea>
              }
              @case ('Approve') {
                <p>Силлабус будет утверждён и опубликован для обучающихся. В гриф «УТВЕРЖДАЮ» будут подставлены ваши ФИО и текущая дата.</p>
                <label class="form-label" for="c3">Комментарий</label><textarea id="c3" name="c" class="form-control" rows="2" [(ngModel)]="wf.comment"></textarea>
              }
              @default { <p>Подтвердите действие.</p> }
            }
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-light" (click)="action.set(null)">Отмена</button>
            <button type="submit" class="btn btn-primary" [disabled]="busy()" data-test="wf-confirm">{{ a.title }}</button>
          </div>
        </form>
      </app-modal>
    }

    @if (cloning()) {
      <app-modal title="Копировать силлабус" (closed)="cloning.set(false)">
        <form (ngSubmit)="clone()">
          <div class="modal-body">
            <p class="small text-muted">Будет создан черновик с тем же содержанием. Материалы УМКД, протокол и гриф утверждения не копируются.</p>
            <label class="form-label" for="cy">Учебный год</label>
            <input class="form-control" id="cy" name="cy" [(ngModel)]="cloneYear" pattern="\\d{4}-\\d{4}" required />
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-light" (click)="cloning.set(false)">Отмена</button>
            <button type="submit" class="btn btn-primary">Создать копию</button>
          </div>
        </form>
      </app-modal>
    }
  } @else { <div class="text-muted">Загрузка…</div> }`
})
export class SyllabusDetailsComponent implements OnInit {
  api = inject(ApiService);
  auth = inject(AuthService);
  private notify = inject(NotifyService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  f = fmt;

  s = signal<SyllabusDetails | null>(null);
  ref = signal<ReferenceDto | null>(null);
  tab = signal<'syllabus' | 'umkd' | 'history'>('syllabus');
  action = signal<{ action: WorkflowAction; title: string } | null>(null);
  cloning = signal(false);
  busy = signal(false);
  wf = { comment: '', protocolNumber: '', protocolDate: '' };
  cloneYear = '';

  errors = computed(() => this.s()?.issues.filter(i => i.severity === 'error').length ?? 0);
  warnings = computed(() => this.s()?.issues.filter(i => i.severity === 'warning').length ?? 0);
  sortedIssues = computed(() => [...(this.s()?.issues ?? [])].sort((a, b) => a.severity.localeCompare(b.severity)));
  hasSubmit = computed(() => this.s()?.actions.some(a => a.action === 'Submit') ?? false);
  programCodes = computed(() => this.s()?.programs.map(p => p.code).join(', '));

  async ngOnInit() {
    const t = this.route.snapshot.queryParamMap.get('tab');
    if (t === 'umkd' || t === 'history') this.tab.set(t);
    this.route.paramMap.subscribe(() => this.reload());
    this.ref.set(await this.api.reference());
  }

  async reload() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    try { this.s.set(await this.api.syllabus(id)); }
    catch { this.router.navigate(['/syllabi']); }
  }

  setTab(t: 'syllabus' | 'umkd' | 'history') {
    this.tab.set(t);
    this.router.navigate([], { queryParams: { tab: t === 'syllabus' ? null : t }, replaceUrl: true });
  }

  isPositive(a: WorkflowAction) { return ['Submit', 'DepartmentApprove', 'Approve'].includes(a); }

  startAction(action: WorkflowAction, title: string) {
    this.wf = { comment: '', protocolNumber: '', protocolDate: new Date().toISOString().slice(0, 10) };
    this.action.set({ action, title });
  }

  async execute() {
    const a = this.action();
    if (!a) return;
    if (a.action === 'Return' && !this.wf.comment.trim()) { this.notify.warning('Укажите, что нужно доработать.'); return; }
    if (a.action === 'DepartmentApprove' && (!this.wf.protocolNumber.trim() || !this.wf.protocolDate)) {
      this.notify.warning('Укажите номер и дату протокола.'); return;
    }
    this.busy.set(true);
    try {
      const res = await this.api.workflow(this.s()!.id, {
        action: a.action, comment: this.wf.comment || undefined,
        protocolNumber: a.action === 'DepartmentApprove' ? this.wf.protocolNumber : undefined,
        protocolDate: a.action === 'DepartmentApprove' ? this.wf.protocolDate : undefined
      });
      this.s.set(res);
      this.action.set(null);
      this.notify.success(`Статус изменён: ${res.statusName}.`);
    } catch { /* ошибка показана перехватчиком */ }
    finally { this.busy.set(false); }
  }

  openClone() {
    const [a, b] = this.s()!.data.academicYear.split('-').map(Number);
    this.cloneYear = `${a + 1}-${b + 1}`;
    this.cloning.set(true);
  }

  async clone() {
    const res = await this.api.clone(this.s()!.id, this.cloneYear);
    this.cloning.set(false);
    this.notify.success(`Создана копия на ${this.cloneYear} учебный год.`);
    this.router.navigate(['/syllabi', res.id, 'edit']);
  }

  async remove() {
    if (!confirm('Удалить черновик силлабуса и все его материалы УМКД?')) return;
    await this.api.remove(this.s()!.id);
    this.notify.success('Черновик удалён.');
    this.router.navigate(['/syllabi']);
  }
}
