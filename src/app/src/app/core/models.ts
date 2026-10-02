export interface IngredientEntry {
  qty: number | null;
  unit: string | null;
  name: string;
}

export interface RecipeSummary {
  id: string;
  title: string;
  description?: string | null;
  imageUrl?: string | null;
  tags: string[];
  servings: number;
  prepMinutes?: number | null;
  cookMinutes?: number | null;
  isSeasonalHighlight: boolean;
  updatedAt: string;
}

export interface CommentDto {
  id: string;
  author: string;
  text: string;
  createdAt: string;
}

export interface Recipe {
  id: string;
  title: string;
  description?: string | null;
  sourceUrl?: string | null;
  imageUploadedPath?: string | null;
  imageOriginalUrl?: string | null;
  imageUrl?: string | null;
  servings: number;
  prepMinutes?: number | null;
  cookMinutes?: number | null;
  ingredients: IngredientEntry[];
  steps: string[];
  tags: string[];
  isSeasonalHighlight: boolean;
  comments: CommentDto[];
  createdAt: string;
  updatedAt: string;
}

export interface RecipeInput {
  title: string;
  description?: string | null;
  sourceUrl?: string | null;
  imageOriginalUrl?: string | null;
  servings: number;
  prepMinutes?: number | null;
  cookMinutes?: number | null;
  ingredients: IngredientEntry[];
  steps: string[];
  tags: string[];
  isSeasonalHighlight: boolean;
}

export interface RecipeDraft {
  sourceUrl?: string | null;
  title?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  servings?: number | null;
  prepMinutes?: number | null;
  cookMinutes?: number | null;
  ingredients: IngredientEntry[];
  steps: string[];
  tags: string[];
  rawText?: string | null;
  rawTextKind?: string | null;
  warnings: string[];
  hasData: boolean;
}

export const SLOT_BREAKFAST = 1;
export const SLOT_LUNCH = 2;
export const SLOT_DINNER = 3;
export const SLOT_EXTRA = 4;

export interface PlanEntry {
  id: string | null;
  date: string;
  slot: number;
  recipeId: string | null;
  recipeTitle: string | null;
}

export interface ShoppingItem {
  id: string;
  name: string;
  unit?: string | null;
  quantity?: number | null;
  recipeTitles: string[];
  mealCount: number;
  checked: boolean;
  addedManually: boolean;
}

export interface ShoppingList {
  weekStart: string;
  generatedAt: string;
  items: ShoppingItem[];
}

