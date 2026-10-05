import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { RecipesApi } from '../../core/api';
import { Lang } from '../../core/i18n';
import type { RecipeSummary } from '../../core/models';

@Component({
  selector: 'app-recipe-list',
  imports: [FormsModule],
  templateUrl: './recipe-list.html',
  styleUrl: './recipe-list.scss',
})
export class RecipeList implements OnInit {
  private readonly api = inject(RecipesApi);
  private readonly router = inject(Router);
  protected readonly i18n = inject(Lang);

  protected readonly search = signal('');
  protected readonly activeTag = signal('');
  protected readonly tags = signal<string[]>([]);
  protected readonly busy = signal(true);
  protected readonly error = signal('');
  protected readonly items = signal<RecipeSummary[]>([]);

  async ngOnInit(): Promise<void> {
    this.api.tags().subscribe({
      next: (t) => this.tags.set(t),
      error: () => this.tags.set([]),
    });
    await this.reload();
  }

  protected async reload(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const items = await new Promise<RecipeSummary[]>((res, rej) =>
        this.api.list(this.search(), this.activeTag() || undefined).subscribe({ next: res, error: rej }),
      );
      this.items.set(items);
    } catch {
      this.error.set(this.i18n.t('list.error'));
    } finally {
      this.busy.set(false);
    }
  }

  protected toggleTag(t: string): void {
    this.activeTag.update((cur) => (cur === t ? '' : t));
    void this.reload();
  }

  protected onSearchChange(): void {
    void this.reload();
  }

  protected open(id: string): void {
    void this.router.navigate(['/recipes', id]);
  }

  protected newFromUrl(): void {
    void this.router.navigate(['/recipes/new']);
  }

  protected cardTags(r: RecipeSummary): string[] {
    return (r.tags ?? []).slice(0, 4);
  }

  protected totalMinutes(r: RecipeSummary): number {
    return (r.prepMinutes ?? 0) + (r.cookMinutes ?? 0);
  }
}
