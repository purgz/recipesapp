import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { RecipesApi } from '../../core/api';
import { Lang } from '../../core/i18n';
import { isInSeason } from '../../core/seasonal-produce';
import type { Recipe } from '../../core/models';

@Component({
  selector: 'app-recipe-detail',
  imports: [DatePipe, FormsModule],
  templateUrl: './recipe-detail.html',
  styleUrl: './recipe-detail.scss',
})
export class RecipeDetail implements OnInit {
  private readonly api = inject(RecipesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly i18n = inject(Lang);

  protected readonly recipe = signal<Recipe | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly commentError = signal('');

  /** Portions the ingredient amounts are currently scaled to. */
  protected readonly portions = signal(4);

  protected readonly scale = computed(() => {
    const recipe = this.recipe();
    const portions = this.portions();
    return recipe && recipe.servings > 0 && portions > 0 ? portions / recipe.servings : 1;
  });

  /** "In season" is derived from tags; no stored flag. */
  protected readonly inSeason = computed(() => isInSeason(this.recipe()?.tags ?? []));

  protected readonly scaledIngredients = computed(() =>
    (this.recipe()?.ingredients ?? []).map((ing) => ({
      ...ing,
      qty: ing.qty === null ? null : ing.qty * this.scale(),
    })),
  );

  protected commentAuthor = '';
  protected commentText = '';

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  private async reload(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }
    this.busy.set(true);
    this.error.set('');
    try {
      const r = await new Promise<Recipe>((res, rej) => this.api.get(id).subscribe({ next: res, error: rej }));
      this.recipe.set(r);
      this.portions.set(r.servings);
    } catch {
      this.error.set(this.i18n.t('detail.error'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async addComment(): Promise<void> {
    const r = this.recipe();
    if (!r || !this.commentText.trim()) {
      return;
    }
    try {
      await new Promise<unknown>((res, rej) =>
        this.api
          .addComment(r.id, this.commentAuthor.trim() || 'husholdning', this.commentText.trim())
          .subscribe({ next: res, error: rej }),
      );
      this.commentText = '';
      this.commentError.set('');
      await this.reload();
    } catch {
      this.commentError.set(this.i18n.t('detail.commentFail'));
    }
  }

  protected async removeComment(commentId: string): Promise<void> {
    const r = this.recipe();
    if (!r) {
      return;
    }
    await new Promise<unknown>((res) => this.api.deleteComment(r.id, commentId).subscribe({ next: res, error: res }));
    await this.reload();
  }

  protected async remove(): Promise<void> {
    const r = this.recipe();
    if (!r || !window.confirm(this.i18n.t('detail.confirmDelete'))) {
      return;
    }
    await new Promise<unknown>((res, rej) => this.api.remove(r.id).subscribe({ next: res, error: rej }));
    void this.router.navigate(['/recipes']);
  }

  protected edit(): void {
    const r = this.recipe();
    if (r) {
      void this.router.navigate(['/recipes', r.id, 'edit']);
    }
  }

  protected adjustPortions(delta: number): void {
    this.portions.update((p) => Math.min(99, Math.max(1, p + delta)));
  }

  protected fmtQty(q: number | null): string {
    return q === null ? '' : String(Math.round(q * 100) / 100);
  }

  protected openSource(): void {
    const url = this.recipe()?.sourceUrl;
    if (url) {
      window.open(url, '_blank', 'noopener');
    }
  }
}
