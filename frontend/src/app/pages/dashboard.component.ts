import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { DashboardDto, STATUS_TITLES, SyllabusStatus } from '../core/models';
import { StatusBadgeComponent, STATUS_CSS, UmkdProgressComponent } from '../shared/ui';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, StatusBadgeComponent, UmkdProgressComponent],
  template: `
  @if (data(); as d) {
    @if (auth.isStaff()) {
      <div class="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-2">
        <div>
          <h1 class="h3 mb-0">Здравствуйте, {{ firstName() }}</h1>
          <div class="text-muted">Рабочий стол модуля «Электронный силлабус + УМКД»</div>
        </div>
        <a class="btn btn-primary" routerLink="/syllabi/new">+ Новый силлабус</a>
      </div>
      <div class="row g-3 mb-4">
        @for (st of statuses; track st) {
          <div class="col-6 col-md">
            <a class="card stat-card text-decoration-none h-100" routerLink="/syllabi" [queryParams]="{status: st}">
              <div class="card-body">
                <div class="stat-value text-{{ css[st] }}">{{ d.byStatus[st] ?? 0 }}</div>
                <div class="small text-muted">{{ titles[st] }}</div>
              </div>
            </a>
          </div>
        }
      </div>
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card h-100">
            <div class="card-header bg-white fw-semibold">Мои силлабусы</div>
            @if (!d.mine.length) { <div class="card-body text-muted">У вас пока нет силлабусов. Создайте новый или скопируйте прошлогодний из реестра.</div> }
            @else {
              <div class="table-responsive"><table class="table table-hover align-middle mb-0">
                <thead class="table-light"><tr><th>Дисциплина</th><th>Год</th><th>Статус</th><th style="width:140px">УМКД</th></tr></thead>
                <tbody>
                  @for (s of d.mine; track s.id) {
                    <tr>
                      <td><a [routerLink]="['/syllabi', s.id]">{{ s.disciplineCode }}</a><span class="text-muted ms-1">{{ s.disciplineName }}</span></td>
                      <td class="text-nowrap">{{ s.academicYear }}</td>
                      <td><app-status [status]="s.status" /></td>
                      <td>@if (s.umkd) { <app-umkd-progress [value]="s.umkd" /> }</td>
                    </tr>
                  }
                </tbody>
              </table></div>
            }
          </div>
        </div>
        <div class="col-lg-5">
          <div class="card h-100">
            <div class="card-header bg-white fw-semibold">Требуют вашего решения</div>
            <ul class="list-group list-group-flush">
              @for (s of d.awaitingMe; track s.id) {
                <li class="list-group-item d-flex justify-content-between align-items-center">
                  <div><a [routerLink]="['/syllabi', s.id]">{{ s.disciplineCode }} {{ s.disciplineName }}</a>
                    <div class="small text-muted">{{ s.academicYear }} · {{ s.lecturer }}</div></div>
                  <app-status [status]="s.status" />
                </li>
              }
              @if (d.materialsToReview > 0) {
                <li class="list-group-item"><a routerLink="/umkd">Материалы УМКД на проверке: <strong>{{ d.materialsToReview }}</strong></a></li>
              }
              @if (!d.awaitingMe.length && !d.materialsToReview) { <li class="list-group-item text-muted">Нет документов, ожидающих вашего решения.</li> }
            </ul>
          </div>
        </div>
      </div>
    } @else {
      <h1 class="h3">Мои дисциплины</h1>
      <p class="text-muted">Утверждённые силлабусы и учебно-методические материалы по вашей образовательной программе.</p>
      @if (!d.published.length) { <div class="alert alert-light border">Для вашей программы пока нет опубликованных силлабусов.</div> }
      <div class="row g-3">
        @for (s of d.published; track s.id) {
          <div class="col-md-6 col-xl-4">
            <div class="card h-100"><div class="card-body">
              <div class="small text-muted">{{ s.disciplineCode }} · курс {{ s.course }}, семестр {{ s.semester }} · {{ s.credits }} кр.</div>
              <h2 class="h5 mt-1">{{ s.disciplineName }}</h2>
              <div class="small mb-3">Лектор: {{ s.lecturer }}</div>
              <a class="btn btn-sm btn-primary me-1" [routerLink]="['/syllabi', s.id]">Силлабус</a>
              <a class="btn btn-sm btn-outline-primary" [routerLink]="['/syllabi', s.id]" [queryParams]="{tab:'umkd'}">Материалы</a>
            </div></div>
          </div>
        }
      </div>
    }
  } @else { <div class="text-muted">Загрузка…</div> }`
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  data = signal<DashboardDto | null>(null);
  statuses: SyllabusStatus[] = ['Draft', 'Returned', 'OnDepartmentReview', 'DepartmentApproved', 'Approved'];
  titles = STATUS_TITLES;
  css = STATUS_CSS;

  firstName() { return (this.auth.user()?.fullName ?? '').split(' ')[1] ?? ''; }
  async ngOnInit() { this.data.set(await this.api.dashboard()); }
}
