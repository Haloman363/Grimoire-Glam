import { test } from 'node:test';
import assert from 'node:assert/strict';
import { handle, validateLines, verifySquareSignature } from './src/index.js';

const env = { SQUARE_ACCESS_TOKEN: 'tok', SQUARE_LOCATION_ID: 'LOC', ALLOWED_ORIGINS: 'https://shop.example,https://www.shop.example', REDIRECT_URL: 'https://shop.example/thank-you/' };
const VAR = 'K6QQIFP7PP3XYPQZLRXPCRSC';
const MOD = 'CCVSAQOQ7SIM4KG6QD5NR7PH';
const post = (body, origin = 'https://shop.example') =>
  new Request('https://w.example/checkout', { method: 'POST', headers: { Origin: origin, 'Content-Type': 'application/json' }, body: JSON.stringify(body) });

function mockSquare({ tracked = true, qty = 1, sellable = true } = {}) {
  const calls = [];
  const f = async (url, init) => {
    const body = init.body ? JSON.parse(init.body) : null;
    calls.push({ url, body });
    const ok = (o) => new Response(JSON.stringify(o), { status: 200 });
    if (url.endsWith('/catalog/batch-retrieve')) return ok({ objects: [{ id: VAR, type: 'ITEM_VARIATION', item_variation_data: { name: 'Stiletto', track_inventory: tracked, sellable } }] });
    if (url.endsWith('/inventory/counts/batch-retrieve')) return ok({ counts: qty ? [{ catalog_object_id: VAR, quantity: String(qty) }] : [] });
    if (url.endsWith('/online-checkout/payment-links')) return ok({ payment_link: { url: 'https://square.link/u/abc' } });
    return new Response('{}', { status: 404 });
  };
  return { f, calls };
}

test('creates a payment link from catalog ids, never from browser prices', async () => {
  const { f, calls } = mockSquare();
  const res = await handle(post({ lines: [{ variationId: VAR, modifierIds: [MOD], note: ' L: T(15/12/16) ', quantity: 1, priceCents: 1 }] }), env, f);
  assert.equal(res.status, 200);
  assert.equal((await res.json()).url, 'https://square.link/u/abc');
  assert.equal(res.headers.get('Access-Control-Allow-Origin'), 'https://shop.example');
  const link = calls.find((c) => c.url.endsWith('/payment-links')).body;
  assert.deepEqual(link.order.line_items[0], { catalog_object_id: VAR, quantity: '1', modifiers: [{ catalog_object_id: MOD }], note: 'L: T(15/12/16)' });
  assert.equal(link.order.location_id, 'LOC');
  assert.equal(link.order.pricing_options.auto_apply_taxes, true);
  assert.equal(link.checkout_options.ask_for_shipping_address, true);
  assert.equal(JSON.stringify(link).includes('price'), false);
});

test('rejects sold-out and unknown items', async () => {
  const out = await handle(post({ lines: [{ variationId: VAR, quantity: 1 }] }), env, mockSquare({ qty: 0 }).f);
  assert.equal(out.status, 409);
  const unsellable = await handle(post({ lines: [{ variationId: VAR, quantity: 1 }] }), env, mockSquare({ sellable: false }).f);
  assert.equal(unsellable.status, 409);
  const tooMany = await handle(post({ lines: [{ variationId: VAR, quantity: 2 }] }), env, mockSquare({ qty: 1 }).f);
  assert.equal(tooMany.status, 409);
});

test('untracked items skip the inventory check', async () => {
  const { f, calls } = mockSquare({ tracked: false });
  const res = await handle(post({ lines: [{ variationId: VAR, quantity: 3 }] }), env, f);
  assert.equal(res.status, 200);
  assert.equal(calls.some((c) => c.url.includes('inventory')), false);
});

test('blocks other origins and bad input', async () => {
  assert.equal((await handle(post({ lines: [{ variationId: VAR, quantity: 1 }] }, 'https://evil.example'), env, mockSquare().f)).status, 403);
  assert.throws(() => validateLines({ lines: [] }));
  assert.throws(() => validateLines({ lines: [{ variationId: 'x', quantity: 1 }] }));
  assert.throws(() => validateLines({ lines: [{ variationId: VAR, quantity: 0 }] }));
  assert.throws(() => validateLines({ lines: [{ variationId: VAR, quantity: 11 }] }));
  assert.throws(() => validateLines({ lines: [{ variationId: VAR, quantity: 1, modifierIds: ['bad id!'] }] }));
  assert.equal(validateLines({ lines: [{ variationId: VAR, quantity: 1, note: 'x'.repeat(500) }] })[0].note.length, 140);
});

test('Square failures do not leak details to the browser', async () => {
  const f = async () => new Response(JSON.stringify({ errors: [{ code: 'UNAUTHORIZED', detail: 'secret detail' }] }), { status: 401 });
  const res = await handle(post({ lines: [{ variationId: VAR, quantity: 1 }] }), env, f);
  assert.equal(res.status, 502);
  assert.equal((await res.text()).includes('secret'), false);
});

test('webhook signature check and rebuild dispatch', async () => {
  const wenv = { SQUARE_WEBHOOK_SIGNATURE_KEY: 'sigkey', SQUARE_WEBHOOK_URL: 'https://w.example/square-webhook', GITHUB_REPO: 'o/r', GITHUB_TOKEN: 'gh' };
  const body = '{"type":"catalog.version.updated"}';
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode('sigkey'), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const sig = btoa(String.fromCharCode(...new Uint8Array(await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(wenv.SQUARE_WEBHOOK_URL + body)))));
  assert.equal(await verifySquareSignature(body, sig, wenv), true);
  assert.equal(await verifySquareSignature(body + ' ', sig, wenv), false);

  const seen = [];
  const f = async (url, init) => { seen.push({ url, init }); return new Response(null, { status: 204 }); };
  const ok = await handle(new Request('https://w.example/square-webhook', { method: 'POST', headers: { 'x-square-hmacsha256-signature': sig }, body }), wenv, f);
  assert.equal(ok.status, 200);
  assert.equal(seen[0].url, 'https://api.github.com/repos/o/r/dispatches');
  const bad = await handle(new Request('https://w.example/square-webhook', { method: 'POST', headers: { 'x-square-hmacsha256-signature': 'nope' }, body }), wenv, f);
  assert.equal(bad.status, 401);
});
