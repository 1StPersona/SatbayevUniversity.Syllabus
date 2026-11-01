import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { NotifyService } from '../core/notify.service';
import { STATUS_TITLES, SyllabusStatus, UmkdProgressDto } from '../core/models';

export const STATUS_CSS: Record<SyllabusStatus, string> = {
  Draft: 'secondary', OnDepartmentReview: 'info', Returned: 'danger',
  DepartmentApproved: 'primary', Approved: 'success', Archived: 'dark'
};

@Component({
  selector: 'app-status',
  standalone: true,
  template: `<span class="badge text-bg-{{ css }}">{{ title }}</span>`
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: SyllabusStatus;
  get css() { return STATUS_CSS[this.status]; }
  get title() { return STATUS_TITLES[this.status]; }
}

@Component({
  selector: 'app-umkd-progress',
  standalone: true,
  template: `
    <div class="d-flex align-items-center gap-2" [title]="'Обязательных компонентов УМКД принято: ' + value.done + ' из ' + value.total">
      <div class="progress flex-grow-1" style="height:8px;min-width:70px">
        <div class="progress-bar" [class.bg-success]="value.isComplete" [style.width.%]="value.percent"></div>
      </div>
      <small class="text-muted text-nowrap">{{ value.done }}/{{ value.total }}</small>
    </div>`
})
export class UmkdProgressComponent {
  @Input({ required: true }) value!: UmkdProgressDto;
}

@Component({
  selector: 'app-toasts',
  standalone: true,
  template: `
    <div class="toast-stack" aria-live="polite">
      @for (t of notify.toasts(); track t.id) {
        <div class="alert shadow-sm alert-dismissible mb-2" role="alert"
             [class.alert-success]="t.kind==='success'" [class.alert-danger]="t.kind==='error'"
             [class.alert-warning]="t.kind==='warning'" [class.alert-info]="t.kind==='info'">
          <div class="fw-semibold">{{ t.text }}</div>
          @if (t.details?.length) {
            <ul class="mb-0 small ps-3">@for (d of t.details; track $index) { <li>{{ d }}</li> }</ul>
          }
          <button type="button" class="btn-close" aria-label="Закрыть" (click)="notify.dismiss(t.id)"></button>
        </div>
      }
    </div>`
})
export class ToastsComponent {
  notify = inject(NotifyService);
}

/** Простое модальное окно без Bootstrap JS. */
@Component({
  selector: 'app-modal',
  standalone: true,
  template: `
    <div class="modal d-block" tabindex="-1" role="dialog" (keydown.escape)="closed.emit()">
      <div class="modal-dialog" [class.modal-lg]="size==='lg'" role="document">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title">{{ title }}</h5>
            <button type="button" class="btn-close" aria-label="Закрыть" (click)="closed.emit()"></button>
          </div>
          <ng-content />
        </div>
      </div>
    </div>
    <div class="modal-backdrop show" (click)="closed.emit()"></div>`
})
export class ModalComponent {
  @Input() title = '';
  @Input() size: 'md' | 'lg' = 'md';
  @Output() closed = new EventEmitter<void>();
}

/** Форматирование для шаблонов. */
export const fmt = {
  date(iso?: string | null) {
    if (!iso) return '—';
    const d = new Date(iso.length === 10 ? iso + 'T00:00:00' : iso);
    return d.toLocaleDateString('ru-RU');
  },
  dateTime(iso?: string | null) {
    if (!iso) return '—';
    return new Date(iso.endsWith('Z') || iso.includes('+') ? iso : iso + 'Z')
      .toLocaleString('ru-RU', { dateStyle: 'short', timeStyle: 'short' });
  },
  longDate(iso?: string | null) {
    if (!iso) return null;
    const d = new Date(iso + 'T00:00:00');
    const month = d.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' }).split(' ')[1];
    return { day: String(d.getDate()).padStart(2, '0'), month, year: d.getFullYear() };
  },
  size(bytes: number) {
    if (bytes < 1024) return `${bytes} Б`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1).replace('.', ',')} КБ`;
    return `${(bytes / 1024 / 1024).toFixed(1).replace('.', ',')} МБ`;
  },
  num(v?: number | null) { return v == null ? '' : String(Math.round(v * 100) / 100).replace('.', ','); },
  shortName(full: string) {
    const p = full.split(' ').filter(Boolean);
    return p.length < 2 ? full : `${p[0]} ${p.slice(1, 3).map(x => x[0] + '.').join('')}`;
  },
  lines(text?: string | null) {
    return (text ?? '').split('\n').map(l => l.trim().replace(/^[–\-—•]\s*/, '').trim()).filter(Boolean);
  },
  paragraphs(text?: string | null) { return (text ?? '').split('\n').map(l => l.trim()).filter(Boolean); },
  creditWord(n: number) {
    const m10 = n % 10, m100 = n % 100;
    if (m10 === 1 && m100 !== 11) return 'кредит';
    if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return 'кредита';
    return 'кредитов';
  }
};

export { DecimalPipe };
