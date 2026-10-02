# RecipeApp

Recipe manager for a Danish household: paste recipe URLs (incl. YouTube), extract
ingredients/steps, tags, comments, weekly meal plan, auto shopping list.

## Stack

- API: ASP.NET Core on `net10.0` (dev machine SDK is 10.x; plan originally said 9 — 10 is what's installed/LTS)
- Frontend: Angular 22 (standalone, signals-capable), scaffolded in `src/app`
- DB: EF Core 10 + SQLite at `src/api/App_Data/app.db` (gitignored)
- Angular production build is served from `wwwroot` by the API in prod (single process, no CORS); dev uses `ng serve` proxy instead
- Extraction is deterministic — no LLM API costs on the deployed app

## Commands

- API dev server: `dotnet run --project src/api` → http://localhost:5000 (port comes from `src/api/Properties/launchSettings.json`)
- Frontend dev: `npm start` in `src/app` → :4200, proxies `/api` → :5000 via `src/app/proxy.conf.json`
- Verify before finishing a change: `dotnet build src/api` and `npm run build` in `src/app`
- Migrations: `dotnet ef migrations add <Name> --project src/api -o Data/Migrations` then `dotnet ef database update --project src/api` (dotnet-ef tool, version 10.x)
- Household password: stored via `dotnet user-secrets set "Household:Password" "<secret>" --project src/api` (currently the dev placeholder `test-password` — replace before any real use). Unset password makes `/api/auth/login` return 500 with instructions.

## Layout

- `src/api/` — .NET: entities + DbContext + migrations in `Data/`; `wwwroot` reserved for the Angular prod build
- `src/app/` — Angular app; `proxy.conf.json` for dev
- `AGENTS.md` — this file

## Architecture rules (easy to get wrong)

- Extraction must be a chain of `IRecipeExtractor` implementations, never inline logic in controllers:
  SchemaOrg (JSON-LD) → Microdata (itemprop fallback for old WP blogs like valdemarsro.dk, HtmlAgilityPack) →
  YouTube (metadata/captions via timedtext, no API key) → optional LLM → manual edit form.
  Extractors run lowest `Priority` first and merge warnings when none produce data.
- Auth details: API calls must fail with 401/403, never redirect (SPA owns navigation) — cookie auth events
  override `OnRedirectToLogin`. `IFormFile` upload endpoint must call `.DisableAntiforgery()`.
- LLM stage: OpenAI-compatible endpoint, config-gated by `ExtractLlm:Enabled` (default OFF). On the deployed VM an `ollama` sibling container serves `http://ollama:11434/v1` with a 7–8B Q4 model (12 GB RAM budget — 12B-class models don't have comfortable headroom). LLM extraction runs as a background job (202 + polling), max 1 concurrent, context ≤ 8k. Never a blocking synchronous request.
- Deferred fallback (do not build without asking): local-PC edition on user's hardware + one-way publish `POST /api/publish` with `PUBLISH_TOKEN`. Exists as a concept because VM inference may prove too slow; recipe JSON is the sync unit.
- Auth = single shared household secret → signed HttpOnly cookie. Do NOT add ASP.NET Identity or per-user accounts.
- `Ingredients`/`Steps` are stored as JSON strings (`IngredientsJson`/`StepsJson`) on Recipe; shopping-list aggregation matches on ingredient name + unit exactly.
- Danish ingredient units ("tsk", "spsk", "g", "båd"...) must survive scraping verbatim — never translate or convert.
- Seasonal DK produce per month = static TypeScript constant in the Angular app, not DB rows.
- Meal plan: `MealPlanEntry` unique on `(Date, Slot)`; `ShoppingList` unique per `WeekStart` (Monday). Slot enum: Breakfast/Lunch/Dinner/Extra.
- Routing: `/api/{**}` falls back to 404 BEFORE the SPA `index.html` fallback — keep that ordering in `Program.cs`.
  Do NOT use `UseDefaultFiles()` — root `/` is served by the anonymous SPA file fallback instead.

## Deployment

- Target: Oracle Cloud free Ampere A1 (ARM64). GitHub Actions builds a `linux/arm64` image → GHCR; VM pulls and runs `docker compose up -d`.
- Compose services: `caddy` (TLS + reverse proxy `api:8080`, subdomain `recipes.<domain>` with auto-HTTPS), `api`, `ollama` (model + SQLite + images all on one named data volume).
- Gotcha: ports 80/443 must be opened TWICE on Oracle — VCN subnet security list AND instance security list.
- Oracle may reclaim idle free-tier instances: nightly cron backs up SQLite DB + `App_Data/images` (tarball → Azure Blob, user's existing account). Nothing on the VM is unreproducible except backups.
- Fallback host if reclaimed: Hetzner ~€4/mo, same compose file; also maintain Docker image for `linux/amd64` if switching.
