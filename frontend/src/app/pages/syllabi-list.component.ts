import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { ProgramDto, STATUS_TITLES, SyllabusListItem, SyllabusStatus } from '../core/models';
import { StatusBadgeComponent, UmkdProgressComponent } from '../shared/ui';

@Component({
  selector: 'app-syllabi-list',
  standalone: true,
  imports: [FormsModule, RouterLink, StatusBadgeComponent, UmkdProgressComponent],
  template: `
    <div class="d-flex flex-wrap justify-content-between align-items-center mb-3 gap-2">
      <h1 class="h3 mb-0">{{ auth.isStaff() ? 'Реестр силлабусов' : 'Силлабусы моих дисциплин' }}</h1>
      @if (auth.isStaff()) { <a class="btn btn-primary" routerLink="/syllabi/new">+ Новый силлабус</a> }
    </div>
    <form class="card card-body mb-3" (ngSubmit)="apply()">
      <div class="row g-2 align-items-end">
        <div class="col-md-4"><label class="form-label small mb-1" for="q">Поиск</label>
          <input class="form-control" id="q" name="q" [(ngModel)]="q" placeholder="Код, название, преподаватель" /></div>
        <div class="col-md-2"><label class="form-label small mb-1" for="year">Учебный год</label>
          <select class="form-select" id="year" name="year" [(ngModel)]="year"><option value="">Все</option>
            @for (y of years(); track y) { <option [value]="y">{{ y }}</option> }</select></div>
        @if (auth.isStaff()) {
          <div class="col-md-2"><label class="form-label small mb-1" for="status">Статус</label>
            <select class="form-select" id="status" name="status" [(ngModel)]="status"><option value="">Все действующие</option>
              @for (s of statusList; track s) { <option [value]="s">{{ titles[s] }}</option> }</select></div>
        }
        <div class="col-md-3"><label class="form-label small mb-1" for="prog">Образовательная программа</label>
          <select class="form-select" id="prog" name="programId" [(ngModel)]="programId"><option value="">Все</option>
            @for (p of programs(); track p.id) { <option [value]="p.id">{{ p.code }} {{ p.name }}</option> }</select></div>
        <div class="col-md-1 d-grid"><button class="btn btn-outline-primary" type="submit">Найти</button></div>
      </div>
    </form>
    <div class="card"><div class="table-responsive">
      <table class="table table-hover align-middle mb-0">
        <thead class="table-light"><tr><th>Дисциплина</th><th>ОП</th><th>Год / курс / сем.</th><th>Лектор</th><th>Статус</th>
          @if (auth.isStaff()) { <th style="width:140px">УМКД</th> }</tr></thead>
        <tbody>
          @for (s of items(); track s.id) {
            <tr>
              <td><a [routerLink]="['/syllabi', s.id]" class="fw-semibold">{{ s.disciplineCode }}</a><div class="small">{{ s.disciplineName }}</div></td>
              <td class="small">{{ s.programs.join(', ') }}</td>
              <td class="small text-nowrap">{{ s.academicYear }}<br />{{ s.course }} курс, {{ s.semester }} сем.</td>
              <td class="small">{{ s.lecturer }}</td>
              <td><app-status [status]="s.status" /></td>
              @if (auth.isStaff()) { <td>@if (s.umkd) { <app-umkd-progress [value]="s.umkd" /> }</td> }
            </tr>
          } @empty {
            <tr><td colspan="6" class="text-center text-muted py-4">{{ loading() ? 'Загрузка…' : 'Ничего не найдено.' }}</td></tr>
          }
        </tbody>
      </table>
    </div></div>`
})
export class SyllabiListComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  auth = inject(AuthService);
  items = signal<SyllabusListItem[]>([]);
  years = signal<string[]>([]);
  programs = signal<ProgramDto[]>([]);
  loading = signal(true);
  titles = STATUS_TITLES;
  statusList = Object.keys(STATUS_TITLES) as SyllabusStatus[];
  q = ''; year = ''; status: SyllabusStatus | '' = ''; programId: number | '' = '';

  async ngOnInit() {
    const [years, ref] = await Promise.all([this.api.years(), this.api.reference()]);
    this.years.set(years);
    this.programs.set(ref.programs);
    this.route.queryParamMap.subscribe(async p => {
      this.q = p.get('q') ?? ''; this.year = p.get('year') ?? '';
      this.status = (p.get('status') as SyllabusStatus) ?? ''; this.programId = p.get('programId') ? +p.get('programId')! : '';
      this.loading.set(true);
      try { this.items.set(await this.api.syllabi({ q: this.q, year: this.year, status: this.status, programId: this.programId })); }
      finally { this.loading.set(false); }
    });
  }

  apply() {
    const qp: Record<string, string> = {};
    if (this.q) qp['q'] = this.q;
    if (this.year) qp['year'] = this.year;
    if (this.status) qp['status'] = this.status;
    if (this.programId) qp['programId'] = String(this.programId);
    this.router.navigate([], { queryParams: qp });
  }
}
