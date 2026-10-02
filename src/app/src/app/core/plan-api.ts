import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { PlanEntry, ShoppingList } from './models';

@Injectable({ providedIn: 'root' })
export class PlanApi {
  private readonly http = inject(HttpClient);

  plan(week?: string) {
    const params: Record<string, string> | undefined = week ? { week } : undefined;
    return this.http.get<PlanEntry[]>('/api/plan', { params });
  }

  putEntry(date: string, slot: number, recipeId: string) {
    return this.http.put('/api/plan/entry', { date, slot, recipeId }, { responseType: 'text' });
  }

  deleteEntry(date: string, slot: number) {
    return this.http.delete('/api/plan/entry', { params: { date, slot }, responseType: 'text' });
  }

  shopping(week?: string) {
    const params: Record<string, string> | undefined = week ? { week } : undefined;
    return this.http.get<ShoppingList | null>('/api/shopping', { params });
  }

  generate(week: string) {
    return this.http.post<ShoppingList>('/api/shopping/generate', { week });
  }

  setChecked(id: string, checked: boolean) {
    return this.http.patch(`/api/shopping/item/${id}`, { checked }, { responseType: 'text' });
  }

  addCustom(week: string, name: string, unit?: string | null, quantity?: number | null) {
    return this.http.post<ShoppingList>('/api/shopping/item', { week, name, unit, quantity });
  }

  removeItem(id: string) {
    return this.http.delete(`/api/shopping/item/${id}`, { responseType: 'text' });
  }
}

/** Monday-based ISO date string helpers (week plans run Mon–Sun). */
export function currentMonday(): string {
  const now = new Date();
  const offset = (now.getDay() + 6) % 7;
  now.setDate(now.getDate() - offset);
  return toDateOnlyString(now);
}

export function shiftWeek(iso: string, days: number): string {
  const d = isoDate(iso);
  d.setDate(d.getDate() + days);
  return toDateOnlyString(d);
}

export function isoDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, (m ?? 1) - 1, d ?? 1);
}

export function toDateOnlyString(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function formatDaDate(iso: string): string {
  return new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'short' }).format(isoDate(iso));
}
