#!/usr/bin/env node
// Pulls the live catalog (items, prices, modifiers, photos, stock) from Square and writes
//   data/products.json            what the site is built from
//   assets/img/products/<id>.webp  full size (<=1200px) photo   <id>-sm.webp  520px thumbnail (cards display at most ~255px wide, 2x = ~510)
//
// Usage:
//   SQUARE_ACCESS_TOKEN=... node scripts/sync-catalog.mjs          live, from Square
//   node scripts/sync-catalog.mjs --from-file saved-catalog.json   offline, from a saved API response
//
// The token only needs read access (ITEMS_READ, INVENTORY_READ). Set SQUARE_ENV=sandbox to use the sandbox.

import { readFile, writeFile, mkdir, readdir, rm, access } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';
import { transformCatalog, imageUrlMap } from './lib/transform.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const imgDir = path.join(root, 'assets/img/products');
const site = JSON.parse(await readFile(path.join(root, 'config/site.json'), 'utf8'));
const overrides = JSON.parse(await readFile(path.join(root, 'config/overrides.json'), 'utf8'));
const locationId = process.env.SQUARE_LOCATION_ID || site.squareLocationId;

const args = process.argv.slice(2);
const fileIdx = args.indexOf('--from-file');

async function loadFromSquare() {
  const token = process.env.SQUARE_ACCESS_TOKEN;
  if (!token) throw new Error('SQUARE_ACCESS_TOKEN is not set (or pass --from-file <saved response>)');
  const base = process.env.SQUARE_ENV === 'sandbox' ? 'https://connect.squareupsandbox.com' : 'https://connect.squareup.com';
  const call = async (method, url, body) => {
    const res = await fetch(base + url, {
      method,
      headers: { Authorization: `Bearer ${token}`, 'Square-Version': '2024-10-17', 'Content-Type': 'application/json' },
      body: body ? JSON.stringify(body) : undefined,
    });
    const json = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(`Square ${method} ${url} -> ${res.status} ${JSON.stringify(json.errors ?? json)}`);
    return json;
  };

  const items = [];
  const related = new Map();
  let cursor;
  do {
    const r = await call('POST', '/v2/catalog/search', {
      object_types: ['ITEM'],
      include_related_objects: true,
      limit: 100,
      ...(cursor && { cursor }),
    });
    items.push(...(r.objects ?? []));
    for (const o of r.related_objects ?? []) related.set(o.id, o);
    cursor = r.cursor;
  } while (cursor);

  const categories = [];
  cursor = undefined;
  do {
    const r = await call('GET', `/v2/catalog/list?types=CATEGORY${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`);
    categories.push(...(r.objects ?? []));
    cursor = r.cursor;
  } while (cursor);

  const variationIds = items.flatMap((o) => (o.item_data.variations ?? []).map((v) => v.id));
  const counts = {};
  for (let n = 0; n < variationIds.length; n += 500) {
    let c;
    do {
      const r = await call('POST', '/v2/inventory/counts/batch-retrieve', {
        catalog_object_ids: variationIds.slice(n, n + 500),
        location_ids: [locationId],
        states: ['IN_STOCK'],
        ...(c && { cursor: c }),
      });
      for (const k of r.counts ?? []) counts[k.catalog_object_id] = Number(k.quantity);
      c = r.cursor;
    } while (c);
  }
  return { items, related: [...related.values()], categories, counts };
}

async function loadFromFile(file) {
  const j = JSON.parse(await readFile(file, 'utf8'));
  return { items: j.objects ?? j.items, related: j.related_objects ?? j.related, categories: j.categories ?? [], counts: j.counts ?? {} };
}

const exists = (p) => access(p).then(() => true, () => false);

async function fetchImage(url, dest) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url} -> ${res.status}`);
  const buf = Buffer.from(await res.arrayBuffer());
  const img = sharp(buf).rotate();
  await img.clone().resize(1200, 1200, { fit: 'inside', withoutEnlargement: true }).webp({ quality: 78 }).toFile(`${dest}.webp`);
  await img.clone().resize(520, 520, { fit: 'inside', withoutEnlargement: true }).webp({ quality: 70 }).toFile(`${dest}-sm.webp`);
}

const raw = fileIdx >= 0 ? await loadFromFile(args[fileIdx + 1]) : await loadFromSquare();
const data = transformCatalog({ ...raw, overrides, lowStockThreshold: site.lowStockThreshold, locationId });

// Photos: download any we do not have yet, drop ones no longer referenced.
await mkdir(imgDir, { recursive: true });
const urls = imageUrlMap(raw.related);
const wanted = new Set(data.products.flatMap((p) => p.images));
let downloaded = 0;
const queue = [...wanted];
await Promise.all(
  Array.from({ length: 6 }, async () => {
    while (queue.length) {
      const id = queue.pop();
      if (await exists(path.join(imgDir, `${id}.webp`)) && (await exists(path.join(imgDir, `${id}-sm.webp`)))) continue;
      try {
        await fetchImage(urls.get(id), path.join(imgDir, id));
        downloaded++;
      } catch (e) {
        console.warn(`photo ${id} failed: ${e.message}`);
        wanted.delete(id);
      }
    }
  }),
);
for (const p of data.products) p.images = p.images.filter((id) => wanted.has(id));
for (const f of await readdir(imgDir)) {
  // drop photos no longer in the catalog, and any leftover non-webp files from the old JPEG pipeline
  if (!f.endsWith('.webp') || !wanted.has(f.replace(/(-sm)?\.webp$/, ''))) await rm(path.join(imgDir, f));
}

// Keep the file stable between runs when nothing changed so the workflow does not make empty commits.
const out = path.join(root, 'data/products.json');
const prev = (await exists(out)) ? JSON.parse(await readFile(out, 'utf8')) : null;
const next = { currency: 'USD', ...data };
if (prev && JSON.stringify({ ...prev, generatedAt: 0 }) === JSON.stringify({ ...next, generatedAt: 0 })) {
  console.log(`No catalog changes (${data.products.length} products).`);
} else {
  await mkdir(path.dirname(out), { recursive: true });
  await writeFile(out, JSON.stringify({ ...next, generatedAt: new Date().toISOString() }, null, 2) + '\n');
  console.log(`Wrote ${data.products.length} products, ${downloaded} new photos.`);
}
