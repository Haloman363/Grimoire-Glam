import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { transformCatalog, slugify } from './lib/transform.mjs';

const fx = JSON.parse(readFileSync(new URL('./fixtures/catalog.json', import.meta.url), 'utf8'));
const overrides = JSON.parse(readFileSync(new URL('../config/overrides.json', import.meta.url), 'utf8'));
const run = (patch = {}) =>
  transformCatalog({ items: fx.objects, related: fx.related_objects, categories: fx.categories, counts: fx.counts, overrides, lowStockThreshold: 3, locationId: 'L4YE869D48SSP', ...patch });

test('slugify handles punctuation and accents', () => {
  assert.equal(slugify("The Fool's Decree"), 'the-fool-s-decree');
  assert.equal(slugify('Noir et Blanc — The Ribbon Collection'), 'noir-et-blanc-the-ribbon-collection');
  assert.equal(slugify('Café'), 'cafe');
});

test('real catalog: 16 visible products, categories and units', () => {
  const d = run();
  assert.equal(d.products.length, 16);
  assert.ok(d.categories.includes('Seasonal Sets'));
  const uranium = d.products.find((p) => p.slug === 'uranium-nails');
  assert.equal(uranium.category, 'Seasonal Sets');
  assert.equal(uranium.variations[0].unit, 'Set');
  assert.equal(uranium.variations[0].priceCents, 3500);
  assert.equal(d.products.find((p) => p.slug === 'beetlejuice').name.startsWith('Beatlejuice'), true);
});

test('modifier lists keep the custom sizing text field', () => {
  const d = run();
  const text = Object.values(d.modifierLists).find((m) => m.kind === 'text');
  assert.equal(text.maxLength, 140);
  const grimoire = d.products.find((p) => p.slug === 'the-grimoire');
  assert.equal(grimoire.modifierLists.length, 3);
});

test('trackStock false ignores counts entirely', () => {
  const v = run({ trackStock: false }).products.flatMap((p) => p.variations);
  assert.equal(v.every((x) => x.stock === 'untracked' && x.quantity === null), true);
});

test('stock states follow inventory counts', () => {
  const grimoire = (d) => d.products.find((p) => p.slug === 'the-grimoire').variations;
  assert.equal(grimoire(run())[0].stock, 'low');
  const id = grimoire(run())[0].id;
  assert.equal(grimoire(run({ counts: { ...fx.counts, [id]: 0 } }))[0].stock, 'out');
  assert.equal(grimoire(run({ counts: { ...fx.counts, [id]: 10 } }))[0].stock, 'in');
});

test('hidden, archived and deleted items are dropped; new items appear automatically', () => {
  const items = structuredClone(fx.objects);
  items[0].item_data.ecom_visibility = 'HIDDEN';
  items[1].item_data.is_archived = true;
  items[2].is_deleted = true;
  assert.equal(run({ items }).products.length, 13);

  const extra = structuredClone(fx.objects[0]);
  extra.id = 'NEWITEMNEWITEMNEWITEM01';
  extra.item_data.name = 'Brand New Set';
  extra.item_data.variations = extra.item_data.variations.map((v, n) => ({ ...v, id: `NEWVAR${n}NEWVARNEWVAR01` }));
  const d = run({ items: [...fx.objects, extra] });
  assert.ok(d.products.some((p) => p.slug === 'brand-new-set'));
});

test('items not on the website channel (in-person only) are dropped', () => {
  const WEB = 'CH_WEB', POS = 'CH_POS';
  const items = structuredClone(fx.objects);
  items[0].item_data.channels = [WEB, POS];
  items[1].item_data.channels = [POS];
  const d = run({ items, websiteChannelId: WEB });
  assert.equal(d.products.length, fx.objects.length - 1);
  assert.ok(!d.products.some((p) => p.name === items[1].item_data.name));
  // without a channel id (or without channel data on an item) nothing is filtered
  assert.equal(run({ items }).products.length, fx.objects.length);
});

test('duplicate names get distinct slugs', () => {
  const a = structuredClone(fx.objects[0]);
  a.id = 'DUPLICATEDUPLICATEDUP01';
  const d = run({ items: [fx.objects[0], a] });
  assert.equal(new Set(d.products.map((p) => p.slug)).size, 2);
});

test('no em dashes survive in site data, and prose reads cleanly', async () => {
  const { noEmDash, noEmDashName } = await import('./lib/text.mjs');
  assert.equal(noEmDashName('Noir et Blanc — The Ribbon Collection'), 'Noir et Blanc - The Ribbon Collection');
  assert.equal(noEmDash('Black and white — the oldest argument'), 'Black and white, the oldest argument');
  assert.equal(noEmDash('a&mdash;b'), 'a, b');
  assert.equal(JSON.stringify(run()).includes('—'), false);
});

test('output is stable when Square returns related objects in a different order', () => {
  const a = run();
  const b = run({ related: [...fx.related_objects].reverse() });
  assert.equal(JSON.stringify(a), JSON.stringify(b));
  assert.deepEqual(Object.values(a.modifierLists).map((m) => m.name.slice(0, 4)), ['Size', 'Leng', 'Past']);
});
