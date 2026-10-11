#!/usr/bin/env node
// Static site generator: data/products.json + config/ + src/ + assets/  ->  dist/
// Run `npm run sync` first (or let the GitHub workflow do it) so data/products.json is current.

import { readFile, writeFile, mkdir, cp, rm, access } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { slugify } from './lib/transform.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dist = path.join(root, 'dist');
const site = JSON.parse(await readFile(path.join(root, 'config/site.json'), 'utf8'));
const data = JSON.parse(await readFile(path.join(root, 'data/products.json'), 'utf8'));
const exists = (p) => access(p).then(() => true, () => false);

const cname = (await exists(path.join(root, 'CNAME'))) ? (await readFile(path.join(root, 'CNAME'), 'utf8')).trim() : '';
const siteUrl = process.env.SITE_URL || (cname ? `https://${cname}` : '');
// GitHub project pages live under /<repo>/; a custom domain (CNAME) lives at the root.
const basePath = (process.env.BASE_PATH ?? '').replace(/\/$/, '');

const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
const money = (cents) => `$${(cents / 100).toFixed(2)}`;
const url = (p) => `${basePath}${p}`;
const img = (id, small = false) => url(`/assets/img/products/${id}${small ? '-sm' : ''}.jpg`);
const categorySlug = (name) => slugify(name);

const mods = data.modifierLists;
const priceRange = (p) => {
  const prices = p.variations.map((v) => v.priceCents);
  return { min: Math.min(...prices), max: Math.max(...prices) };
};
const priceLabel = (p) => {
  const { min, max } = priceRange(p);
  const unit = p.variations[0].unit ? ` / ${p.variations[0].unit}` : '';
  return `${min === max ? '' : 'From '}${money(min)}${unit}`;
};
const stockOf = (p) => {
  if (p.variations.every((v) => v.stock === 'out')) return 'out';
  if (p.variations.some((v) => v.stock === 'in' || v.stock === 'untracked')) return 'in';
  return 'low';
};
const badge = (p) => ({ out: '<span class="badge badge-out">Sold out</span>', low: '<span class="badge">Low stock</span>', in: '' })[stockOf(p)];

const nav = [
  { label: 'Home', href: '/' },
  { label: 'Sizing Info', href: '/sizing-info/' },
  { label: 'Shop All', href: '/shop/' },
  { label: 'Custom Sizing', href: '/custom-sizing/' },
  {
    label: 'More',
    children: [
      { label: 'Nail Accessories', href: '/nail-accessories/' },
      ...(data.categories.includes('Seasonal Sets') ? [{ label: 'Seasonal Sets', href: `/collections/${categorySlug('Seasonal Sets')}/` }] : []),
      { label: 'Gift Card', href: site.giftCardUrl, external: true },
    ],
  },
];

const icon = {
  cart: '<svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2 3h3l2.5 12h11L21 7H6"/><circle cx="9" cy="20" r="1.5"/><circle cx="17" cy="20" r="1.5"/></svg>',
  mail: '<svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor" aria-hidden="true"><path d="M3 5h18a1 1 0 011 1v12a1 1 0 01-1 1H3a1 1 0 01-1-1V6a1 1 0 011-1zm9 7.2L4.5 7v10.5h15V7L12 12.2z"/></svg>',
  instagram: '<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5"/><circle cx="12" cy="12" r="4"/><circle cx="17.5" cy="6.5" r="1" fill="currentColor"/></svg>',
  pinterest: '<svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor" aria-hidden="true"><path d="M12 2a10 10 0 00-3.6 19.3c-.1-.8-.2-2 0-2.9l1.2-5s-.3-.6-.3-1.5c0-1.4.8-2.4 1.8-2.4.9 0 1.3.6 1.3 1.4 0 .9-.5 2.2-.8 3.4-.2 1 .5 1.8 1.5 1.8 1.8 0 3.2-1.9 3.2-4.6 0-2.4-1.7-4.1-4.2-4.1-2.9 0-4.5 2.1-4.5 4.3 0 .9.3 1.8.7 2.3.1.1.1.2.1.3l-.3 1.1c0 .2-.1.2-.3.1-1.3-.6-2.1-2.5-2.1-4 0-3.3 2.4-6.3 6.9-6.3 3.6 0 6.4 2.6 6.4 6 0 3.6-2.3 6.5-5.4 6.5-1.1 0-2.1-.6-2.4-1.2l-.7 2.5c-.2.9-.9 2-1.3 2.7A10 10 0 1012 2z"/></svg>',
  vimeo: '<svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor" aria-hidden="true"><path d="M22 7.4c-.1 2-1.5 4.7-4.200 8.200-2.800 3.600-5.100 5.400-7 5.400-1.200 0-2.200-1.100-3-3.300L6 12.100c-.6-2.200-1.200-3.300-1.900-3.300-.1 0-.7.300-1.600 1L2 8.900l2.600-2.300c1.200-1 2-1.600 2.700-1.700 1.400-.1 2.300.8 2.600 2.900.4 2.200.6 3.600.8 4.100.4 1.800.8 2.700 1.300 2.700.4 0 .9-.6 1.700-1.700.8-1.100 1.200-2 1.300-2.500.1-1-.3-1.500-1.300-1.500-.5 0-.9.100-1.400.3.900-3 2.700-4.500 5.200-4.400 1.900.1 2.800 1.300 2.500 3.600z"/></svg>',
};

const navHtml = nav
  .map((n) =>
    n.children
      ? `<li class="has-sub"><button type="button" aria-expanded="false" aria-haspopup="true">${n.label}</button><ul class="sub">${n.children
          .map((c) => `<li><a href="${c.external ? c.href : url(c.href)}"${c.external ? ' rel="noopener"' : ''}>${c.label}</a></li>`)
          .join('')}</ul></li>`
      : `<li><a href="${url(n.href)}">${n.label}</a></li>`,
  )
  .join('');

function layout({ title, description, path: pagePath, body, ogImage, bodyClass = '', jsonld, noindex = false }) {
  const fullTitle = pagePath === '/' ? `${site.name} | ${site.tagline}` : `${title} | ${site.name}`;
  const canonical = siteUrl ? `${siteUrl}${pagePath}` : '';
  const og = ogImage ? (siteUrl ? `${siteUrl}${ogImage}` : ogImage) : '';
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(fullTitle)}</title>
<meta name="description" content="${esc(description || site.description)}">
${noindex ? '<meta name="robots" content="noindex">' : ''}${canonical ? `<link rel="canonical" href="${canonical}">` : ''}
<meta property="og:site_name" content="${esc(site.name)}">
<meta property="og:title" content="${esc(fullTitle)}">
<meta property="og:description" content="${esc(description || site.description)}">
<meta property="og:type" content="${jsonld ? 'product' : 'website'}">
${canonical ? `<meta property="og:url" content="${canonical}">` : ''}${og ? `<meta property="og:image" content="${og}">` : ''}
<meta name="twitter:card" content="${og ? 'summary_large_image' : 'summary'}">
<link rel="icon" href="${url('/assets/favicon.svg')}" type="image/svg+xml">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Rock+Salt&family=Taviraj:ital,wght@0,300;0,400;0,600;1,300;1,400&display=swap">
<link rel="stylesheet" href="${url('/css/site.css')}">
${jsonld ? `<script type="application/ld+json">${JSON.stringify(jsonld).replace(/</g, '\\u003c')}</script>` : ''}
</head>
<body class="${bodyClass}" data-base="${basePath}" data-checkout-api="${esc(site.checkoutApi)}">
<header class="site-header">
  <div class="wrap bar">
    <a class="logo" href="${url('/')}">The Grimoire<br>Glam Co</a>
    <button class="menu-toggle" type="button" aria-label="Menu" aria-expanded="false"><span></span><span></span><span></span></button>
    <nav class="main-nav" aria-label="Main"><ul>${navHtml}</ul></nav>
    <a class="cart-link" href="${url('/cart/')}" aria-label="Cart">${icon.cart}<span class="cart-count" hidden>0</span></a>
  </div>
</header>
<main>
${body}
</main>
<footer class="site-footer">
  <div class="wrap foot">
    <p class="pay">Secure checkout by Square &middot; Apple Pay &middot; Google Pay &middot; Cash App Pay &middot; all major cards</p>
    <ul class="social">
      <li><a href="mailto:${esc(site.email)}" aria-label="Email">${icon.mail}</a></li>
      <li><a href="${esc(site.instagram)}" rel="noopener" aria-label="Instagram">${icon.instagram}</a></li>
      <li><a href="${esc(site.pinterest)}" rel="noopener" aria-label="Pinterest">${icon.pinterest}</a></li>
      <li><a href="${esc(site.vimeo)}" rel="noopener" aria-label="Vimeo">${icon.vimeo}</a></li>
    </ul>
  </div>
  <div class="legal"><p>&copy; ${new Date().getFullYear()} ${esc(site.name)}. All designs 3D modeled and printed by hand in Utah.</p></div>
</footer>
<script src="${url('/js/app.js')}" defer></script>
${bodyClass.split(/\s+/).includes('page-sizing') ?`<script src="${url('/js/sizing.js')}" defer></script>` : ''}
</body>
</html>
`;
}

const pages = []; // [path, html]
const add = (p, html) => pages.push([p, html]);

// ---------- cards ----------
function card(p) {
  const first = p.images[0];
  const image = first
    ? `<img src="${img(first, true)}" alt="${esc(p.name)}" loading="lazy" width="640" height="640">`
    : '<div class="ph" role="img" aria-label="Photo coming soon">?</div>';
  return `<li class="card" data-category="${esc(p.category ? categorySlug(p.category) : '')}">
  <a href="${url(`/shop/${p.slug}/`)}">
    <div class="card-img">${image}${badge(p)}</div>
    <h3>${esc(p.name)}</h3>
    <p class="price">${priceLabel(p)}</p>
  </a>
</li>`;
}
const grid = (list) => `<ul class="grid">${list.map(card).join('\n')}</ul>`;

// ---------- home ----------
const featured = data.products.filter((p) => p.images.length).slice(0, 6);
add(
  '/',
  layout({
    title: site.name,
    description: site.description,
    path: '/',
    ogImage: featured[0] ? img(featured[0].images[0]) : '',
    bodyClass: 'page-home',
    body: `<section class="hero"><div class="wrap">
  <h1>Welcome to Grimoire Glam</h1>
  <p class="sub">${esc(site.tagline)}<br>Resin Printed Press-on Nails</p>
</div></section>
<section class="band"><div class="wrap narrow"><div class="panel center">
  <p><em>Not printed on. Printed as.</em></p>
  <p>Every nail you see was 3D modeled by hand and resin printed in my garage in Utah. The base. The designs. Every raised detail. All of it &mdash; printed from scratch because I wanted to make something that didn't exist yet.</p>
  <p>Wearable sculpture for the ones who've always wanted more from their fingertips.</p>
  <p>Everything is made to order and with intention to details.</p>
  <p><em>Welcome to Grimoire Glam.</em></p>
  <p><a class="btn" href="${url('/shop/')}">Shop now</a></p>
</div></div></section>
${featured.length ? `<section class="wrap featured"><h2>Featured sets</h2>${grid(featured)}<p class="center"><a class="btn btn-ghost" href="${url('/shop/')}">View all sets</a></p></section>` : ''}`,
  }),
);

// ---------- shop ----------
const chips = ['<button type="button" class="chip is-active" data-filter="">All</button>']
  .concat(data.categories.map((c) => `<button type="button" class="chip" data-filter="${categorySlug(c)}">${esc(c)}</button>`))
  .join('');
add(
  '/shop/',
  layout({
    title: 'Shop All',
    description: `Shop every resin-printed press-on nail set from ${site.name}.`,
    path: '/shop/',
    bodyClass: 'page-shop',
    body: `<section class="wrap"><h1 class="page-title">Shop All</h1>
<div class="chips" role="group" aria-label="Filter by collection">${chips}</div>
${data.products.length ? grid(data.products) : '<p class="center">New sets are on the way.</p>'}
<p class="center empty" hidden>Nothing in this collection yet.</p></section>`,
  }),
);

// ---------- collections ----------
for (const c of data.categories) {
  const list = data.products.filter((p) => p.category === c);
  add(
    `/collections/${categorySlug(c)}/`,
    layout({
      title: c,
      description: `${c} from ${site.name}.`,
      path: `/collections/${categorySlug(c)}/`,
      bodyClass: 'page-shop',
      body: `<section class="wrap"><h1 class="page-title">${esc(c)}</h1>${grid(list)}</section>`,
    }),
  );
}

// ---------- product pages ----------
function productForm(p) {
  const variationControl =
    p.variations.length > 1
      ? `<div class="field"><label for="f-var">Shape</label><select id="f-var" name="variation" required>
  ${p.variations.map((v) => `<option value="${v.id}"${v.stock === 'out' ? ' disabled' : ''}>${esc(v.name)}${v.stock === 'out' ? ' (sold out)' : ''}</option>`).join('')}
</select></div>`
      : `<input type="hidden" name="variation" value="${p.variations[0].id}">`;

  const modControls = p.modifierLists
    .map((ref, n) => {
      const m = mods[ref.id];
      if (m.kind === 'text') {
        return `<div class="field"><label for="f-m${n}">${esc(m.name)}</label>
  <textarea id="f-m${n}" name="text:${ref.id}" rows="3" maxlength="${m.maxLength ?? 140}" placeholder="Optional unless you chose Custom size"></textarea>
  <p class="hint"><a href="${url('/custom-sizing/')}" target="_blank" rel="noopener">Open the custom sizing tool</a> to measure and generate your code.</p></div>`;
      }
      const required = ref.min >= 1;
      return `<div class="field"><label for="f-m${n}">${esc(m.name)}</label><select id="f-m${n}" name="mod:${ref.id}"${required ? ' required' : ''}>
  ${required ? '<option value="">Choose&hellip;</option>' : '<option value="">None</option>'}
  ${m.options.map((o) => `<option value="${o.id}">${esc(o.name)}${o.priceCents ? ` (+${money(o.priceCents)})` : ''}</option>`).join('')}
</select></div>`;
    })
    .join('\n');

  const soldOut = stockOf(p) === 'out';
  return `<form class="buy" novalidate>
  ${variationControl}
  ${modControls}
  <p class="form-error" role="alert" hidden></p>
  <button class="btn btn-buy" type="submit"${soldOut ? ' disabled' : ''}>${soldOut ? 'Sold out' : 'Add to cart'}</button>
  <p class="hint">Made to order. Shipping and tax are calculated at checkout.</p>
</form>`;
}

for (const p of data.products) {
  const paras = p.description
    ? p.description
        .split(/\n\s*\n/)
        .map((t) => `<p>${esc(t.trim()).replace(/\n/g, '<br>')}</p>`)
        .join('\n')
    : '';
  const gallery = p.images.length
    ? `<div class="gallery"><img class="main-img" src="${img(p.images[0])}" alt="${esc(p.name)}" width="1400" height="1400">
  ${p.images.length > 1 ? `<ul class="thumbs">${p.images.map((id, n) => `<li><button type="button" data-full="${img(id)}" aria-label="Photo ${n + 1}"><img src="${img(id, true)}" alt="" loading="lazy" width="120" height="120"></button></li>`).join('')}</ul>` : ''}
</div>`
    : '<div class="gallery"><div class="ph big" role="img" aria-label="Photo coming soon">?</div></div>';

  const clientData = {
    id: p.id,
    slug: p.slug,
    name: p.name,
    image: p.images[0] ? img(p.images[0], true) : null,
    variations: p.variations,
    modifierLists: p.modifierLists.map((r) => ({ ...r, ...mods[r.id] })),
  };
  const { min } = priceRange(p);
  const jsonld = {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: p.name,
    description: p.description.slice(0, 500),
    ...(p.images[0] && { image: [siteUrl ? `${siteUrl}${img(p.images[0])}` : img(p.images[0])] }),
    brand: { '@type': 'Brand', name: site.name },
    offers: {
      '@type': 'Offer',
      priceCurrency: data.currency,
      price: (min / 100).toFixed(2),
      availability: stockOf(p) === 'out' ? 'https://schema.org/OutOfStock' : 'https://schema.org/InStock',
      ...(siteUrl && { url: `${siteUrl}/shop/${p.slug}/` }),
    },
  };

  add(
    `/shop/${p.slug}/`,
    layout({
      title: p.name,
      description: p.description ? p.description.replace(/\s+/g, ' ').slice(0, 155) : `${p.name} by ${site.name}.`,
      path: `/shop/${p.slug}/`,
      ogImage: p.images[0] ? img(p.images[0]) : '',
      bodyClass: 'page-product',
      jsonld,
      body: `<section class="wrap product">
  <nav class="crumbs" aria-label="Breadcrumb"><a href="${url('/shop/')}">Shop All</a>${p.category ? ` / <a href="${url(`/collections/${categorySlug(p.category)}/`)}">${esc(p.category)}</a>` : ''}</nav>
  <div class="product-grid">
    ${gallery}
    <div class="info">
      <h1>${esc(p.name)}</h1>
      <p class="price big" id="price" data-base="${min}">${priceLabel(p)} ${badge(p)}</p>
      ${productForm(p)}
      <div class="desc">${paras}</div>
    </div>
  </div>
  <script type="application/json" id="product-data">${JSON.stringify(clientData).replace(/</g, '\\u003c')}</script>
</section>`,
    }),
  );
}

// ---------- cart ----------
add(
  '/cart/',
  layout({
    title: 'Your Cart',
    path: '/cart/',
    noindex: true,
    bodyClass: 'page-cart',
    body: `<section class="wrap narrow"><h1 class="page-title">Your Cart</h1>
<div id="cart-root"><p class="center">Loading your cart&hellip;</p></div>
<noscript><p class="center">The cart needs JavaScript. You can also email <a href="mailto:${esc(site.email)}">${esc(site.email)}</a> to order.</p></noscript>
</section>`,
  }),
);

// ---------- sizing info (chart) ----------
const standard = [
  ['XS', 10, 7, 8, 7, 6],
  ['S', 13, 11, 12, 11, 9],
  ['M', 15, 12, 13, 11, 9],
  ['L', 16, 13, 14, 12, 10],
  ['XL', 18, 14, 15, 13, 11],
];
const lengths = [['S', 18], ['M', 22], ['L', 25], ['XL', 30], ['XXL', 40]];
const shapes = ['Stiletto', 'Square', 'Almond', 'Coffin', 'Squoval'];
add(
  '/sizing-info/',
  layout({
    title: 'Sizing Info',
    description: 'Standard nail widths, shapes and lengths for Grimoire Glam press-on nails.',
    path: '/sizing-info/',
    bodyClass: 'page-sizing-info',
    body: `<section class="wrap narrow"><h1 class="page-title">Grimoire's Standard Sizing</h1>
<h2>Nail widths</h2>
<div class="table-wrap"><table><thead><tr><th>Size</th><th>Thumb</th><th>Index</th><th>Middle</th><th>Ring</th><th>Pinky</th></tr></thead><tbody>
${standard.map((r) => `<tr><th scope="row">${r[0]}</th>${r.slice(1).map((n) => `<td>${n} mm</td>`).join('')}</tr>`).join('\n')}
</tbody></table></div>
<p>If you don't fit any of these nail sizes, choose the <strong>Custom</strong> option in the size menu when selecting your nails, then use the <a href="${url('/custom-sizing/')}">custom sizing tool</a> to measure and generate your code.</p>
<h2>Nail shapes &amp; lengths</h2>
<div class="table-wrap"><table><thead><tr><th>Shape</th>${lengths.map(([s]) => `<th>${s}</th>`).join('')}</tr></thead><tbody>
${shapes.map((s) => `<tr><th scope="row">${s}</th>${lengths.map(([, mm]) => `<td>${mm} mm</td>`).join('')}</tr>`).join('\n')}
</tbody></table></div>
<p>These lengths are measured from the thumb. The other fingers are proportionally scaled. If you want longer lengths than I offer, include the length you'd like when ordering your custom size.</p>
<p class="center"><a class="btn" href="${url('/custom-sizing/')}">Custom size here</a></p>
</section>`,
  }),
);

// ---------- custom sizing tool ----------
const sizingRows = (side) =>
  ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']
    .map(
      (f, i) =>
        `<div class="srow"><div class="finger">${f}</div>${['w', 'a', 'l']
          .map((k) => `<div class="sfield"><input type="number" inputmode="decimal" min="1" max="40" id="${side}_${k}_${i}" placeholder="&mdash;" aria-label="${side === 'L' ? 'Left' : 'Right'} ${f} ${{ w: 'width', a: 'arch', l: 'length' }[k]} in millimeters"></div>`)
          .join('')}</div>`,
    )
    .join('');
add(
  '/custom-sizing/',
  layout({
    title: 'Custom Sizing',
    description: 'Measure once. Fits like it was cast for you. Generate your custom nail sizing code.',
    path: '/custom-sizing/',
    bodyClass: 'page-sizing',
    body: `<section class="wrap sizing-tool"><h1 class="page-title">Nail Sizing</h1>
<p class="tag">Measure once. Fits like it was cast for you.</p>
<details><summary>How to measure</summary><div class="howto">
  <p>You'll enter three numbers per nail, all in <b>millimeters (mm)</b>. Round to the nearest whole number &mdash; no decimals.</p>
  <p><b>Width</b> &mdash; measure straight across the <b>widest</b> part of your nail, from one side wall to the other.</p>
  <p><b>Arch</b> &mdash; measure across the <b>curved base</b> of your nail, right where it meets your cuticle.</p>
  <p><b>Length</b> &mdash; measure from your <b>cuticle to the tip</b> of your natural nail (how long your nail bed is).</p>
  <p>Do all ten, both hands &mdash; hands aren't always the same. When in doubt, round up.</p>
</div></details>
<div class="hand"><h2>Left Hand</h2><div class="sub">Thumb &rarr; Pinky &middot; all in mm</div>
  <div class="shead"><span>Finger</span><span>Width</span><span>Arch</span><span>Length</span></div>${sizingRows('L')}</div>
<div class="hand"><h2>Right Hand</h2><div class="sub">Thumb &rarr; Pinky &middot; all in mm</div>
  <button class="copybtn" id="copyLR" type="button">Copy Left &rarr; Right</button>
  <div class="shead"><span>Finger</span><span>Width</span><span>Arch</span><span>Length</span></div>${sizingRows('R')}</div>
<button class="generate" id="generate" type="button">Generate my code</button>
<div class="err" id="err" role="alert"></div>
<div class="out" id="out"><h3>Your sizing code</h3>
  <textarea class="code" id="code" rows="3" readonly></textarea>
  <div class="count" id="count"></div>
  <button class="copycode" id="copycode" type="button">Copy code</button>
  <p class="note"><b>Key:</b> T=thumb, I=index, M=middle, R=ring, P=pinky &middot; each reads <b>Width/Arch/Length</b> in mm. Paste this into the <b>sizing code</b> box on the product page, and choose <b>Custom</b> for size. I take it from there.</p>
  <p class="center"><a class="btn" href="${url('/shop/')}">Shop sets</a></p></div>
<div class="toast" id="toast" role="status">Copied!</div>
</section>`,
  }),
);

// ---------- accessories ----------
add(
  '/nail-accessories/',
  layout({
    title: 'Nail Accessories',
    description: '3D printed nail savers, nail storage and extras from The Grimoire Glam Co.',
    path: '/nail-accessories/',
    bodyClass: 'page-accessories',
    body: `<section class="wrap narrow"><h1 class="page-title">3D Printed Nail Accessories</h1>
<div class="acc"><h2>Nail Savers</h2><p>I don't know about you, but I absolutely HATE when I use my nails as tools. These products have helped save my nails from possible chipping, breaking, and just being overall worn down. Get these and they will help SAVE YOUR NAILS!!</p></div>
<div class="acc"><h2>Nail Storage</h2><p>Here are some cute nail storage items that will help you stay organized when switching sets or even taking a break from your Grimoire nails.</p></div>
<div class="acc"><h2>Extra Items</h2><p>I love designing and printing things to help them come to life. Here are other items that would just be fun to have in your home. I know I've loved them.</p></div>
<p class="center">New accessories are coming soon.</p></section>`,
  }),
);

// ---------- thank you (Square redirects here after payment) ----------
add(
  '/thank-you/',
  layout({
    title: 'Thank you',
    path: '/thank-you/',
    noindex: true,
    bodyClass: 'page-thanks',
    body: `<section class="wrap narrow center"><h1 class="page-title">Thank you</h1><p>Your order is in. Square is emailing your receipt, and your set goes into the print queue &mdash; everything here is made to order.</p><p>Questions about sizing or your order? <a href="mailto:${esc(site.email)}">${esc(site.email)}</a></p><p><a class="btn" href="${url('/shop/')}">Keep browsing</a></p></section>`,
  }),
);

// ---------- 404 ----------
add(
  '/404.html',
  layout({
    title: 'Page not found',
    path: '/404.html',
    noindex: true,
    body: `<section class="wrap narrow center"><h1 class="page-title">Lost in the stacks</h1><p>That page doesn't exist &mdash; maybe it was a set that sold out.</p><p><a class="btn" href="${url('/shop/')}">Browse the shop</a></p></section>`,
  }),
);

// ---------- write ----------
await rm(dist, { recursive: true, force: true });
for (const [p, html] of pages) {
  const file = p.endsWith('.html') ? path.join(dist, p) : path.join(dist, p, 'index.html');
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, html);
}
await cp(path.join(root, 'src/css'), path.join(dist, 'css'), { recursive: true });
await cp(path.join(root, 'src/js'), path.join(dist, 'js'), { recursive: true });
await cp(path.join(root, 'assets'), path.join(dist, 'assets'), { recursive: true });
if (cname) await writeFile(path.join(dist, 'CNAME'), cname + '\n');
await writeFile(path.join(dist, '.nojekyll'), '');
await writeFile(path.join(dist, 'robots.txt'), `User-agent: *\nAllow: /\n${siteUrl ? `Sitemap: ${siteUrl}/sitemap.xml\n` : ''}`);
if (siteUrl) {
  const urls = pages.map(([p]) => p).filter((p) => !p.endsWith('.html') && p !== '/cart/' && p !== '/thank-you/');
  await writeFile(
    path.join(dist, 'sitemap.xml'),
    `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls.map((p) => `  <url><loc>${siteUrl}${p}</loc></url>`).join('\n')}\n</urlset>\n`,
  );
}
console.log(`Built ${pages.length} pages from ${data.products.length} products into dist/`);
