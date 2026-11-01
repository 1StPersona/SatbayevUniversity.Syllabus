import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { UmkdOverviewDto } from '../core/models';
import { StatusBadgeComponent, UmkdProgressComponent } from '../shared/ui';

@Component({
  selector: 'app-umkd-overview',
  standalone: true,
  imports: [FormsModule, RouterLink, StatusBadgeComponent, UmkdProgressComponent],
  template: `
  <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
    <div><h1 class="h3 mb-0">Комплектность УМКД</h1><div class="text-muted small">Состояние компонентов учебно-методических комплексов по дисциплинам</div></div>
    @if (data(); as d) {
      <div class="d-flex gap-2 align-items-center"><label for="year" class="small text-muted">Учебный год</label>
        <select id="year" class="form-select form-select-sm" [ngModel]="d.year" (ngModelChange)="load($event)">
          @for (y of d.years; track y) { <option [value]="y">{{ y }}</option> }
        </select></div>
    }
  </div>
  <div class="d-flex flex-wrap gap-3 small mb-2"><span>✅ принято</span><span>🕓 на проверке</span><span>⚠️ на доработку</span><span>⬜ нет материалов</span><span class="text-muted">— необязательный</span></div>
  @if (data(); as d) {
    <div class="card"><div class="table-responsive">
      <table class="table table-sm table-bordered align-middle mb-0">
        <thead class="table-light"><tr><th style="min-width:220px">Дисциплина</th><th>Статус силлабуса</th>
          @for (t of d.types; track t.id) { <th class="text-center small" style="min-width:70px" [title]="t.description">{{ t.name }}{{ t.isRequired ? '' : '*' }}</th> }
          <th style="min-width:120px">Итого</th></tr></thead>
        <tbody>
          @for (r of d.rows; track r.syllabusId) {
            <tr>
              <td><a [routerLink]="['/syllabi', r.syllabusId]" [queryParams]="{tab:'umkd'}">{{ r.disciplineCode }}</a><span class="small ms-1">{{ r.disciplineName }}</span><div class="small text-muted">{{ r.lecturer }}</div></td>
              <td><app-status [status]="r.status" /></td>
              @for (c of r.cells; track $index) { <td class="text-center" [title]="d.types[$index].name">{{ icons[c] }}</td> }
              <td><app-umkd-progress [value]="r.progress" /></td>
            </tr>
          } @empty { <tr><td [attr.colspan]="d.types.length + 3" class="text-center text-muted py-4">Нет силлабусов за выбранный год.</td></tr> }
        </tbody>
      </table>
    </div></div>
    <div class="small text-muted mt-2">* — необязательный компонент. Компонент считается закрытым, когда хотя бы один материал принят заведующим кафедрой.</div>
  } @else { <div class="text-muted">Загрузка…</div> }`
})
export class UmkdOverviewComponent implements OnInit {
  private api = inject(ApiService);
  data = signal<UmkdOverviewDto | null>(null);
  icons: Record<string, string> = { done: '✅', pending: '🕓', rejected: '⚠️', missing: '⬜', optional: '—' };
  async ngOnInit() { await this.load(); }
  async load(year?: string) { this.data.set(await this.api.umkdOverview(year)); }
}
