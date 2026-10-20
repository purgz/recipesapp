import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { RecipesApi } from '../../core/api';
import { Lang } from '../../core/i18n';
import { MONTH_NAMES_EN, MONTH_NAMES_DA, seasonalForMonth, seasonalMatches } from '../../core/seasonal-produce';
import type { RecipeSummary } from '../../core/models';

@Component({
  selector: 'app-seasonal',
  imports: [],
  templateUrl: './seasonal.html',
  styleUrl: './seasonal.scss',
})
export class Seasonal implements OnInit {
  private readonly api = inject(RecipesApi);
  private readonly router = inject(Router);
  protected readonly i18n = inject(Lang);

  protected readonly month = signal(new Date().getMonth() + 1);
  protected readonly recipes = signal<RecipeSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  protected readonly monthNames = computed(() =>
    this.i18n.current() === 'da' ? MONTH_NAMES_DA : MONTH_NAMES_EN,
  );
  protected readonly produce = computed(() => seasonalForMonth(this.month()));

  /** Recipe id → the month's produce items its tags matched. */
  protected readonly matches = computed(() => {
    const map = new Map<string, string[]>();
    for (const r of this.recipes()) {
      const hits = seasonalMatches(r.tags ?? [], this.month());
      if (hits.length > 0) {
        map.set(r.id, hits);
      }
    }
    return map;
  });

  protected readonly seasonalRecipes = computed(() =>
    this.recipes().filter((r) => this.matches().has(r.id)),
  );

  async ngOnInit(): Promise<void> {
    try {
      const all = await new Promise<RecipeSummary[]>((res, rej) =>
        this.api.list().subscribe({ next: res, error: rej }),
      );
      this.recipes.set(all);
    } catch {
      this.error.set(this.i18n.t('seasonal.loadFail'));
    } finally {
      this.loading.set(false);
    }
  }

  protected setMonth(m: number): void {
    this.month.set(m);
  }

  protected open(id: string): void {
    void this.router.navigate(['/recipes', id]);
  }
}
