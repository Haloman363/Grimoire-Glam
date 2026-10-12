// Pure data shaping: raw Square catalog objects -> the compact JSON the site is built from.
// No network or filesystem access here so it can be unit tested against saved API responses.

import { noEmDash, noEmDashName } from './text.mjs';

export function slugify(s) {
  return String(s)
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function stockFor(variationData, countsByVariation, lowThreshold, trackStock) {
  if (!trackStock || !variationData.track_inventory) return { stock: 'untracked', quantity: null };
  const qty = Number(countsByVariation.get(variationData.__id) ?? 0);
  if (qty <= 0) return { stock: 'out', quantity: 0 };
  return { stock: qty <= lowThreshold ? 'low' : 'in', quantity: qty };
}

/**
 * @param {object} input
 * @param {object[]} input.items            ITEM catalog objects
 * @param {object[]} input.related          related_objects from the search (IMAGE, MODIFIER_LIST, MEASUREMENT_UNIT)
 * @param {object[]} input.categories       CATEGORY catalog objects
 * @param {Map<string,number>|object} input.counts   variationId -> in-stock quantity at the selling location
 * @param {object} input.overrides          config/overrides.json
 * @param {number} input.lowStockThreshold
 * @param {boolean} input.trackStock        false = ignore Square counts; nothing is ever low or sold out
 * @param {string} input.locationId         only variations sellable at this location are kept
 */
export function transformCatalog({ items, related, categories, counts, overrides = {}, lowStockThreshold = 3, trackStock = true, locationId }) {
  const byId = new Map(related.map((o) => [o.id, o]));
  const countMap = counts instanceof Map ? counts : new Map(Object.entries(counts ?? {}));
  const catName = new Map(categories.filter((c) => !c.is_deleted).map((c) => [c.id, c.category_data.name]));
  const catOrder = categories
    .filter((c) => !c.is_deleted && c.category_data.is_top_level !== false)
    .sort((a, b) => (a.category_data.parent_category?.ordinal ?? 0) - (b.category_data.parent_category?.ordinal ?? 0))
    .map((c) => c.category_data.name);

  let modifierLists = {};
  for (const o of related) {
    if (o.type !== 'MODIFIER_LIST' || o.is_deleted) continue;
    const m = o.modifier_list_data;
    modifierLists[o.id] = {
      name: noEmDashName(m.name),
      kind: m.modifier_type === 'TEXT' ? 'text' : 'list',
      maxLength: m.max_length ?? null,
      options: (m.modifiers ?? [])
        .filter((x) => !x.is_deleted)
        .sort((a, b) => (a.modifier_data.ordinal ?? 0) - (b.modifier_data.ordinal ?? 0))
        .map((x) => ({ id: x.id, name: noEmDashName(x.modifier_data.name), priceCents: x.modifier_data.price_money?.amount ?? 0 })),
    };
  }

  // Square returns related objects in varying order; sort so unchanged catalogs produce identical output
  // (otherwise the sync workflow would commit on every run).
  const listOrder = (id) => byId.get(id)?.modifier_list_data?.ordinal ?? 0;
  modifierLists = Object.fromEntries(
    Object.entries(modifierLists).sort(([a], [b]) => listOrder(a) - listOrder(b) || (a < b ? -1 : 1)),
  );

  const usedSlugs = new Set();
  const products = [];
  for (const o of items) {
    const i = o.item_data;
    if (o.is_deleted || i.is_archived || i.ecom_visibility === 'HIDDEN' || i.ecom_visibility === 'UNINDEXED') continue;
    if (i.ecom_visibility && i.ecom_visibility !== 'VISIBLE') continue;
    const ov = overrides.items?.[o.id] ?? {};

    const variations = (i.variations ?? [])
      .filter((v) => !v.is_deleted && v.item_variation_data.sellable !== false)
      .filter((v) => {
        if (!locationId) return true;
        if (v.absent_at_location_ids?.includes(locationId)) return false;
        return v.present_at_all_locations || v.present_at_location_ids?.includes(locationId);
      })
      .map((v) => {
        const d = { ...v.item_variation_data, __id: v.id };
        const unit = byId.get(d.measurement_unit_id)?.measurement_unit_data?.measurement_unit?.custom_unit?.name ?? null;
        return {
          id: v.id,
          name: noEmDashName(d.name),
          priceCents: d.price_money?.amount ?? null,
          unit,
          ...stockFor(d, countMap, lowStockThreshold, trackStock),
        };
      })
      .filter((v) => v.priceCents !== null);
    if (!variations.length) continue;

    let slug = ov.slug || slugify(i.name) || o.id.toLowerCase();
    if (usedSlugs.has(slug)) slug = `${slug}-${o.id.slice(0, 5).toLowerCase()}`;
    usedSlugs.add(slug);

    const catId = (i.categories ?? [])[0]?.id ?? i.category_id;
    const modifierListIds = (i.modifier_list_info ?? [])
      .filter((m) => m.enabled !== false && !m.hidden_from_customer && modifierLists[m.modifier_list_id])
      .sort((a, b) => (a.ordinal ?? 0) - (b.ordinal ?? 0))
      .map((m) => ({ id: m.modifier_list_id, min: m.min_selected_modifiers ?? 0, max: m.max_selected_modifiers ?? null }));

    products.push({
      id: o.id,
      slug,
      name: noEmDashName(i.name),
      description: noEmDash((i.description_plaintext ?? i.description ?? '').trim()),
      category: ov.category ?? (catName.get(catId) ? noEmDashName(catName.get(catId)) : null),
      variations,
      modifierLists: modifierListIds,
      images: (i.image_ids ?? []).filter((id) => byId.has(id)),
      updatedAt: o.updated_at,
    });
  }

  const categoryNames = [...new Set([...catOrder, ...products.map((p) => p.category).filter(Boolean)])].filter((c) =>
    products.some((p) => p.category === c),
  );

  return { modifierLists, categories: categoryNames, products };
}

export function imageUrlMap(related) {
  return new Map(related.filter((o) => o.type === 'IMAGE' && !o.is_deleted).map((o) => [o.id, o.image_data.url]));
}
