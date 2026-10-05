import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { RecipesApi } from '../../core/api';
import { Lang } from '../../core/i18n';
import type { IngredientEntry, Recipe, RecipeDraft, RecipeInput } from '../../core/models';

@Component({
  selector: 'app-recipe-form',
  imports: [FormsModule],
  templateUrl: './recipe-form.html',
  styleUrl: './recipe-form.scss',
})
export class RecipeForm implements OnInit {
  private readonly api = inject(RecipesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly i18n = inject(Lang);

  protected readonly id = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly extracting = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly warnings = signal<string[]>([]);
  protected readonly extractedUrl = signal('');

  protected imageUploadedPath = signal<string | null>(null);
  protected imageOriginalUrl = signal<string | null>(null);

  protected title = signal('');
  protected description = signal('');
  protected sourceUrl = signal('');
  protected servings = signal(4);
  protected prepMinutes = signal<number | null>(null);
  protected cookMinutes = signal<number | null>(null);
  protected tagsText = signal('');
  protected isSeasonalHighlight = signal(false);
  protected ingredients = signal<IngredientEntry[]>([]);
  protected steps = signal<string[]>([]);

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      await this.loadExisting(id);
      return;
    }

    const url = this.route.snapshot.queryParamMap.get('url');
    if (url) {
      await this.extract(url);
    }
  }

  private async loadExisting(id: string): Promise<void> {
    this.loading.set(true);
    try {
      const r = await new Promise<Recipe>((res, rej) => this.api.get(id).subscribe({ next: res, error: rej }));
      this.id.set(r.id);
      this.title.set(r.title);
      this.description.set(r.description ?? '');
      this.sourceUrl.set(r.sourceUrl ?? '');
      this.servings.set(r.servings);
      this.prepMinutes.set(r.prepMinutes ?? null);
      this.cookMinutes.set(r.cookMinutes ?? null);
      this.tagsText.set(r.tags.join(', '));
      this.isSeasonalHighlight.set(r.isSeasonalHighlight);
      this.ingredients.set(r.ingredients.length ? r.ingredients : [{ qty: null, unit: null, name: '' }]);
      this.steps.set(r.steps.length ? [...r.steps] : ['']);
      this.imageUploadedPath.set(r.imageUploadedPath ?? null);
      this.imageOriginalUrl.set(r.imageOriginalUrl ?? null);
    } catch {
      this.error.set(this.i18n.t('form.loadFail'));
    } finally {
      this.loading.set(false);
    }
  }

  protected async extract(url: string): Promise<void> {
    if (!url.trim()) {
      return;
    }
    this.extracting.set(true);
    this.error.set('');
    this.warnings.set([]);
    try {
      const draft = await new Promise<RecipeDraft>((res, rej) =>
        this.api.extract(url.trim()).subscribe({ next: res, error: rej }),
      );
      this.applyDraft(draft);
    } catch {
      this.error.set(this.i18n.t('form.extractFail'));
    } finally {
      this.extracting.set(false);
    }
  }

  private applyDraft(d: RecipeDraft): void {
    this.title.set(d.title?.trim() || '');
    this.description.set(d.description?.trim() || '');
    this.sourceUrl.set(d.sourceUrl || '');
    this.servings.set(d.servings ?? 4);
    this.prepMinutes.set(d.prepMinutes ?? null);
    this.cookMinutes.set(d.cookMinutes ?? null);
    this.tagsText.set(d.tags.join(', '));
    this.imageOriginalUrl.set(d.imageUrl ?? null);
    this.imageUploadedPath.set(null);
    this.ingredients.set(d.ingredients.length ? d.ingredients : [{ qty: null, unit: null, name: '' }]);
    this.steps.set(d.steps.slice(0, 60).length ? d.steps.slice(0, 60) : ['']);
    this.warnings.set(d.warnings ?? []);
    if (d.sourceUrl) {
      this.extractedUrl.set(d.sourceUrl);
    }
  }

  protected addIngredient(): void {
    this.ingredients.update((list) => [...list, { qty: null, unit: null, name: '' }]);
  }

  protected patchIng(index: number, field: 'qty' | 'unit' | 'name', value: string | number): void {
    this.ingredients.update((list) => {
      const next = [...list];
      const v = field === 'qty' ? (value === '' || value === null ? null : Number(value)) : value;
      next[index] = { ...next[index], [field]: v } as IngredientEntry;
      return next;
    });
  }

  protected patchStep(index: number, value: string): void {
    this.steps.update((list) => {
      const next = [...list];
      next[index] = value;
      return next;
    });
  }

  protected removeIngredient(index: number): void {
    this.ingredients.update((list) => list.filter((_, i) => i !== index));
  }

  protected addStep(): void {
    this.steps.update((s) => [...s, '']);
  }

  protected removeStep(index: number): void {
    this.steps.update((s) => s.filter((_, i) => i !== index));
  }

  protected onImagePicked(evt: Event): void {
    const input = evt.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (this.id() === null) {
      this.error.set(this.i18n.t('form.saveFirstUpload'));
      input.value = '';
      return;
    }
    this.api.uploadImage(this.id()!, file).subscribe({
      next: (r) => {
        this.imageUploadedPath.set(r.imagePath + '?v=' + Date.now());
        this.error.set('');
      },
      error: () => this.error.set(this.i18n.t('form.uploadFail')),
    });
    input.value = '';
  }

  protected removeImage(): void {
    if (this.id() === null) {
      this.error.set(this.i18n.t('form.saveFirstRemove'));
      return;
    }
    this.api.deleteImage(this.id()!).subscribe({
      next: () => this.imageUploadedPath.set(null),
      error: () => this.error.set(this.i18n.t('form.imageDeleteFail')),
    });
  }

  protected async save(): Promise<void> {
    if (!this.title().trim()) {
      this.error.set(this.i18n.t('form.needTitle'));
      return;
    }
    const input: RecipeInput = {
      title: this.title().trim(),
      description: this.description().trim() || null,
      sourceUrl: this.sourceUrl().trim() || null,
      imageOriginalUrl: this.imageOriginalUrl(),
      servings: Math.max(1, this.servings()),
      prepMinutes: this.prepMinutes(),
      cookMinutes: this.cookMinutes(),
      ingredients: this.ingredients().filter((i) => i.name.trim()),
      steps: this.steps().filter((s) => s.trim()),
      tags: this.tagsText()
        .split(',')
        .map((t) => t.trim())
        .filter((t) => t),
      isSeasonalHighlight: this.isSeasonalHighlight(),
    };

    this.saving.set(true);
    this.error.set('');
    try {
      if (this.id()) {
        await new Promise<Recipe>((res, rej) => this.api.update(this.id()!, input).subscribe({ next: res, error: rej }));
        void this.router.navigate(['/recipes', this.id()]);
      } else {
        const r = await new Promise<Recipe>((res, rej) => this.api.create(input).subscribe({ next: res, error: rej }));
        void this.router.navigate(['/recipes', r.id]);
      }
    } catch {
      this.error.set(this.i18n.t('form.saveFail'));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(this.id() ? ['/recipes', this.id()] : ['/recipes']);
  }

  protected previewImage(): string | null {
    return this.imageUploadedPath() ?? this.imageOriginalUrl();
  }


  protected async onUrlInputKeypress(evt: KeyboardEvent): Promise<void> {
    if (evt.key === 'Enter') {
      const target = evt.target as HTMLInputElement;
      await this.extract(target.value);
    }
  }
}
