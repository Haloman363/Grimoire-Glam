# Handoff: Cloudflare side of the Grimoire Glam storefront

You are picking up one section of a larger project. Everything else (site, sync, worker code, tests, README) is already built and pushed. Your job is the **Cloudflare work only**. Read `README.md` first for the architecture; this file is the runbook.

## Context
- Repo: `haloman363/Grimoire-Glam`, branch `claude/sleepy-edison-ck51pd` (do not push elsewhere; do not open a PR unless asked).
- Static site on GitHub Pages at **`grimoireglam.com`** (the file `CNAME` already says so). Square still does payments/catalog.
- `worker/` is a Cloudflare Worker (`worker/src/index.js`, config in `worker/wrangler.toml`) that turns a cart into a Square-hosted checkout. It must be served at **`https://checkout.grimoireglam.com`** (`routes` in wrangler.toml uses `custom_domain = true`, so wrangler creates that DNS record itself).
- `config/site.json` already has `"checkoutApi": "https://checkout.grimoireglam.com"`.
- The zone `grimoireglam.com` is already on the owner's Cloudflare account and **already has some DNS records** (it resolves to Cloudflare IPs). Treat them as unknown and live.

## Your tasks
1. Deploy the worker.
2. Make sure the worker has the `SQUARE_ACCESS_TOKEN` secret.
3. Point `grimoireglam.com` at GitHub Pages in DNS.
4. Verify, then report.

Not your tasks: GitHub Pages settings, merging to `main`, running the GitHub workflow, Square dashboard changes, shipping fees, secret scanning. List these as "left for the owner" in your report.

## Hard rules
- **Never ask for, print, log, echo or commit a token, key or password.** Read credentials only from environment variables. If something is missing, tell the owner which variable or step is needed and stop that task.
- **Never paste secrets into a file in the repo.** `.dev.vars`, `.env`, `.wrangler/` are git-ignored; still don't create them with real values.
- **Touch only the `grimoireglam.com` zone and the `grimoire-glam-checkout` worker.** Nothing else on the account.
- **DNS is outward-facing: list existing records first, show the owner a before/after table, and wait for an explicit "yes" before creating, changing or deleting any record.**
- Do not create real Square orders or payments to test.
- Do not change code unless the deploy genuinely fails because of it; if you do, keep it minimal, run `npm test`, and say exactly what changed.

## Prerequisites (check, don't assume)
Environment variables: `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID`.
The token needs: Account → Workers Scripts: Edit; Zone `grimoireglam.com` → DNS: Edit and Workers Routes: Edit; Account Settings: Read. (The "Edit Cloudflare Workers" template plus DNS Edit on that zone covers it.)

```bash
cd /path/to/Grimoire-Glam && git checkout claude/sleepy-edison-ck51pd && git pull
npm ci
cd worker && npx wrangler whoami          # must succeed and show the right account
```
If `wrangler whoami` fails, or the zone is not on that account, stop and report.

## Task 1: deploy the worker
```bash
cd worker
npx wrangler deploy
```
- Expect it to create the custom domain `checkout.grimoireglam.com` and print the deployment.
- If it fails because a DNS record named `checkout` already exists, **do not delete it**: show the existing record to the owner and ask.
- If it fails because the zone is not in the same account as the token, report that.

## Task 2: the Square secret
Check whether it is already set (names only, never values):
```bash
npx wrangler secret list
```
- If `SQUARE_ACCESS_TOKEN` is listed, leave it alone.
- If not, and the token is not available to you as an environment variable, **stop and tell the owner to run `cd worker && npx wrangler secret put SQUARE_ACCESS_TOKEN` themselves** (or add it in the Cloudflare dashboard: Workers & Pages → grimoire-glam-checkout → Settings → Variables and Secrets). Do not ask them to send you the token.
- Optional webhook secrets (`SQUARE_WEBHOOK_SIGNATURE_KEY`, `GITHUB_TOKEN`) are **not** needed now; skip unless the owner asks.

## Task 3: DNS for GitHub Pages
1. Get the zone: `GET https://api.cloudflare.com/client/v4/zones?name=grimoireglam.com`.
2. List all records: `GET /zones/<zone_id>/dns_records?per_page=100`. Show them to the owner as a table.
3. Propose this target state and **wait for approval**:

| Type | Name | Content | Proxy |
|---|---|---|---|
| A | `@` | 185.199.108.153 | DNS only |
| A | `@` | 185.199.109.153 | DNS only |
| A | `@` | 185.199.110.153 | DNS only |
| A | `@` | 185.199.111.153 | DNS only |
| CNAME | `www` | `haloman363.github.io` | DNS only |

   Optional IPv6 (only if the owner wants it): AAAA `@` → `2606:50c0:8000::153`, `2606:50c0:8001::153`, `2606:50c0:8002::153`, `2606:50c0:8003::153`.
4. "DNS only" (grey cloud) matters: GitHub must reach the domain directly to issue the HTTPS certificate. If the owner later wants Cloudflare proxying, SSL/TLS mode must be **Full**, never Flexible.
5. Existing `@`/`www` records that point elsewhere (for example the old Square site) will conflict. Show them in the table as "to remove" and ask. Leave unrelated records (MX, TXT for email, etc.) alone, and call out any you are unsure about instead of deleting.
6. After approval, apply the changes and re-list the records to confirm. Record every record ID you created or deleted in your report so the change can be rolled back.

## Task 4: verify
```bash
# worker is up
curl -s https://checkout.grimoireglam.com/health                # -> {"ok":true}

# CORS: allowed origin gets the header, a stranger does not
curl -si -X OPTIONS https://checkout.grimoireglam.com/checkout \
  -H "Origin: https://grimoireglam.com" -H "Access-Control-Request-Method: POST" | grep -i access-control-allow-origin
curl -s -X POST https://checkout.grimoireglam.com/checkout \
  -H "Origin: https://evil.example" -H "Content-Type: application/json" -d '{}'   # -> 403 "Origin not allowed"

# Square token works, without creating an order: a well-formed but fake item id
curl -s -X POST https://checkout.grimoireglam.com/checkout \
  -H "Origin: https://grimoireglam.com" -H "Content-Type: application/json" \
  -d '{"lines":[{"variationId":"AAAAAAAAAAAAAAAAAAAAAAAA","quantity":1}]}'
```
Reading the last result:
- `409 ... no longer available`: **good**. The worker reached Square with a valid token and Square doesn't know that id.
- `502 We could not reach checkout...`: Square rejected the call. The token is wrong, expired, or missing. Check `npx wrangler tail` for the logged Square error (do not paste token values) and report.
- `500`: worker bug; capture the `wrangler tail` output and report.

DNS checks (propagation can take minutes; GitHub certificate up to an hour):
```bash
dig +short grimoireglam.com A        # four 185.199.x.153 addresses
dig +short www.grimoireglam.com CNAME  # haloman363.github.io.
```
`https://grimoireglam.com` will not serve the site until the owner merges to `main`, sets Pages source to GitHub Actions, and runs the workflow. Until then a GitHub 404 on the domain is expected.

## Report back
Give the owner, in this order:
1. Worker: deployed yes/no, URL, version/deployment id, any warnings.
2. Secret: present yes/no (never the value).
3. DNS: table of records created/changed/deleted with IDs, and anything left untouched on purpose.
4. Verification results from Task 4, pass/fail for each check.
5. **Left for the owner**: set GitHub Pages source to GitHub Actions, add the `SQUARE_ACCESS_TOKEN` GitHub Actions secret if not done, merge the branch to `main`, run the "Sync Square and deploy" workflow, tick Enforce HTTPS once offered, decide shipping fees.
6. Rollback: `npx wrangler delete` for the worker; delete the DNS record IDs you created (and recreate any you deleted, from the before-table).
