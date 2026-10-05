import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { RecipesApi } from '../../core/api';
import { Lang } from '../../core/i18n';
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
    await new Promise<unknown>((res, rej) =>
      this.api
        .addComment(r.id, this.commentAuthor.trim() || 'husholdning', this.commentText.trim())
        .subscribe({ next: res, error: rej }),
    );
    this.commentText = '';
    await this.reload();
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

  protected openSource(): void {
    const url = this.recipe()?.sourceUrl;
    if (url) {
      window.open(url, '_blank', 'noopener');
    }
  }
}
