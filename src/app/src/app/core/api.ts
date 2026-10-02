import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { CommentDto, Recipe, RecipeDraft, RecipeInput, RecipeSummary } from './models';

@Injectable({ providedIn: 'root' })
export class RecipesApi {
  private readonly http = inject(HttpClient);

  list(search?: string, tag?: string) {
    const params: Record<string, string> = {};
    if (search?.trim()) {
      params['search'] = search.trim();
    }
    if (tag?.trim()) {
      params['tag'] = tag.trim();
    }
    return this.http.get<RecipeSummary[]>('/api/recipes', { params });
  }

  get(id: string) {
    return this.http.get<Recipe>(`/api/recipes/${id}`);
  }

  extract(url: string) {
    return this.http.post<RecipeDraft>('/api/extract', { url });
  }

  create(input: RecipeInput) {
    return this.http.post<Recipe>('/api/recipes', input);
  }

  update(id: string, input: RecipeInput) {
    return this.http.put<Recipe>(`/api/recipes/${id}`, input);
  }

  remove(id: string) {
    return this.http.delete(`/api/recipes/${id}`, { responseType: 'text' });
  }

  uploadImage(id: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ imagePath: string }>(`/api/recipes/${id}/image`, form);
  }

  deleteImage(id: string) {
    return this.http.delete(`/api/recipes/${id}/image`, { responseType: 'text' });
  }

  addComment(id: string, author: string, text: string) {
    return this.http.post<CommentDto>(`/api/recipes/${id}/comments`, { author, text });
  }

  deleteComment(id: string, commentId: string) {
    return this.http.delete(`/api/recipes/${id}/comments/${commentId}`, { responseType: 'text' });
  }

  tags() {
    return this.http.get<string[]>('/api/tags');
  }
}
