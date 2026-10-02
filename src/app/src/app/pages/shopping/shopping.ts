import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PlanApi, currentMonday, formatDaDate, shiftWeek } from '../../core/plan-api';
import type { ShoppingList, ShoppingItem } from '../../core/models';

@Component({
  selector: 'app-shopping',
  imports: [FormsModule],
  templateUrl: './shopping.html',
  styleUrl: './shopping.scss',
})
export class Shopping implements OnInit {
  private readonly planApi = inject(PlanApi);

  protected readonly week = signal(currentMonday());
  protected readonly list = signal<ShoppingList | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly newItemName = signal('');
  protected readonly newItemUnit = signal('');

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
    } catch {
      this.error.set('Kunne ikke hente indkøbslisten.');
    } finally {
      this.busy.set(false);
    }
  }

  protected weekLabel(): string {
    const week = this.week();
    return `${formatDaDate(week)} – ${formatDaDate(shiftWeek(week, 6))}`;
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
      this.error.set('Kunne ikke (gen)generere listen.');
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
          .addCustom(this.week(), name, this.newItemUnit().trim() || null)
          .subscribe({ next: res, error: rej }),
      );
      this.list.set(l);
      this.newItemName.set('');
      this.newItemUnit.set('');
    } catch {
      this.error.set('Fandtes allerede eller fejl.');
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
