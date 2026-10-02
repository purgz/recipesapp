import { Routes } from '@angular/router';
import { authGuard } from './core/auth-guard';
import { LoginComponent } from './pages/login/login';
import { MealPlan } from './pages/meal-plan/meal-plan';
import { RecipeDetail } from './pages/recipe-detail/recipe-detail';
import { RecipeForm } from './pages/recipe-form/recipe-form';
import { RecipeList } from './pages/recipe-list/recipe-list';
import { Seasonal } from './pages/seasonal/seasonal';
import { Shopping } from './pages/shopping/shopping';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'recipes', component: RecipeList, canActivate: [authGuard] },
  { path: 'recipes/new', component: RecipeForm, canActivate: [authGuard] },
  { path: 'recipes/:id', component: RecipeDetail, canActivate: [authGuard] },
  { path: 'recipes/:id/edit', component: RecipeForm, canActivate: [authGuard] },
  { path: 'plan', component: MealPlan, canActivate: [authGuard] },
  { path: 'shopping', component: Shopping, canActivate: [authGuard] },
  { path: 'seasonal', component: Seasonal, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'recipes' },
  { path: '**', redirectTo: 'recipes' },
];
