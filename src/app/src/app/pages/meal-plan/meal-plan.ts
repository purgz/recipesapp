import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RecipesApi } from '../../core/api';
import { Lang } from '../../core/i18n';
import {
  PlanApi,
  currentMonday,
  formatDaDate,
  shiftWeek,
} from '../../core/plan-api';
import type { PlanEntry, RecipeSummary } from '../../core/models';

@Component({
  selector: 'app-meal-plan',
  imports: [FormsModule],
  templateUrl: './meal-plan.html',
  styleUrl: './meal-plan.scss',
})
export class MealPlan implements OnInit {
  private readonly planApi = inject(PlanApi);
  private readonly recipesApi = inject(RecipesApi);
  protected readonly i18n = inject(Lang);

  protected readonly week = signal(currentMonday());
  protected readonly entries = signal<PlanEntry[]>([]);
  protected readonly recipes = signal<RecipeSummary[]>([]);
  protected readonly allTags = signal<string[]>([]);
  protected readonly chooser = signal<{ date: string; slot: number } | null>(null);
  protected readonly chooserSearch = signal('');
  protected readonly chooserTag = signal('');
  protected readonly busy = signal(false);

  protected readonly slots = [{ slot: 1 }, { slot: 2 }, { slot: 3 }, { slot: 4 }];

  protected readonly days = computed(() => {
    const monday = this.week();
    return Array.from({ length: 7 }, (_, i) => {
      const date = shiftWeek(monday, i);
      return {
        label: this.i18n.t(`plan.day.${i}`),
        date,
        dateLabel: formatDaDate(date),
      };
    });
  });

  protected readonly weekLabel = computed(() => {
    const monday = this.week();
    return `${formatDaDate(monday)} - ${formatDaDate(shiftWeek(monday, 6))} (${this.weekNumber(monday)}. uge)`;
  });

  protected readonly chooserCandidates = computed(() => {
    const q = this.chooserSearch().trim().toLowerCase();
    const tag = this.chooserTag();
    return this.recipes().filter(
      (r) =>
        (!tag || r.tags.includes(tag)) &&
        (!q || r.title.toLowerCase().includes(q) || r.tags.some((t) => t.includes(q))),
    );
  });

  async ngOnInit(): Promise<void> {
    this.recipesApi.tags().subscribe((t) => this.allTags.set(t));
    await this.reload();
  }

  private async reload(): Promise<void> {
    this.busy.set(true);
    try {
      const entries = await new Promise<PlanEntry[]>((res, rej) =>
        this.planApi.plan(this.week()).subscribe({ next: res, error: rej }),
      );
      this.entries.set(entries);

      const recipes = await new Promise<RecipeSummary[]>((res, rej) =>
        this.recipesApi.list().subscribe({ next: res, error: rej }),
      );
      this.recipes.set(recipes);
    } finally {
      this.busy.set(false);
    }
  }

  protected entryAt(date: string, slot: number): PlanEntry | undefined {
    return this.entries().find((e) => e.date === date && e.slot === slot && e.recipeId);
  }

  protected openChooser(date: string, slot: number): void {
    this.chooser.set({ date, slot });
    this.chooserSearch.set('');
    this.chooserTag.set('');
  }

  protected closeChooser(): void {
    this.chooser.set(null);
  }

  protected async pick(recipeId: string): Promise<void> {
    const target = this.chooser();
    if (!target) {
      return;
    }
    this.closeChooser();
    await new Promise<unknown>((res, rej) =>
      this.planApi
        .putEntry(target.date, target.slot, recipeId)
        .subscribe({ next: res, error: rej }),
    );
    await this.reload();
  }

  protected async removeEntry(date: string, slot: number): Promise<void> {
    await new Promise<unknown>((res, rej) =>
      this.planApi.deleteEntry(date, slot).subscribe({ next: res, error: rej }),
    );
    await this.reload();
  }

  protected async prevWeek(): Promise<void> {
    this.week.set(shiftWeek(this.week(), -7));
    await this.reload();
  }

  protected async nextWeek(): Promise<void> {
    this.week.set(shiftWeek(this.week(), 7));
    await this.reload();
  }

  protected async gotoThisWeek(): Promise<void> {
    this.week.set(currentMonday());
    await this.reload();
  }

  protected recipeImage(recipeId: string): string | undefined {
    return this.recipes().find((r) => r.id === recipeId)?.imageUrl ?? undefined;
  }

  private weekNumber(iso: string): number {
    const d = new Date(iso + 'T00:00:00');
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7));
    const firstThursday = new Date(d.getFullYear(), 0, 4);
    firstThursday.setHours(0, 0, 0, 0);
    firstThursday.setDate(firstThursday.getDate() - ((firstThursday.getDay() + 6) % 7) + 3);
    return 1 + Math.round((d.getTime() - firstThursday.getTime()) / 604800000);
  }
}
