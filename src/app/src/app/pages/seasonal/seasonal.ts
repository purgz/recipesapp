import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { RecipesApi } from '../../core/api';
import { Lang } from '../../core/i18n';
import { MONTH_NAMES_EN, MONTH_NAMES_DA, seasonalForMonth } from '../../core/seasonal-produce';
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

  protected readonly monthNames = computed(() =>
    this.i18n.current() === 'da' ? MONTH_NAMES_DA : MONTH_NAMES_EN,
  );
  protected readonly produce = computed(() => seasonalForMonth(this.month()));
  protected readonly seasonalRecipes = computed(() => this.recipes());

  async ngOnInit(): Promise<void> {
    const all = await new Promise<RecipeSummary[]>((res, rej) =>
      this.api.list().subscribe({ next: res, error: rej }),
    );
    this.recipes.set(all.filter((r) => r.isSeasonalHighlight));
    this.loading.set(false);
  }

  protected setMonth(m: number): void {
    this.month.set(m);
  }

  protected open(id: string): void {
    void this.router.navigate(['/recipes', id]);
  }
}
