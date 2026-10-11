# The Grimoire Glam Co: storefront

A static site on **GitHub Pages** (your domain, your design) with **Square** still running the catalog, payments, tax, orders, inventory and payouts.

```
Square catalog ──(every 30 min, or instantly via webhook)──▶ GitHub Action ──▶ rebuilt site on GitHub Pages
Customer cart ──▶ Cloudflare Worker (holds the Square token) ──▶ Square-hosted checkout page
```

Add a product, change a price, upload photos, or sell the last one in Square and the site follows. No code edits.

## What updates automatically
| In Square | On the site |
|---|---|
| New item (visible online) | New card on Shop All **and** its own page `/shop/<name>/` |
| Price, name, description, photos | Updated |
| Shape / Size / Length options and the custom-sizing text field | Updated (they come from the item's variations and modifier lists) |
| Stock hits 0 | "Sold out"; low counts show "Low stock" |
| Item hidden online, archived or deleted | Removed |
| New Square category | New filter chip + `/collections/<name>/` page |

The 5 seasonal items have no Square category, so `config/overrides.json` files them under "Seasonal Sets". Give them a real "Seasonal Sets" category in Square and that override becomes unnecessary.

## One-time setup

### 1. Square access token (read-only is enough for the sync)
Square Developer Dashboard → create an application → **Production** access token. The sync needs `ITEMS_READ` and `INVENTORY_READ`. The checkout worker needs `ORDERS_WRITE`, `PAYMENTS_WRITE` (payment links), `ITEMS_READ`, `INVENTORY_READ`. Easiest is one token with those scopes, stored in two places below.

### 2. GitHub
1. Repo → **Settings → Pages → Source: GitHub Actions**.
2. **Settings → Secrets and variables → Actions → New secret**: `SQUARE_ACCESS_TOKEN`.
3. Merge to `main`. The workflow in `.github/workflows/deploy.yml` syncs, builds and deploys. Until the domain is connected, the site is at `https://haloman363.github.io/Grimoire-Glam/`.

### 3. Checkout worker (free Cloudflare account)
```bash
cd worker
npx wrangler login
# edit wrangler.toml: ALLOWED_ORIGINS and REDIRECT_URL (your domain)
npx wrangler secret put SQUARE_ACCESS_TOKEN
npx wrangler deploy          # prints https://grimoire-glam-checkout.<you>.workers.dev
```
Put that URL in `config/site.json` → `"checkoutApi"`, commit, done. **Until `checkoutApi` is set, the cart button says checkout isn't connected.**

### 4. Custom domain
1. Add a file named `CNAME` at the repo root containing just your domain, e.g. `grimoireglam.com`. The build picks it up (canonical URLs, sitemap, root-relative links).
2. At your registrar's DNS:
   - Apex (`grimoireglam.com`): four **A** records → `185.199.108.153`, `185.199.109.153`, `185.199.110.153`, `185.199.111.153`
   - `www`: a **CNAME** → `haloman363.github.io`
3. Repo → Settings → Pages → enter the domain → tick **Enforce HTTPS** once the certificate is issued (can take up to an hour).
4. Add the domain to `ALLOWED_ORIGINS` / `REDIRECT_URL` in `worker/wrangler.toml` and `npx wrangler deploy` again.

### 5. Optional: instant updates (instead of waiting up to 30 min)
Square Developer Dashboard → Webhooks → subscribe `catalog.version.updated` and `inventory.count.updated` to `https://<worker>/square-webhook`. Then:
```bash
npx wrangler secret put SQUARE_WEBHOOK_SIGNATURE_KEY   # from the webhook subscription
npx wrangler secret put GITHUB_TOKEN                   # fine-grained PAT, this repo only, "Contents: read & write"
```
Set `SQUARE_WEBHOOK_URL` and `GITHUB_REPO` in `wrangler.toml`.

## Day to day
- Edit text/design: `scripts/build.mjs` (pages), `src/css/site.css`, `src/js/`, `config/site.json`.
- Force a refresh now: Actions tab → "Sync Square and deploy" → **Run workflow**.
- Local preview: `npm ci && SQUARE_ACCESS_TOKEN=... npm run sync && npm run dev` → http://localhost:8080
- Tests: `npm test` (catalog transform against a saved copy of your real catalog; worker with Square mocked).

## Things to know
- **Stock can be up to 30 min stale on the page.** The worker re-checks live inventory at checkout and refuses sold-out items, so you won't oversell because of the static page.
- **Shipping**: checkout collects a shipping address, but the worker does not add a shipping fee. Square Online's shipping rules don't carry over to payment links. Decide how you want to charge shipping (flat fee, free, included in price) before launch.
- **Gift cards** link out to Square's gift card page (unchanged).
- GitHub pauses scheduled workflows after 60 days with no repo activity. The sync commits on every catalog change, which counts, but if the shop goes quiet for two months, press Run workflow once.
- Don't commit tokens. `.env`/`.dev.vars` are git-ignored.

## Safe to make public
Nothing in this repo is a credential, so it can be made public later:
- **Secrets live outside the repo**: the Square token in GitHub Actions secrets and Cloudflare Worker secrets; webhook key and GitHub token likewise. `.env`, `.dev.vars` and `.wrangler/` are git-ignored.
- What is committed is already public on the live site: business email, social links, product data and photos. Square location/item IDs are identifiers, not credentials (they appear in any storefront's page source).
- Product photos are re-encoded by the sync, so they carry no EXIF/GPS data.
- The test fixture (`scripts/fixtures/catalog.json`) is a trimmed copy of the public catalog. Don't replace it with a raw API dump that includes customers, orders or costs.
- Before making the repo public, turn on **Settings → Code security → Secret scanning + push protection**.
