# The Grimoire Glam Co

A static storefront for resin-printed press-on nails. Products, prices, photos and stock come from Square; the site is rebuilt from that catalog automatically and served as plain HTML from GitHub Pages. Square still handles checkout, tax, orders and payouts.

```
Square catalog ──▶ sync script ──▶ data/products.json + photos ──▶ static pages
Cart ──▶ checkout worker (holds the Square token) ──▶ Square-hosted checkout
```

## What updates by itself
Add, edit or hide an item in Square and the site follows: product pages, collections, prices, option lists (shape, size, length), photos, stock badges and search. The workflow in `.github/workflows/deploy.yml` re-syncs on every push to `main`, every 30 minutes, on demand, and when the optional webhook fires.

## Layout
| Path | Purpose |
|---|---|
| `scripts/sync-catalog.mjs` | Pulls the catalog and inventory from Square, resizes photos to WebP |
| `scripts/lib/transform.mjs` | Pure data shaping (tested against a saved catalog) |
| `scripts/build.mjs` | Generates every page, the sitemap and the search index into `dist/` |
| `src/` | Stylesheet and browser scripts (cart, search, product form, sizing tool) |
| `worker/` | Cloudflare Worker that turns a cart into a Square checkout link |
| `config/site.json` | Site name, contact links, checkout address, low-stock threshold |
| `config/overrides.json` | Per-item tweaks keyed by Square item ID (URL name, grouping) |
| `assets/` | Fonts, favicon and synced product photos |

## Commands
```bash
npm ci
npm test            # catalog transform + worker (Square mocked)
npm run sync        # needs SQUARE_ACCESS_TOKEN in the environment
npm run build       # writes dist/
npm run dev         # build and serve on http://localhost:8080
```
`node scripts/sync-catalog.mjs --from-file scripts/fixtures/catalog.json` runs the sync offline against a saved catalog.

## Configuration
- **Secrets** are never committed. The Square token lives in the repository's Actions secrets (for the sync) and in the worker's secrets (for checkout). `.env`, `.dev.vars` and `.wrangler/` are git-ignored.
- **Domain**: the `CNAME` file holds the custom domain; pages are built for the site root.
- **Worker settings** (allowed origins, redirect URL, location ID) are in `worker/wrangler.toml`. Deploy it with `npx wrangler deploy` from `worker/`.

## Notes
- Prices are never sent from the browser. The worker builds the order from Square catalog IDs, checks the chosen options belong to the item, and re-checks live stock before creating the checkout link.
- Site text never contains em dashes: Square text is cleaned during the sync and the build sweeps every page.
- Stock shown on a page can be up to 30 minutes old; checkout always uses live inventory.

## Contributors
- [Haloman363](https://github.com/Haloman363): owner and maintainer
