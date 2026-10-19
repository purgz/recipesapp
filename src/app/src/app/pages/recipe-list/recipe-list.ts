import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, catchError, debounceTime, merge, of, startWith, switchMap, tap } from 'rxjs';
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
  private readonly destroyRef = inject(DestroyRef);
  protected readonly i18n = inject(Lang);

  protected readonly search = signal('');
  protected readonly activeTag = signal('');
  protected readonly tags = signal<string[]>([]);
  protected readonly busy = signal(true);
  protected readonly error = signal('');
  protected readonly items = signal<RecipeSummary[]>([]);

  /** Keystrokes are debounced; tag clicks apply immediately. */
  private readonly searchChanges = new Subject<void>();
  private readonly tagChanges = new Subject<void>();

  ngOnInit(): void {
    this.api.tags().subscribe({
      next: (t) => this.tags.set(t),
      error: () => this.tags.set([]),
    });

    // switchMap cancels the previous request, so a slow response for an old
    // query can never overwrite the results of a newer one.
    merge(
      this.searchChanges.pipe(debounceTime(250)),
      this.tagChanges,
    )
      .pipe(
        startWith(void 0),
        tap(() => {
          this.busy.set(true);
          this.error.set('');
        }),
        switchMap(() =>
          this.api.list(this.search(), this.activeTag() || undefined).pipe(
            catchError(() => {
              this.error.set(this.i18n.t('list.error'));
              return of([] as RecipeSummary[]);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((items) => {
        this.items.set(items);
        this.busy.set(false);
      });
  }

  protected toggleTag(t: string): void {
    this.activeTag.update((cur) => (cur === t ? '' : t));
    this.tagChanges.next();
  }

  protected onSearchChange(): void {
    this.searchChanges.next();
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
