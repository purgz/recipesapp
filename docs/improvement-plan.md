# RecipeApp — improvement plan

_Explored and verified 19 Oct 2026. Scope: UI issues, backend correctness/performance, and
nice-to-have features, ordered by priority. Guardrails from `AGENTS.md` still apply: no LLM,
single household secret (no per-user accounts), Danish units stored verbatim, meal-slot and
shopping-list uniqueness rules unchanged._

## 0. What was implemented in this pass

| Area | Change | Files |
| --- | --- | --- |
| Ingredient parsing | Plain integers (`1 løg`, `250 g mel`), mixed fractions (`3½`, `1 ½`), range lower bounds and punctuation cleanup now parse. Previously **every** integer-quantity line stored the whole line in `Name` with `Qty = null`, so shopping lists could never merge or sum anything. | `src/api/Extraction/IngredientParser.cs` |
| Shopping list | Hand-added items are no longer wiped on every plan change; checked state survives case/whitespace differences; one redundant DB query removed. Regeneration now enriches matching manual items with recipe titles. | `src/api/Services/ShoppingListService.cs` |
| Recipe CRUD | Searching matches title, description, tags **and** ingredients (including `\u`-escaped Danish characters); deleting a recipe removes its image, refreshes affected shopping lists and prunes orphan tags; corrupt `StepsJson` no longer 500s; `AsSplitQuery` avoids the multi-collection join warning. | `src/api/Services/RecipeEndpoints.cs` |
| Extraction | One page download per request shared by JSON-LD and microdata extractors (the fallback used to download twice), plus an 8 MB HTML guard. | `src/api/Extraction/ScrapeFetcher.cs` (new), `SchemaOrgRecipeExtractor.cs`, `MicrodataRecipeExtractor.cs` |
| Auth | Login is throttled (10/min fixed window → 429) and password comparison is constant-time. | `src/api/Program.cs` |
| Recipe list UI | 250 ms debounced, cancellation-safe search (`switchMap`); results stay visible while loading; cards are keyboard operable; empty-state copy fixed. | `pages/recipe-list/*`, `core/i18n.ts` |
| Recipe detail | Portion scaling stepper scales ingredient quantities live; comment failures stay inline instead of replacing the recipe view. | `pages/recipe-detail/*` |
| Meal plan | Clicking a filled cell reopens the chooser to replace it; `Esc` closes the chooser; dates follow the selected UI language. | `pages/meal-plan/*`, `core/plan-api.ts` |
| Shopping UI | Hide-done toggle, checked counter, optional quantity when adding an item, locale-aware dates. | `pages/shopping/*` |
| Navigation / misc | Active nav highlighting, seasonal page error state, locale-aware week labels. | `app.html`, `app.ts`, `pages/seasonal/*` |

Verification: `dotnet build src/api` (0 errors), `npm run build`/`ng build` (clean), and an
API smoke suite covering aggregation, manual-item preservation, delete-refresh, orphan tags,
corrupt JSON and the 429 throttle. UI was served from the API's `wwwroot` (the sandbox blocks
new TCP listeners, so `ng serve` could not bind; on a normal machine use `npm start` +
`dotnet run`).

---

## 1. P0 — correctness and deployment risks

| # | Item | Why it matters | Effort |
| --- | --- | --- | --- |
| 1 | **Apply EF migrations on startup (or in an entrypoint)** | `Program.cs` never calls `Database.Migrate()`. A fresh volume/container starts with no tables and every request 500s. Add a guarded `Migrate()` with retry, or a separate `dotnet ef database update` step in compose. | S |
| 2 | **Backfill existing ingredient rows** | This pass fixes parsing for *new* imports. Existing recipes still store `Qty:null` and `Name:"1 løg, finthakket"`, so their shopping lists cannot merge quantities. Write a one-off, dry-run script that reparses `IngredientsJson`, backs up first, then regenerate the open week. | M |
| 3 | **Block SSRF in `/api/extract`** | The API fetches any user-supplied URL, including `localhost`, RFC1918 and cloud-metadata addresses. Validate scheme, resolve the host, reject private/loopback/link-local ranges, cap redirects. | M |
| 4 | **Make backups WAL-safe** | `scripts/backup.sh` falls back to `cp app.db` when `sqlite3` is absent — the runtime image likely has no sqlite3, so recent WAL pages are lost/inconsistent. Add `sqlite3` to the runtime image (or use `VACUUM INTO` through a small API/CLI helper) and verify restores. | S |
| 5 | **`MealCount` counts ingredient lines, not meals** | `ShoppingListService.Aggregate` uses `bucket.Count`, so two “salt” lines in one recipe report 2 meals. Count distinct `(RecipeId, Date, Slot)`. | S |
| 6 | **SQLite path resolution under custom content roots** | During verification, `Data Source=App_Data/app.db` failed to see an existing schema when the content root was overridden; an absolute path worked. Resolve the path from `ContentRootPath` explicitly (or document absolute paths) to avoid surprises in tests/containers. | S |
| 7 | **Local-time week fallback** | `/api/plan` and `/api/shopping` default to `DateTime.UtcNow` when `?week=` is absent. Around midnight Danish time this can select the previous week. Use Europe/Copenhagen or require the parameter. | S |

---

## 2. P1 — high-impact UX and reliability

| # | Item | Why / notes | Effort |
| --- | --- | --- | --- |
| 8 | **Cache recipe images locally** | Nearly all recipes use `ImageOriginalUrl` (hotlinked). When the source removes/blocks the image, the card and detail break. Fetch + validate on save/import, store under `App_Data/images`, keep the remote URL as fallback. | M |
| 9 | **Thumbnails / responsive images** | Cards download the full source image (often hundreds of KB to MBs). Generate 400 px WebP thumbnails on upload/import and serve `srcset`. | M |
| 10 | **Global 401/429 handling** | When the cookie expires mid-session, pages show generic errors. Add an `HttpInterceptorFn`: 401 → `/login?returnUrl=…`; 429 on login → explain the cooldown. | S |
| 11 | **Shopping list polish** | Hide-done/counter/quantity landed. Next: aisle/category grouping, “clear checked”, clipboard/plain-text export, and a sticky “remaining: n” header. | M |
| 12 | **Meal plan productivity** | Copy previous week, drag/drop or move a meal, “inspire me” random picker, and duplicate a recipe into another slot. Mobile layout currently collapses the 7×4 grid to one column — review. | M |
| 13 | **Recipe detail extras** | Print stylesheet/button, cook mode (one step at a time, wake-lock), ingredient checkboxes for prep. Portion scaling is already in. | M |
| 14 | **Accessibility pass** | Focus trap + `aria-modal` for the chooser overlay, `:focus-visible` outlines, labels for icon-only buttons, `aria-live` for async errors, `prefers-reduced-motion`. | S |
| 15 | **Recipe form safety** | Unsaved-changes guard (`CanDeactivate`), inline URL/title validation, and image upload before the first save (staged upload) instead of “save first”. | S–M |
| 16 | **Tag management** | Orphan tags are now pruned, but there is no way to rename/merge tags. The scraped English keyword noise (`birria tacos recipe`, …) suggests a merge/review screen would help. | M |
| 17 | **Search pagination / result cap** | Search is fast at household scale but `List` returns everything. Add `limit`/`offset` (or infinite scroll) and an “n results” indicator. | S |
| 18 | **Remaining hardcoded locale strings** | Dates in comments use `dd.MM.yyyy`; server validation messages are Danish-only; seasonal produce names stay Danish in EN mode. Decide: localize or state that recipe content/errors are Danish-only. | S |
| 19 | **Seasonal page depth** | Only recipes tagged `IsSeasonalHighlight` are shown. Consider matching recipe ingredients against the month's produce list and suggesting “in season now” recipes automatically. | M |

---

## 3. P2 — backend performance and architecture

| # | Item | Why / notes | Effort |
| --- | --- | --- | --- |
| 20 | **Normalize ingredients into a table** | `IngredientsJson` forces JSON parsing in every shopping generation and prevents SQL joins. A `RecipeIngredient(RecipeId, Position, Qty, Unit, Name)` table makes aggregation, scaling, search and validation straightforward. Requires migration + backfill + DTO mapping. | L |
| 21 | **Full-text search (FTS5)** | Current search does `LIKE` scans over title/description/tags/JSON (with an escaped-form workaround for `ø`). FTS5 with a `unicode61` tokenizer (or a maintained `SearchText` column) is the clean solution. | M |
| 22 | **Indexes** | Add `Recipes(UpdatedAt)` for the default ordering and verify `MealPlanEntries(Date)` / `ShoppingListItems(ShoppingListId)`. The unique `(Date, Slot)` already covers date-prefix lookups. | S |
| 23 | **Upload hardening** | Validate image magic bytes (not just extension), stream uploads instead of buffering, and add a periodic orphan-image cleanup (delete files with no `ImageUploadedPath`). | S–M |
| 24 | **Tests** | No automated tests exist. Add: `IngredientParser` unit tests (the cases fixed here), `ShoppingListService` aggregation tests, and `WebApplicationFactory` endpoint tests against a temp SQLite DB. | M |
| 25 | **Health check** | `/api/health` returns ok without touching the DB. Use `AddHealthChecks().AddDbContextCheck` so Caddy/compose can detect a broken DB. | S |
| 26 | **Extraction resilience** | Add extraction-result caching/dedupe by `SourceUrl`, handle redirects explicitly, and record which extractor succeeded in the draft for easier debugging. | M |
| 27 | **Rate limiting behind Caddy** | The limiter keys on `RemoteIpAddress`; behind the compose proxy every request looks like the Caddy container. Add `UseForwardedHeaders` (trusting the compose network only) so limits/logs see the real client. | S |
| 28 | **Structured logs / correlation** | Add request IDs to scrape and extraction logs; useful when a specific blog fails only sometimes. | S |

---

## 4. P3 — nice-to-have features

| # | Feature | Notes | Effort |
| --- | --- | --- | --- |
| 29 | **PWA / offline shopping list** | The supermarket use case is exactly where signal is worst. Cache the current list + check state locally and sync when back online. | M–L |
| 30 | **Duplicate import detection** | `SourceUrl` is not unique. Warn “this link is already saved” and link to the existing recipe. | S |
| 31 | **Collections / cookbooks** | Group recipes beyond tags (e.g. “Weeknight”, “Guests”, “Baking”). | M |
| 32 | **Favourites / ratings / cook history** | “Cook again” is more useful than a 5-star scale for a household. | M |
| 33 | **Shopping list sharing** | Read-only link or plain-text/WhatsApp export for whoever is at the shop. | S–M |
| 34 | **Leftovers / batch cooking** | Mark a meal as “cooked double” so it covers another slot; affects shopping quantities. | M |
| 35 | **Household settings** | Default servings/household size (used by portion scaling and shopping) instead of per-recipe 4. | S |
| 36 | **Nutrition** | Only with a deterministic data source/API; would add cost/complexity and was not requested. | L |

---

## 5. Recommended order

1. **P0 #1, #4, #6** — deployment/backup safety (small, high risk if skipped).
2. **P0 #2, #5** — make the parser fix visible on existing data; correct meal counts.
3. **P0 #3** — SSRF guard before exposing the app beyond the household.
4. **P1 #8–10** — image caching, thumbnails, session handling (biggest perceived quality jump).
5. **P1 #11–13** — shopping/meal-plan/detail workflow improvements.
6. **P2 #20–24** — normalise ingredients + FTS + tests once the data model is stable.
7. **P3** — pick by household demand; the PWA is the standout.

## 6. Guardrails / decisions to confirm

- **Do not** add LLM features, per-user accounts, or YouTube support (removed for brittleness).
- Keep Danish units verbatim; do not translate or convert on scrape.
- Aggregation matching stays exact on name + unit unless the household explicitly opts into an
  alias table (`spsk`/`spiseskefuld`, `stk`/`stykker`).
- Meal plan uniqueness `(Date, Slot)` and one shopping list per Monday are load-bearing; changes
  need a migration and a UI story for conflicts.
