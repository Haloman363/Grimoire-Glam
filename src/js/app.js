// Cart, product form, shop filters and nav. No dependencies, no build step.
(function () {
  'use strict';
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var KEY = 'grimoireCart.v1';
  var money = function (c) { return '$' + (c / 100).toFixed(2); };
  var base = document.body.getAttribute('data-base') || '';
  var checkoutApi = (document.body.getAttribute('data-checkout-api') || '').replace(/\/$/, '');

  // ---- cart storage (localStorage can be unavailable; fall back to memory) ----
  var memory = [];
  function load() {
    try { return JSON.parse(localStorage.getItem(KEY)) || []; } catch (e) { return memory; }
  }
  function save(lines) {
    memory = lines;
    try { localStorage.setItem(KEY, JSON.stringify(lines)); } catch (e) { /* ignore */ }
    badge();
  }
  function badge() {
    var n = load().reduce(function (s, l) { return s + l.qty; }, 0);
    $$('.cart-count').forEach(function (el) { el.textContent = n || ''; el.hidden = !n; });
    // the visible number must be part of the accessible name (WCAG label-in-name)
    $$('.cart-link').forEach(function (a) { a.setAttribute('aria-label', n ? 'Cart, ' + n + (n === 1 ? ' item' : ' items') : 'Cart'); });
  }
  if (document.body.classList.contains('page-thanks')) save([]); // order placed: empty the cart
  badge();

  // ---- nav ----
  var toggle = $('.menu-toggle');
  if (toggle) toggle.addEventListener('click', function () {
    var open = $('.main-nav').classList.toggle('open');
    toggle.setAttribute('aria-expanded', open);
  });
  $$('.has-sub > button').forEach(function (b) {
    b.addEventListener('click', function () {
      var li = b.parentNode, open = li.classList.toggle('open');
      b.setAttribute('aria-expanded', open);
    });
  });


  // ---- search (client side, over /search-index.json built from the Square catalog) ----
  var sBtn = $('.search-toggle'), sPanel = $('#search-panel');
  if (sBtn && sPanel) {
    var sInput = $('.search-input', sPanel), sList = $('.search-results', sPanel), index = null, loading = null;
    var loadIndex = function () {
      if (!loading) loading = fetch(base + '/search-index.json').then(function (r) { return r.json(); }).then(function (j) { index = j; }).catch(function () { index = []; });
      return loading;
    };
    var closeSearch = function () { sPanel.hidden = true; sBtn.setAttribute('aria-expanded', 'false'); };
    var openSearch = function () {
      sPanel.hidden = false; sBtn.setAttribute('aria-expanded', 'true'); sInput.focus(); loadIndex().then(renderResults);
    };
    var renderResults = function () {
      sList.textContent = '';
      var q = sInput.value.trim().toLowerCase();
      if (!q || !index) return;
      var terms = q.split(/\s+/);
      var hits = index.filter(function (p) { return terms.every(function (t) { return p.k.indexOf(t) !== -1; }); }).slice(0, 8);
      if (!hits.length) { var none = document.createElement('li'); none.className = 'search-empty'; none.textContent = 'No sets match \u201c' + sInput.value.trim() + '\u201d.'; sList.appendChild(none); return; }
      hits.forEach(function (p) {
        var li = document.createElement('li'), a = document.createElement('a');
        a.href = p.u; a.setAttribute('role', 'option');
        if (p.i) { var im = document.createElement('img'); im.src = p.i; im.alt = ''; im.width = 48; im.height = 48; a.appendChild(im); }
        var t = document.createElement('span'), n = document.createElement('span'), m = document.createElement('span');
        n.className = 'r-name'; n.textContent = p.n; m.className = 'r-meta'; m.textContent = p.p + (p.c ? ' \u00b7 ' + p.c : '');
        t.appendChild(n); t.appendChild(m); a.appendChild(t); li.appendChild(a); sList.appendChild(li);
      });
    };
    sBtn.addEventListener('click', function () { sPanel.hidden ? openSearch() : closeSearch(); });
    sInput.addEventListener('input', function () { loadIndex().then(renderResults); });
    sInput.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') { var first = $('a', sList); if (first) { e.preventDefault(); location.href = first.href; } }
      if (e.key === 'ArrowDown') { var a1 = $('a', sList); if (a1) { e.preventDefault(); a1.focus(); } }
    });
    sList.addEventListener('keydown', function (e) {
      var cur = document.activeElement; if (!cur || cur.tagName !== 'A') return;
      var links = $$('a', sList), i = links.indexOf(cur);
      if (e.key === 'ArrowDown' && links[i + 1]) { e.preventDefault(); links[i + 1].focus(); }
      if (e.key === 'ArrowUp') { e.preventDefault(); (i > 0 ? links[i - 1] : sInput).focus(); }
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !sPanel.hidden) { closeSearch(); sBtn.focus(); } });
    document.addEventListener('click', function (e) { if (!sPanel.hidden && !sPanel.contains(e.target) && !sBtn.contains(e.target)) closeSearch(); });
  }

  // ---- shop filter ----
  var chips = $$('.chip');
  if (chips.length) {
    var apply = function (f) {
      chips.forEach(function (c) { c.classList.toggle('is-active', c.getAttribute('data-filter') === f); });
      var shown = 0;
      $$('.card').forEach(function (c) {
        var show = !f || c.getAttribute('data-category') === f;
        c.hidden = !show; if (show) shown++;
      });
      var empty = $('.empty'); if (empty) empty.hidden = shown > 0;
      try { history.replaceState(null, '', f ? '#' + f : location.pathname); } catch (e) { /* ignore */ }
    };
    chips.forEach(function (c) { c.addEventListener('click', function () { apply(c.getAttribute('data-filter')); }); });
    var h = location.hash.slice(1);
    if (h && chips.some(function (c) { return c.getAttribute('data-filter') === h; })) apply(h);
  }

  // ---- product page ----
  var dataEl = $('#product-data');
  if (dataEl) {
    var p = JSON.parse(dataEl.textContent);
    var form = $('.buy');
    var err = $('.form-error', form);

    // Offer sizes saved from the custom sizing tool (this browser only).
    var sizeBox = form.querySelector('textarea[name^="text:"]');
    var savedSizes = null;
    ['grimoireSizes.v1', 'grimoireMarketSizes.v1'].forEach(function (k) {
      try {
        var one = JSON.parse(localStorage.getItem(k));
        if (one && one.code && (!savedSizes || (one.savedAt || 0) > (savedSizes.savedAt || 0))) savedSizes = one;
      } catch (e) {}
    });
    if (sizeBox && savedSizes && savedSizes.code) {
      var useBtn = document.createElement('button');
      useBtn.type = 'button';
      useBtn.className = 'savebtn';
      useBtn.textContent = 'Use my saved sizes';
      useBtn.addEventListener('click', function () {
        sizeBox.value = savedSizes.code;
        sizeBox.dispatchEvent(new Event('input', { bubbles: true }));
        useBtn.textContent = 'Saved sizes added';
      });
      sizeBox.parentNode.insertBefore(useBtn, sizeBox.nextSibling);
    }

    $$('.thumbs button').forEach(function (b, i) {
      if (!i) b.classList.add('is-active');
      b.addEventListener('click', function () {
        $('.main-img').src = b.getAttribute('data-full');
        $$('.thumbs button').forEach(function (x) { x.classList.toggle('is-active', x === b); });
      });
    });

    var currentVariation = function () {
      var id = form.elements.variation.value;
      return p.variations.filter(function (v) { return v.id === id; })[0];
    };
    var selected = function () {
      var out = { modifiers: [], note: '', text: null, customSize: false };
      p.modifierLists.forEach(function (m) {
        if (m.kind === 'text') {
          out.note = (form.elements['text:' + m.id].value || '').trim();
          out.text = m;
        } else {
          var id = form.elements['mod:' + m.id].value;
          var opt = m.options.filter(function (o) { return o.id === id; })[0];
          if (opt) {
            out.modifiers.push({ listId: m.id, listName: m.name, id: opt.id, name: opt.name, priceCents: opt.priceCents });
            if (/^custom/i.test(opt.name)) out.customSize = true;
          }
        }
      });
      return out;
    };
    var refreshPrice = function () {
      var v = currentVariation(); if (!v) return;
      var extra = selected().modifiers.reduce(function (s, m) { return s + m.priceCents; }, 0);
      var el = $('#price');
      el.firstChild.nodeValue = money(v.priceCents + extra) + (v.unit ? ' / ' + v.unit : '') + ' ';
    };
    form.addEventListener('change', refreshPrice);

    form.addEventListener('submit', function (e) {
      e.preventDefault();
      err.hidden = true;
      var v = currentVariation(), sel = selected();
      var fail = function (msg) { err.textContent = msg; err.hidden = false; };
      if (!v) return fail('Please choose a shape.');
      if (v.stock === 'out') return fail('That option is sold out.');
      for (var i = 0; i < p.modifierLists.length; i++) {
        var m = p.modifierLists[i];
        if (m.kind === 'list' && m.min >= 1 && !form.elements['mod:' + m.id].value) return fail('Please choose: ' + m.name + '.');
      }
      if (sel.customSize && !sel.note) return fail('You chose Custom size, so please paste your sizing code (or ten nail widths in mm).');

      var key = [v.id].concat(sel.modifiers.map(function (m) { return m.id; }), [sel.note]).join('|');
      var lines = load();
      var existing = lines.filter(function (l) { return l.key === key; })[0];
      var unit = v.priceCents + sel.modifiers.reduce(function (s, m) { return s + m.priceCents; }, 0);
      var cap = v.quantity == null ? 99 : Math.max(1, v.quantity);
      if (existing) {
        if (existing.qty >= cap) return fail('That is all we have in stock right now.');
        existing.qty++;
      } else {
        lines.push({
          key: key, slug: p.slug, name: p.name, image: p.image, variationId: v.id, variationName: v.name,
          modifiers: sel.modifiers, note: sel.note, qty: 1, unitCents: unit, cap: cap
        });
      }
      save(lines);
      var btn = $('.btn-buy', form), label = btn.textContent;
      btn.textContent = 'Added ✓'; btn.disabled = true;
      setTimeout(function () { btn.textContent = label; btn.disabled = false; }, 1400);
    });
  }

  // ---- cart page ----
  var root = $('#cart-root');
  if (root) {
    var el = function (tag, cls, text) { var n = document.createElement(tag); if (cls) n.className = cls; if (text != null) n.textContent = text; return n; };
    var renderCart = function () {
      var lines = load();
      root.textContent = '';
      if (!lines.length) {
        var empty = el('p', 'center', 'Your cart is empty. ');
        var a = el('a', null, 'Browse the shop'); a.href = base + '/shop/'; empty.appendChild(a);
        root.appendChild(empty); return;
      }
      var total = 0;
      lines.forEach(function (l, i) {
        total += l.unitCents * l.qty;
        var row = el('div', 'cart-line');
        if (l.image) { var im = el('img'); im.src = l.image; im.alt = ''; row.appendChild(im); } else row.appendChild(el('div', 'ph', '?'));
        var mid = el('div');
        var h = el('h3'); var link = el('a', null, l.name); link.href = base + '/shop/' + l.slug + '/'; h.appendChild(link); mid.appendChild(h);
        var ul = el('ul', 'opts');
        ul.appendChild(el('li', null, 'Shape: ' + l.variationName));
        l.modifiers.forEach(function (m) { ul.appendChild(el('li', null, m.listName + ': ' + m.name)); });
        if (l.note) ul.appendChild(el('li', null, 'Sizing: ' + l.note));
        mid.appendChild(ul);
        var q = el('div', 'qty');
        var minus = el('button', null, '−'); minus.type = 'button'; minus.setAttribute('aria-label', 'Decrease quantity');
        var plus = el('button', null, '+'); plus.type = 'button'; plus.setAttribute('aria-label', 'Increase quantity');
        minus.onclick = function () { if (l.qty > 1) { l.qty--; save(lines); renderCart(); } };
        plus.onclick = function () { if (l.qty < (l.cap || 99)) { l.qty++; save(lines); renderCart(); } };
        var rm = el('button', 'linkbtn', 'Remove'); rm.type = 'button';
        rm.onclick = function () { lines.splice(i, 1); save(lines); renderCart(); };
        q.appendChild(minus); q.appendChild(el('span', null, String(l.qty))); q.appendChild(plus); q.appendChild(rm);
        mid.appendChild(q);
        row.appendChild(mid);
        row.appendChild(el('div', 'line-total', money(l.unitCents * l.qty)));
        root.appendChild(row);
      });
      var t = el('div', 'cart-total'); t.appendChild(el('span', null, 'Subtotal')); t.appendChild(el('strong', null, money(total))); root.appendChild(t);
      root.appendChild(el('p', 'hint', (document.body.getAttribute('data-shipping') || 'Shipping is added at checkout.') + ' Tax, if any, is added at checkout. Everything is made to order.'));
      var foot = el('div', 'cart-foot');
      var go = el('button', 'btn', 'Checkout'); go.type = 'button';
      var msg = el('p', 'form-error'); msg.hidden = true; msg.setAttribute('role', 'alert');
      go.onclick = function () {
        msg.hidden = true;
        if (!checkoutApi) { msg.textContent = 'Checkout is not connected yet. Please email us to place your order.'; msg.hidden = false; return; }
        go.disabled = true; go.textContent = 'Taking you to Square…';
        fetch(checkoutApi + '/checkout', {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ lines: lines.map(function (l) { return { variationId: l.variationId, modifierIds: l.modifiers.map(function (m) { return m.id; }), note: l.note, quantity: l.qty }; }) })
        }).then(function (r) { return r.json().then(function (j) { if (!r.ok) throw new Error(j.error || 'Checkout failed'); return j; }); })
          .then(function (j) {
            // only ever leave for Square's hosted checkout, whatever the API returns
            if (!/^https:\/\/([a-z0-9-]+\.)*(square\.link|squareup\.com|square\.site|squareupsandbox\.com)\//i.test(String(j.url))) throw new Error('Checkout returned an unexpected address.');
            location.href = j.url;
          })
          .catch(function (e) { msg.textContent = e.message || 'Something went wrong. Please try again.'; msg.hidden = false; go.disabled = false; go.textContent = 'Checkout'; });
      };
      foot.appendChild(go); foot.appendChild(msg); root.appendChild(foot);
    };
    renderCart();
  }
})();
