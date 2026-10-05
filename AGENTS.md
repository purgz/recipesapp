# RecipeApp

Recipe manager for a Danish household: paste recipe URLs, extract
ingredients/steps, tags, comments, weekly meal plan, auto shopping list.
No video/YouTube support and NO LLM (user decision) - link pasting only.

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

- `src/api/` — .NET: entities + DbContext + migrations in `Data/`; `wwwroot` reserved for the Angular prod build (a `.gitkeep` must stay — StaticWebAssetsLoader throws if the dir is absent!)
- `src/app/` — Angular app; `proxy.conf.json` for dev
- `deploy/` — VM runtime: `docker-compose.yml` + `Caddyfile` + `.env.example` (`.env` on VM, never committed)
- `Dockerfile` (root, multi-stage: Angular → wwwroot, dotnet publish, single process) + `.dockerignore`
- `.github/workflows/build-push-image.yml` — builds `linux/arm64` image on push to main → GHCR
- `scripts/backup.sh` — nightly tarball (+ optional Azure Blob upload); cron wiring documented in-file
- `AGENTS.md` — this file

## Architecture rules (easy to get wrong)

- Extraction: deterministic chain of `IRecipeExtractor` implementations, never inline logic in controllers:
  SchemaOrg (JSON-LD) → Microdata (itemprop fallback for old WP blogs like valdemarsro.dk, HtmlAgilityPack).
  Extractors run lowest `Priority` first and merge warnings when none produce data. What resists parsing is
  structured manually in the edit form. No YouTube/video extractor (removed - was too brittle); no LLM.
- Auth details: API calls must fail with 401/403, never redirect (SPA owns navigation) — cookie auth events
  override `OnRedirectToLogin`. `IFormFile` upload endpoint must call `.DisableAntiforgery()`.
- NO LLM anywhere (user decision): no Ollama service, no publish/LLM-edition fallback — do not add AI
  features back without asking. Video recipes (YouTube only): metadata + captions land as editable
  draft text; parts that resist parsing are structured manually in the edit form.
- Auth = single shared household secret → signed HttpOnly cookie. Do NOT add ASP.NET Identity or per-user accounts.
- `Ingredients`/`Steps` are stored as JSON strings (`IngredientsJson`/`StepsJson`) on Recipe; shopping-list aggregation matches on ingredient name + unit exactly.
- Danish ingredient units ("tsk", "spsk", "g", "båd"...) must survive scraping verbatim — never translate or convert.
- Seasonal DK produce per month = static TypeScript constant in the Angular app, not DB rows.
- Meal plan: `MealPlanEntry` unique on `(Date, Slot)`; `ShoppingList` unique per `WeekStart` (Monday). Slot enum: Breakfast/Lunch/Dinner/Extra.
- Routing: `/api/{**}` falls back to 404 BEFORE the SPA `index.html` fallback — keep that ordering in `Program.cs`.
  Do NOT use `UseDefaultFiles()` — root `/` is served by the anonymous SPA file fallback instead.

## Deployment

- Target: Oracle Cloud free Ampere A1 (ARM64). GitHub Actions builds a `linux/arm64` image → GHCR; VM pulls and runs `docker compose up -d`.
- Compose services: `caddy` (TLS + reverse proxy `api:8080`, subdomain `recipes.<domain>` with auto-HTTPS) and `api` (SQLite + uploaded images on one named data volume). No LLM — tiny footprint, safe on the always-free tier.
- Gotcha: ports 80/443 must be opened TWICE on Oracle — VCN subnet security list AND instance security list.
- Oracle may reclaim idle free-tier instances: nightly cron backs up SQLite DB + `App_Data/images` (tarball → Azure Blob, user's existing account). Nothing on the VM is unreproducible except backups.
- Fallback host if reclaimed: Hetzner ~€4/mo, same compose file; also maintain Docker image for `linux/amd64` if switching.
