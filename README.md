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

### 1. Square access token
1. Go to https://developer.squareup.com/apps and sign in with your Square seller account.
2. Click **+** to create an application. Name it `Grimoire Glam Website` (any name works).
3. Open it → left menu **Credentials**. At the top, switch from **Sandbox** to **Production**.
4. Under **Production**, click **Show** on the **Access token** and copy it.

A token created this way is a *personal access token*. As far as Square documents it, it can do everything your seller account can (scopes can only be limited with OAuth, which this site does not use). So treat it like a password: it only ever goes into the two secret stores below, never into a file, chat or issue. If it leaks, open the same Credentials page and **Regenerate** it (the old one stops working at once), then update both secrets.

The same token is used in two places: GitHub (read-only sync) and the Cloudflare Worker (creates checkout links).

### 2. GitHub
1. Repo → **Settings → Pages → Source: GitHub Actions**.
2. **Settings → Secrets and variables → Actions → New repository secret**: name `SQUARE_ACCESS_TOKEN`, value = the token.
3. Merge to `main`. The workflow in `.github/workflows/deploy.yml` syncs, builds and deploys. Until the domain is connected, the site is at `https://haloman363.github.io/Grimoire-Glam/`.

### 3. Checkout worker (free Cloudflare account, same one that holds grimoireglam.com)
```bash
cd worker
npx wrangler login
npx wrangler secret put SQUARE_ACCESS_TOKEN     # paste the token at the prompt; never into chat, a file or an issue
npx wrangler deploy                             # serves it at https://checkout.grimoireglam.com
```
`config/site.json` already points `checkoutApi` at `https://checkout.grimoireglam.com`, so deploy the worker **before** merging to `main` or the cart's Checkout button will error.

### 4. Custom domain: grimoireglam.com
The repo already contains `CNAME` = `grimoireglam.com` and the worker allows that origin. In **Cloudflare → DNS** for the domain:
1. Remove any existing apex / `www` records that point at Square or anything else.
2. Add four **A** records for `@` → `185.199.108.153`, `185.199.109.153`, `185.199.110.153`, `185.199.111.153`.
3. Add a **CNAME** `www` → `haloman363.github.io`.
4. Set all of those to **DNS only** (grey cloud) until GitHub has issued the HTTPS certificate. If you later want Cloudflare's proxy on, set SSL/TLS mode to **Full**, never Flexible.
5. Repo → Settings → Pages → Custom domain: `grimoireglam.com` → tick **Enforce HTTPS** once it is available (up to an hour).

The `checkout` subdomain is created automatically by `wrangler deploy`; do not add it by hand.

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
