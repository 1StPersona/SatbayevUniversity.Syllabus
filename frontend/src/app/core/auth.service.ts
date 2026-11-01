import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LoginResponse, UserDto } from './models';

const STORAGE_KEY = 'syllabus.auth';

interface Session { token: string; expiresAt: string; user: UserDto; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private session = signal<Session | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isStaff = computed(() => this.hasAny('Admin', 'Director', 'HeadOfDepartment', 'Teacher'));
  readonly isAdmin = computed(() => this.hasAny('Admin'));

  get token(): string | null {
    const s = this.session();
    if (!s) return null;
    if (new Date(s.expiresAt).getTime() <= Date.now()) { this.clear(); return null; }
    return s.token;
  }

  hasAny(...roles: string[]): boolean {
    const r = this.session()?.user.roles ?? [];
    return roles.some(x => r.includes(x));
  }

  async login(email: string, password: string): Promise<void> {
    const res = await firstValueFrom(this.http.post<LoginResponse>('/api/auth/login', { email, password }));
    const s: Session = { token: res.token, expiresAt: res.expiresAt, user: res.user };
    this.session.set(s);
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(s)); } catch { /* приватный режим */ }
  }

  logout(returnUrl?: string) {
    this.clear();
    this.router.navigate(['/login'], { queryParams: returnUrl ? { returnUrl } : {} });
  }

  private clear() {
    this.session.set(null);
    try { localStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
  }

  private restore(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const s = JSON.parse(raw) as Session;
      return new Date(s.expiresAt).getTime() > Date.now() ? s : null;
    } catch { return null; }
  }
}
