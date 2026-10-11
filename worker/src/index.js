// Cloudflare Worker: the only server-side piece of the storefront.
//
//   POST /checkout          cart -> Square-hosted checkout link   (the secret Square token lives here, never in the site)
//   POST /square-webhook    Square catalog/inventory change -> triggers the GitHub rebuild (optional)
//   GET  /health            liveness check
//
// Prices are never taken from the browser: the order is built from Square catalog IDs, so Square
// prices it, applies your tax settings and decrements inventory exactly like a Square Online order.

const SQUARE_VERSION = '2024-10-17';
const ID = /^[A-Za-z0-9_-]{10,40}$/;
const MAX_LINES = 20;
const MAX_QTY = 10;
const MAX_NOTE = 140;

const squareBase = (env) => (env.SQUARE_ENV === 'sandbox' ? 'https://connect.squareupsandbox.com' : 'https://connect.squareup.com');

class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

function corsHeaders(request, env) {
  const origin = request.headers.get('Origin');
  const allowed = (env.ALLOWED_ORIGINS || '').split(',').map((s) => s.trim()).filter(Boolean);
  const headers = { Vary: 'Origin' };
  if (origin && allowed.includes(origin)) {
    headers['Access-Control-Allow-Origin'] = origin;
    headers['Access-Control-Allow-Methods'] = 'POST, OPTIONS';
    headers['Access-Control-Allow-Headers'] = 'Content-Type';
    headers['Access-Control-Max-Age'] = '86400';
  }
  return headers;
}

const json = (body, status, extra = {}) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', ...extra } });

export function validateLines(input) {
  if (!input || !Array.isArray(input.lines) || input.lines.length === 0) throw new HttpError(400, 'Your cart is empty.');
  if (input.lines.length > MAX_LINES) throw new HttpError(400, 'Too many items in one order.');
  return input.lines.map((l) => {
    if (!l || typeof l.variationId !== 'string' || !ID.test(l.variationId)) throw new HttpError(400, 'Invalid item.');
    const modifierIds = Array.isArray(l.modifierIds) ? l.modifierIds : [];
    if (modifierIds.length > 10 || modifierIds.some((m) => typeof m !== 'string' || !ID.test(m))) throw new HttpError(400, 'Invalid option.');
    const quantity = Number(l.quantity);
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QTY) throw new HttpError(400, 'Invalid quantity.');
    const note = typeof l.note === 'string' ? l.note.replace(/[\u0000-\u001f]/g, ' ').trim().slice(0, MAX_NOTE) : '';
    return { variationId: l.variationId, modifierIds, quantity, note };
  });
}

async function square(env, fetchImpl, method, path, body) {
  const res = await fetchImpl(squareBase(env) + path, {
    method,
    headers: { Authorization: `Bearer ${env.SQUARE_ACCESS_TOKEN}`, 'Square-Version': SQUARE_VERSION, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    console.error('Square error', method, path, res.status, JSON.stringify(data.errors ?? data));
    throw new HttpError(502, 'We could not reach checkout right now. Please try again in a moment.');
  }
  return data;
}

// The browser only sends option IDs; make sure each one really belongs to that item and that required / maximum
// selections are respected, so an option (and its price) from another product can never be attached to an order.
async function checkModifiers(env, fetchImpl, lines, variations, items) {
  const listIds = [...new Set([...items.values()].flatMap((i) => (i.item_data?.modifier_list_info ?? []).map((m) => m.modifier_list_id)))];
  const lists = new Map();
  if (listIds.length) {
    const r = await square(env, fetchImpl, 'POST', '/v2/catalog/batch-retrieve', { object_ids: listIds });
    for (const o of r.objects ?? []) lists.set(o.id, o);
  }
  const bad = () => new HttpError(400, 'One of the options in your cart is not available for that item.');
  for (const l of lines) {
    const item = items.get(variations.get(l.variationId).item_variation_data.item_id);
    const infos = (item?.item_data?.modifier_list_info ?? []).filter((m) => m.enabled !== false);
    const owner = new Map(); // modifier id -> list id
    for (const info of infos) {
      for (const m of lists.get(info.modifier_list_id)?.modifier_list_data?.modifiers ?? []) if (!m.is_deleted) owner.set(m.id, info.modifier_list_id);
    }
    const counts = new Map();
    for (const id of l.modifierIds) {
      const listId = owner.get(id);
      if (!listId) throw bad();
      counts.set(listId, (counts.get(listId) ?? 0) + 1);
    }
    for (const info of infos) {
      const data = lists.get(info.modifier_list_id)?.modifier_list_data;
      if (!data || data.modifier_type === 'TEXT') continue;
      const n = counts.get(info.modifier_list_id) ?? 0;
      const min = info.min_selected_modifiers ?? 0;
      const max = info.max_selected_modifiers ?? -1;
      if (n < min || (max >= 0 && n > max)) throw new HttpError(400, `Please choose an option for "${data.name}".`);
    }
  }
}

// Reject lines that are unknown, not sellable, or exceed what is in stock (the static site can be a few minutes stale).
async function checkAvailability(env, fetchImpl, lines) {
  const ids = [...new Set(lines.map((l) => l.variationId))];
  const cat = await square(env, fetchImpl, 'POST', '/v2/catalog/batch-retrieve', { object_ids: ids, include_related_objects: true });
  const variations = new Map((cat.objects ?? []).map((o) => [o.id, o]));
  const items = new Map((cat.related_objects ?? []).filter((o) => o.type === 'ITEM').map((o) => [o.id, o]));
  const wanted = new Map();
  for (const l of lines) wanted.set(l.variationId, (wanted.get(l.variationId) ?? 0) + l.quantity);

  const tracked = [];
  for (const id of ids) {
    const v = variations.get(id);
    if (!v || v.is_deleted || v.type !== 'ITEM_VARIATION') throw new HttpError(409, 'One of the items in your cart is no longer available.');
    if (v.item_variation_data.sellable === false) throw new HttpError(409, 'One of the items in your cart is no longer available.');
    if (v.item_variation_data.track_inventory) tracked.push(id);
  }
  await checkModifiers(env, fetchImpl, lines, variations, items);
  if (!tracked.length) return;

  const counts = new Map();
  const inv = await square(env, fetchImpl, 'POST', '/v2/inventory/counts/batch-retrieve', {
    catalog_object_ids: tracked,
    location_ids: [env.SQUARE_LOCATION_ID],
    states: ['IN_STOCK'],
  });
  for (const c of inv.counts ?? []) counts.set(c.catalog_object_id, Number(c.quantity));
  for (const id of tracked) {
    if ((counts.get(id) ?? 0) < wanted.get(id)) {
      const name = variations.get(id).item_variation_data.name;
      throw new HttpError(409, `Sorry, "${name}" just sold out or there is not enough in stock. Please update your cart.`);
    }
  }
}

export async function createCheckout(request, env, fetchImpl = fetch) {
  let body;
  try {
    body = await request.json();
  } catch {
    throw new HttpError(400, 'Invalid request.');
  }
  const lines = validateLines(body);
  await checkAvailability(env, fetchImpl, lines);

  const order = {
    location_id: env.SQUARE_LOCATION_ID,
    line_items: lines.map((l) => ({
      catalog_object_id: l.variationId,
      quantity: String(l.quantity),
      ...(l.modifierIds.length && { modifiers: l.modifierIds.map((id) => ({ catalog_object_id: id })) }),
      // The custom sizing code (from the sizing tool) travels with the line item so it shows on the order in Square.
      ...(l.note && { note: l.note }),
    })),
    pricing_options: { auto_apply_taxes: true },
  };
  const data = await square(env, fetchImpl, 'POST', '/v2/online-checkout/payment-links', {
    idempotency_key: crypto.randomUUID(),
    order,
    checkout_options: {
      ask_for_shipping_address: true,
      allow_tipping: false,
      ...(env.REDIRECT_URL && { redirect_url: env.REDIRECT_URL }),
    },
  });
  const url = data.payment_link?.url || data.payment_link?.long_url;
  if (!url) throw new HttpError(502, 'Checkout could not be started.');
  return url;
}

// ---- optional: Square webhook -> GitHub rebuild ----
export async function verifySquareSignature(rawBody, signature, env) {
  if (!signature || !env.SQUARE_WEBHOOK_SIGNATURE_KEY || !env.SQUARE_WEBHOOK_URL) return false;
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(env.SQUARE_WEBHOOK_SIGNATURE_KEY), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const mac = await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(env.SQUARE_WEBHOOK_URL + rawBody));
  const expected = btoa(String.fromCharCode(...new Uint8Array(mac)));
  if (expected.length !== signature.length) return false;
  let diff = 0;
  for (let i = 0; i < expected.length; i++) diff |= expected.charCodeAt(i) ^ signature.charCodeAt(i);
  return diff === 0;
}

async function triggerRebuild(env, fetchImpl) {
  const res = await fetchImpl(`https://api.github.com/repos/${env.GITHUB_REPO}/dispatches`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${env.GITHUB_TOKEN}`,
      Accept: 'application/vnd.github+json',
      'User-Agent': 'grimoire-glam-worker',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ event_type: 'square-catalog-updated' }),
  });
  if (!res.ok) console.error('GitHub dispatch failed', res.status, await res.text());
}

export async function handle(request, env, fetchImpl = fetch, ctx) {
  const { pathname } = new URL(request.url);
  const cors = corsHeaders(request, env);
  try {
    if (pathname === '/health') return json({ ok: true }, 200, cors);
    if (pathname === '/checkout') {
      if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers: cors });
      if (request.method !== 'POST') return json({ error: 'Method not allowed' }, 405, cors);
      if (!cors['Access-Control-Allow-Origin']) return json({ error: 'Origin not allowed' }, 403, cors);
      return json({ url: await createCheckout(request, env, fetchImpl) }, 200, cors);
    }
    if (pathname === '/square-webhook' && request.method === 'POST') {
      const raw = await request.text();
      if (!(await verifySquareSignature(raw, request.headers.get('x-square-hmacsha256-signature'), env))) return json({ error: 'Bad signature' }, 401);
      const work = triggerRebuild(env, fetchImpl);
      if (ctx?.waitUntil) ctx.waitUntil(work);
      else await work;
      return json({ ok: true }, 200);
    }
    return json({ error: 'Not found' }, 404, cors);
  } catch (e) {
    if (e instanceof HttpError) return json({ error: e.message }, e.status, cors);
    console.error('Unhandled', e);
    return json({ error: 'Something went wrong.' }, 500, cors);
  }
}

export default { fetch: (request, env, ctx) => handle(request, env, fetch, ctx) };
