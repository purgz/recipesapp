import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private checked = false;

  readonly authed = signal(false);
  readonly passwordUnavailable = signal(false);

  async ensureChecked(): Promise<boolean> {
    if (!this.checked) {
      try {
        const r = await firstValueFrom(this.http.get<{ authenticated: boolean }>('/api/auth/me'));
        this.authed.set(r.authenticated);
      } catch {
        this.authed.set(false);
      }
      this.checked = true;
    }
    return this.authed();
  }

  async login(password: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post('/api/auth/login', { password }, { observe: 'response', responseType: 'text' }),
      );
      this.passwordUnavailable.set(false);
      this.authed.set(true);
      this.checked = true;
    } catch (e: unknown) {
      const status = (e as { status?: number }).status;
      this.passwordUnavailable.set(status === 500);
      this.authed.set(false);
      throw e;
    }
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } finally {
      this.authed.set(false);
      void inject(Router).navigate(['/login']);
    }
  }
}
