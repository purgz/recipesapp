import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Lang } from '../../core/i18n';
import { PlanApi, currentMonday, formatDate, shiftWeek } from '../../core/plan-api';
import { RecipesApi } from '../../core/api';
import type { IngredientEntry, PlanEntry, Recipe, ShoppingList, ShoppingItem } from '../../core/models';

@Component({
  selector: 'app-shopping',
  imports: [FormsModule],
  templateUrl: './shopping.html',
  styleUrl: './shopping.scss',
})
export class Shopping implements OnInit {
  private readonly planApi = inject(PlanApi);
  private readonly recipesApi = inject(RecipesApi);
  protected readonly i18n = inject(Lang);

  protected readonly week = signal(currentMonday());
  protected readonly list = signal<ShoppingList | null>(null);
  protected readonly details = signal<Map<string, Recipe>>(new Map());
  protected readonly planEntries = signal<PlanEntry[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly newItemName = signal('');
  protected readonly newItemUnit = signal('');
  protected readonly newItemQty = signal<number | null>(null);
  protected readonly showChecked = signal(true);

  /** Hide already-bought lines so the remaining shopping trip stays short. */
  protected readonly visibleItems = computed(() => {
    const items = this.list()?.items ?? [];
    return this.showChecked() ? items : items.filter((i) => !i.checked);
  });

  protected readonly checkedCount = computed(() => (this.list()?.items ?? []).filter((i) => i.checked).length);

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  private async reload(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const l = await new Promise<ShoppingList | null>((res, rej) =>
        this.planApi.shopping(this.week()).subscribe({ next: res, error: rej }),
      );
      this.list.set(l);

      // Load the week's planned recipes so the per-recipe ingredient groups
      // below the aggregated list can be rendered.
      const entries = await new Promise<PlanEntry[]>((res, rej) =>
        this.planApi.plan(this.week()).subscribe({ next: res, error: rej }),
      );
      const ids = [...new Set(entries.map((e) => e.recipeId).filter((id): id is string => !!id))];
      const details = await Promise.all(
        ids.map(
          (id) =>
            new Promise<Recipe | null>((res) =>
              this.recipesApi.get(id).subscribe({ next: (r) => res(r), error: () => res(null) }),
            ),
        ),
      );
      const map = new Map<string, Recipe>();
      details.forEach((r) => {
        if (r) {
          map.set(r.id, r);
        }
      });
      this.details.set(map);
      this.planEntries.set(entries);
    } catch {
      this.error.set(this.i18n.t('shopping.loadFail'));
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Each recipe planned this week with its own ingredients — the per-recipe
   * view next to the aggregated check-off list above it.
   */
  protected readonly grouped = computed(() => {
    const detailMap = this.details();
    const groups = new Map<string, { title: string; ingredients: IngredientEntry[]; when: string[] }>();
    const slotLabel = (slot: number) => this.i18n.t('plan.slot' + slot);
    for (const e of this.planEntries()) {
      if (!e.recipeId) {
        continue;
      }
      const recipe = detailMap.get(e.recipeId);
      if (!recipe) {
        continue;
      }
      if (!groups.has(e.recipeId)) {
        groups.set(e.recipeId, { title: recipe.title, ingredients: recipe.ingredients, when: [] });
      }
      groups.get(e.recipeId)!.when.push(`${formatDate(e.date, this.i18n.current())}, ${slotLabel(e.slot)}`);
    }
    return [...groups.entries()]
      .map(([id, g]) => ({ id, ...g }))
      .sort((a, b) => a.title.localeCompare(b.title, 'da'));
  });

  protected weekLabel(): string {
    const week = this.week();
    return `${formatDate(week, this.i18n.current())} - ${formatDate(shiftWeek(week, 6), this.i18n.current())}`;
  }

  protected async prev(): Promise<void> {
    this.week.set(shiftWeek(this.week(), -7));
    await this.reload();
  }

  protected async next(): Promise<void> {
    this.week.set(shiftWeek(this.week(), 7));
    await this.reload();
  }

  protected async renew(): Promise<void> {
    this.busy.set(true);
    try {
      const l = await new Promise<ShoppingList>((res, rej) =>
        this.planApi.generate(this.week()).subscribe({ next: res, error: rej }),
      );
      this.list.set(l);
    } catch {
      this.error.set(this.i18n.t('shopping.genFail'));
    } finally {
      this.busy.set(false);
    }
  }

  protected toggle(item: ShoppingItem): void {
    this.list.update((l) =>
      l
        ? {
            ...l,
            items: l.items.map((i) => (i.id === item.id ? { ...i, checked: !i.checked } : i)),
          }
        : l,
    );
    this.planApi.setChecked(item.id, !item.checked).subscribe({
      error: () => void this.reload(),
    });
  }

  protected async remove(item: ShoppingItem): Promise<void> {
    await new Promise<unknown>((res) => this.planApi.removeItem(item.id).subscribe({ next: res, error: res }));
    await this.reload();
  }

  protected async addCustom(): Promise<void> {
    const name = this.newItemName().trim();
    if (!name) {
      return;
    }
    try {
      const l = await new Promise<ShoppingList>((res, rej) =>
        this.planApi
          .addCustom(this.week(), name, this.newItemUnit().trim() || null, this.newItemQty())
          .subscribe({ next: res, error: rej }),
      );
      this.list.set(l);
      this.newItemName.set('');
      this.newItemUnit.set('');
      this.newItemQty.set(null);
    } catch {
      this.error.set(this.i18n.t('shopping.existsOrError'));
    }
  }

  protected fmtQty(q?: number | null): string {
    if (q === null || q === undefined) {
      return '';
    }
    if (Number.isInteger(q)) {
      return String(q);
    }
    return String(Math.round(q * 100) / 100);
  }

  protected onKeydown(evt: KeyboardEvent): void {
    if (evt.key === 'Enter') {
      evt.preventDefault();
      void this.addCustom();
    }
  }
}

