import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { firstValueFrom, Observable, shareReplay } from 'rxjs';
import {
  AdminUserDto, DashboardDto, DepartmentDto, DisciplineDto, InstituteDto, IssueDto, MaterialDto, MaterialReviewStatus,
  ProgramDto, ReferenceDto, SaveUserRequest, SettingsDto, SyllabusDetails, SyllabusEditModel, SyllabusListItem,
  SyllabusStatus, UmkdDto, UmkdOverviewDto, UmkdTypeDto, WorkflowAction
} from './models';

/** Заголовок, отключающий глобальное уведомление об ошибке (ошибка обрабатывается на месте). */
export const QUIET = { headers: new HttpHeaders({ 'X-Skip-Error-Toast': '1' }) };

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private reference$?: Observable<ReferenceDto>;

  // ---- Справочники (кэшируются до перезагрузки или явного сброса)
  reference(): Promise<ReferenceDto> {
    this.reference$ ??= this.http.get<ReferenceDto>('/api/reference').pipe(shareReplay(1));
    return firstValueFrom(this.reference$);
  }
  resetReference() { this.reference$ = undefined; }

  dashboard() { return firstValueFrom(this.http.get<DashboardDto>('/api/dashboard')); }

  // ---- Силлабусы
  syllabi(filter: { q?: string; status?: SyllabusStatus | ''; year?: string; programId?: number | '' }) {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(filter)) if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    return firstValueFrom(this.http.get<SyllabusListItem[]>('/api/syllabi', { params }));
  }
  years() { return firstValueFrom(this.http.get<string[]>('/api/syllabi/years')); }
  syllabus(id: number) { return firstValueFrom(this.http.get<SyllabusDetails>(`/api/syllabi/${id}`)); }
  template(disciplineId?: number) {
    const params = disciplineId ? new HttpParams().set('disciplineId', disciplineId) : undefined;
    return firstValueFrom(this.http.get<SyllabusEditModel>('/api/syllabi/new', { params }));
  }
  validate(m: SyllabusEditModel) { return firstValueFrom(this.http.post<IssueDto[]>('/api/syllabi/validate', m, QUIET)); }
  create(m: SyllabusEditModel) { return firstValueFrom(this.http.post<{ id: number }>('/api/syllabi', m)); }
  update(id: number, m: SyllabusEditModel) { return firstValueFrom(this.http.put<void>(`/api/syllabi/${id}`, m)); }
  remove(id: number) { return firstValueFrom(this.http.delete<void>(`/api/syllabi/${id}`)); }
  clone(id: number, academicYear: string) {
    return firstValueFrom(this.http.post<{ id: number }>(`/api/syllabi/${id}/clone`, { academicYear }));
  }
  workflow(id: number, body: { action: WorkflowAction; comment?: string; protocolNumber?: string; protocolDate?: string }) {
    return firstValueFrom(this.http.post<SyllabusDetails>(`/api/syllabi/${id}/workflow`, body));
  }
  docx(id: number) { return this.download(`/api/syllabi/${id}/docx`); }

  // ---- УМКД
  umkd(id: number) { return firstValueFrom(this.http.get<UmkdDto>(`/api/syllabi/${id}/umkd`)); }
  upload(id: number, form: FormData) { return firstValueFrom(this.http.post<MaterialDto>(`/api/syllabi/${id}/umkd`, form)); }
  deleteMaterial(id: number) { return firstValueFrom(this.http.delete<void>(`/api/umkd/materials/${id}`)); }
  review(id: number, status: MaterialReviewStatus, comment?: string) {
    return firstValueFrom(this.http.post<MaterialDto>(`/api/umkd/materials/${id}/review`, { status, comment }));
  }
  visibility(id: number, visible: boolean) {
    return firstValueFrom(this.http.post<MaterialDto>(`/api/umkd/materials/${id}/visibility`, { visible }));
  }
  downloadMaterial(id: number) { return this.download(`/api/umkd/materials/${id}/download`); }
  umkdZip(id: number) { return this.download(`/api/syllabi/${id}/umkd/zip`); }
  umkdOverview(year?: string) {
    const params = year ? new HttpParams().set('year', year) : undefined;
    return firstValueFrom(this.http.get<UmkdOverviewDto>('/api/umkd/overview', { params }));
  }

  // ---- Администрирование
  adminList<T>(kind: 'institutes' | 'departments' | 'programs' | 'disciplines' | 'umkd-types') {
    return firstValueFrom(this.http.get<T[]>(`/api/admin/${kind}`));
  }
  adminSave<T>(kind: 'institutes' | 'departments' | 'programs' | 'disciplines' | 'umkd-types', dto: T) {
    return firstValueFrom(this.http.post<T>(`/api/admin/${kind}`, dto));
  }
  settings() { return firstValueFrom(this.http.get<SettingsDto>('/api/admin/settings')); }
  saveSettings(s: SettingsDto) { return firstValueFrom(this.http.put<SettingsDto>('/api/admin/settings', s)); }
  users() { return firstValueFrom(this.http.get<AdminUserDto[]>('/api/admin/users')); }
  saveUser(id: string | null, req: SaveUserRequest) {
    return firstValueFrom(id ? this.http.put(`/api/admin/users/${id}`, req) : this.http.post('/api/admin/users', req));
  }

  /** Скачивание файла с авторизацией: имя берётся из Content-Disposition. */
  private async download(url: string) {
    const res: HttpResponse<Blob> = await firstValueFrom(this.http.get(url, { responseType: 'blob', observe: 'response' }));
    const name = fileName(res.headers.get('Content-Disposition')) ?? 'file';
    const href = URL.createObjectURL(res.body!);
    // Ссылка должна быть в DOM, иначе часть браузеров игнорирует атрибут download и сохраняет файл как «download».
    const a = document.createElement('a');
    a.href = href;
    a.download = name;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(href), 10000);
  }
}

function fileName(cd: string | null): string | null {
  if (!cd) return null;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(cd);
  if (star) return decodeURIComponent(star[1]);
  const plain = /filename="?([^";]+)"?/i.exec(cd);
  return plain ? plain[1] : null;
}

export type { InstituteDto, DepartmentDto, ProgramDto, DisciplineDto, UmkdTypeDto };
