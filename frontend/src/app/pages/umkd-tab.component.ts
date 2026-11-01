import { Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { NotifyService } from '../core/notify.service';
import { MaterialDto, UmkdDto } from '../core/models';
import { fmt, ModalComponent, UmkdProgressComponent } from '../shared/ui';

/** Вкладка УМКД в карточке силлабуса: компоненты, загрузка, проверка, видимость. */
@Component({
  selector: 'app-umkd-tab',
  standalone: true,
  imports: [FormsModule, ModalComponent, UmkdProgressComponent],
  template: `
  @if (umkd(); as u) {
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
      <div>
        <h2 class="h5 mb-0">Учебно-методический комплекс дисциплины</h2>
        @if (auth.isStaff()) { <div class="small text-muted">Обязательных компонентов принято: {{ u.progress.done }} из {{ u.progress.total }}</div> }
      </div>
      <div class="d-flex gap-2">
        @if (u.canManage) { <button class="btn btn-primary btn-sm" (click)="openUpload()" data-test="umkd-add">+ Добавить материал</button> }
        <button class="btn btn-outline-secondary btn-sm" (click)="api.umkdZip(syllabusId())">Скачать УМКД (ZIP)</button>
      </div>
    </div>
    @if (auth.isStaff()) { <div class="mb-3"><app-umkd-progress [value]="u.progress" /></div> }

    <div class="list-group">
      @for (c of u.components; track c.type.id) {
        <div class="list-group-item">
          <button class="btn btn-link text-start text-decoration-none w-100 p-0 d-flex align-items-center gap-2" (click)="toggle(c.type.id)">
            <span>{{ c.isComplete ? '✅' : c.type.isRequired ? '⬜' : '▫️' }}</span>
            <span class="flex-grow-1 text-body">{{ c.type.name }} @if (!c.type.isRequired) { <span class="text-muted small">(необязательный)</span> }</span>
            @if (auth.isStaff()) { <span class="badge" [class.text-bg-success]="c.isComplete" [class.text-bg-light]="!c.isComplete" [class.border]="!c.isComplete">{{ c.stateText }}</span> }
            <span class="badge rounded-pill text-bg-secondary">{{ c.materials.length }}</span>
          </button>
          @if (expanded().has(c.type.id)) {
            <div class="mt-2 ps-4">
              <p class="small text-muted mb-2">{{ c.type.description }}</p>
              @if (c.type.isSyllabus) {
                <a href="" (click)="$event.preventDefault(); api.docx(syllabusId())">Силлабус (DOCX)</a>
              } @else if (!c.materials.length) {
                <div class="text-muted small">Материалы не загружены.</div>
              } @else {
                @for (m of c.materials; track m.id) {
                  <div class="border rounded p-2 mb-2">
                    <div class="d-flex flex-wrap justify-content-between gap-2">
                      <div>
                        <a href="" class="fw-semibold" (click)="$event.preventDefault(); open(m)">{{ m.title }}</a>
                        <div class="small text-muted">
                          @if (m.fileName) { {{ m.fileName }}, {{ f.size(m.fileSize) }} · } @if (m.externalUrl) { ссылка · }
                          {{ m.uploadedByName }}, {{ f.dateTime(m.uploadedAt) }}
                        </div>
                        @if (m.description) { <div class="small">{{ m.description }}</div> }
                        @if (auth.isStaff() && m.reviewComment) { <div class="small text-danger">Комментарий: {{ m.reviewComment }}</div> }
                      </div>
                      @if (auth.isStaff()) {
                        <div class="text-end">
                          <span class="badge" [class.text-bg-success]="m.reviewStatus==='Accepted'" [class.text-bg-danger]="m.reviewStatus==='Rejected'" [class.text-bg-warning]="m.reviewStatus==='Pending'">{{ m.reviewStatusName }}</span>
                          <span class="badge text-bg-light border ms-1">{{ m.visibleToStudents ? 'видно обучающимся' : 'только для преподавателей' }}</span>
                          <div class="mt-2 d-flex gap-1 justify-content-end flex-wrap">
                            @if (u.canReview && m.reviewStatus !== 'Accepted') { <button class="btn btn-sm btn-outline-success" (click)="accept(m)">Принять</button> }
                            @if (u.canReview) { <button class="btn btn-sm btn-outline-danger" (click)="rejecting.set(m); rejectComment = ''">На доработку</button> }
                            @if (u.canManage || u.canReview) { <button class="btn btn-sm btn-outline-secondary" (click)="toggleVisibility(m)">{{ m.visibleToStudents ? 'Скрыть' : 'Показать' }} обучающимся</button> }
                            @if (u.canManage) { <button class="btn btn-sm btn-outline-danger" (click)="remove(m)">Удалить</button> }
                          </div>
                        </div>
                      }
                    </div>
                  </div>
                }
              }
            </div>
          }
        </div>
      }
    </div>
  } @else { <div class="text-muted">Загрузка…</div> }

  @if (uploading()) {
    <app-modal title="Добавить материал УМКД" size="lg" (closed)="uploading.set(false)">
      <form (ngSubmit)="upload()">
        <div class="modal-body">
          <div class="mb-3"><label class="form-label" for="ct">Компонент УМКД</label>
            <select class="form-select" id="ct" name="ct" [(ngModel)]="form.componentTypeId" (ngModelChange)="syncVisibility()" required>
              @for (c of uploadable(); track c.type.id) { <option [ngValue]="c.type.id">{{ c.type.name }}{{ c.type.isRequired ? '' : ' (необязательный)' }}</option> }
            </select></div>
          <div class="mb-3"><label class="form-label" for="title">Название</label>
            <input class="form-control" id="title" name="title" [(ngModel)]="form.title" placeholder="Например: Лекция 1. Введение" />
            <div class="form-text">Если не указать, будет использовано имя файла.</div></div>
          <div class="mb-3"><label class="form-label" for="file">Файл</label>
            <input class="form-control" type="file" id="file" (change)="pick($event)" />
            <div class="form-text">PDF, Office, архивы, изображения, видео — до 50 МБ.</div></div>
          <div class="mb-3"><label class="form-label" for="url">или ссылка на электронный ресурс</label>
            <input class="form-control" type="url" id="url" name="url" [(ngModel)]="form.url" placeholder="https://" /></div>
          <div class="mb-3"><label class="form-label" for="desc">Описание</label>
            <textarea class="form-control" id="desc" name="desc" rows="2" [(ngModel)]="form.description"></textarea></div>
          <div class="form-check"><input class="form-check-input" type="checkbox" id="vis" name="vis" [(ngModel)]="form.visible" />
            <label class="form-check-label" for="vis">Показывать обучающимся после проверки</label></div>
        </div>
        <div class="modal-footer">
          <button type="button" class="btn btn-light" (click)="uploading.set(false)">Отмена</button>
          <button type="submit" class="btn btn-primary" [disabled]="busy()">{{ busy() ? 'Загрузка…' : 'Загрузить' }}</button>
        </div>
      </form>
    </app-modal>
  }

  @if (rejecting(); as m) {
    <app-modal title="Вернуть материал на доработку" (closed)="rejecting.set(null)">
      <div class="modal-body">
        <div class="small text-muted mb-2">{{ m.title }}</div>
        <label class="form-label" for="rc">Что нужно доработать</label>
        <textarea class="form-control" id="rc" rows="3" [(ngModel)]="rejectComment"></textarea>
      </div>
      <div class="modal-footer">
        <button class="btn btn-light" (click)="rejecting.set(null)">Отмена</button>
        <button class="btn btn-danger" [disabled]="!rejectComment.trim()" (click)="reject(m)">Отправить на доработку</button>
      </div>
    </app-modal>
  }`
})
export class UmkdTabComponent implements OnInit {
  api = inject(ApiService);
  auth = inject(AuthService);
  private notify = inject(NotifyService);
  syllabusId = input.required<number>();
  changed = output<void>();
  f = fmt;

  umkd = signal<UmkdDto | null>(null);
  expanded = signal(new Set<number>());
  uploading = signal(false);
  busy = signal(false);
  rejecting = signal<MaterialDto | null>(null);
  rejectComment = '';
  file: File | null = null;
  form = { componentTypeId: 0, title: '', description: '', url: '', visible: true };

  uploadable = () => (this.umkd()?.components ?? []).filter(c => !c.type.isSyllabus);

  async ngOnInit() { await this.load(); }

  async load() {
    const u = await this.api.umkd(this.syllabusId());
    this.umkd.set(u);
    if (!this.auth.isStaff()) this.expanded.set(new Set(u.components.map(c => c.type.id)));
  }

  toggle(id: number) {
    const s = new Set(this.expanded());
    s.has(id) ? s.delete(id) : s.add(id);
    this.expanded.set(s);
  }

  openUpload() {
    this.form = { componentTypeId: this.uploadable()[0]?.type.id ?? 0, title: '', description: '', url: '', visible: true };
    this.file = null;
    this.syncVisibility();
    this.uploading.set(true);
  }

  syncVisibility() {
    const t = this.uploadable().find(c => c.type.id === this.form.componentTypeId)?.type;
    if (t) this.form.visible = t.visibleToStudentsByDefault;
  }

  pick(e: Event) { this.file = (e.target as HTMLInputElement).files?.[0] ?? null; }

  async upload() {
    if (!this.file && !this.form.url.trim()) { this.notify.warning('Прикрепите файл или укажите ссылку.'); return; }
    if (this.file && this.file.size > 50 * 1024 * 1024) { this.notify.warning('Файл больше 50 МБ.'); return; }
    const fd = new FormData();
    fd.append('componentTypeId', String(this.form.componentTypeId));
    fd.append('title', this.form.title);
    fd.append('description', this.form.description);
    fd.append('url', this.form.url.trim());
    fd.append('visibleToStudents', String(this.form.visible));
    if (this.file) fd.append('file', this.file, this.file.name);
    this.busy.set(true);
    try {
      const m = await this.api.upload(this.syllabusId(), fd);
      this.notify.success(`Материал «${m.title}» загружен и отправлен на проверку.`);
      this.uploading.set(false);
      this.expanded.set(new Set([...this.expanded(), m.componentTypeId]));
      await this.refresh();
    } finally { this.busy.set(false); }
  }

  async accept(m: MaterialDto) { await this.api.review(m.id, 'Accepted'); this.notify.success('Материал принят.'); await this.refresh(); }

  async reject(m: MaterialDto) {
    await this.api.review(m.id, 'Rejected', this.rejectComment.trim());
    this.rejecting.set(null);
    this.notify.info('Материал возвращён на доработку.');
    await this.refresh();
  }

  async toggleVisibility(m: MaterialDto) { await this.api.visibility(m.id, !m.visibleToStudents); await this.refresh(); }

  async remove(m: MaterialDto) {
    if (!confirm(`Удалить материал «${m.title}»?`)) return;
    await this.api.deleteMaterial(m.id);
    this.notify.success('Материал удалён.');
    await this.refresh();
  }

  async open(m: MaterialDto) {
    if (!m.hasFile && m.externalUrl) { window.open(m.externalUrl, '_blank', 'noopener'); return; }
    await this.api.downloadMaterial(m.id);
  }

  private async refresh() { await this.load(); this.changed.emit(); }
}
