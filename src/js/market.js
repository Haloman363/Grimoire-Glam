// Market sizing: pick a size from 1 to 10 for each finger. Sizes stay in this browser unless the visitor emails them.
(function () {
  'use strict';
  var byId = function (id) { return document.getElementById(id); };
  var fingers = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky'];
  var keys = ['T', 'I', 'M', 'R', 'P'];
  var STORE = 'grimoireMarketSizes.v1';
  var ids = [];
  ['L', 'R'].forEach(function (side) { for (var i = 0; i < 5; i++) ids.push('M' + side + i); });

  function load() { try { return JSON.parse(localStorage.getItem(STORE)); } catch (e) { return null; } }
  function store(o) { try { localStorage.setItem(STORE, JSON.stringify(o)); return true; } catch (e) { return false; } }
  function clearStore() { try { localStorage.removeItem(STORE); } catch (e) {} }

  function toast(msg) {
    var t = byId('m-toast');
    t.textContent = msg;
    t.classList.add('show');
    setTimeout(function () { t.classList.remove('show'); }, 1600);
  }

  function readHand(side) { var v = []; for (var i = 0; i < 5; i++) v.push(byId('M' + side + i).value); return v; }
  function complete(v) { return v.every(function (x) { return x; }); }
  function describe(vals) { return fingers.map(function (f, i) { return f + ' ' + (vals[i] || '-'); }).join(', '); }
  function codeOf(L, R) {
    var part = function (v) { return keys.map(function (k, i) { return k + v[i]; }).join(' '); };
    return 'MARKET SIZES 1-10  L: ' + part(L) + ' | R: ' + part(R);
  }

  var state = { ready: false, code: '' };
  function update() {
    var L = readHand('L'), R = readHand('R');
    byId('m-left').textContent = describe(L);
    byId('m-right').textContent = describe(R);
    state.ready = complete(L) && complete(R);
    state.code = state.ready ? codeOf(L, R) : '';
    byId('m-code').value = state.code;
    byId('m-out').classList.toggle('ready', state.ready);
    byId('m-err').classList.remove('show');
    var email = document.body.getAttribute('data-email') || '';
    if (state.ready) {
      var summary = 'Left hand: ' + describe(L) + '\nRight hand: ' + describe(R);
      byId('m-email').href = 'mailto:?subject=' + encodeURIComponent('My Grimoire Glam sizes') +
        '&body=' + encodeURIComponent(summary + '\n\nSizing code (paste into the sizing box when you order):\n' + state.code + '\n');
      byId('m-shop').href = 'mailto:' + encodeURIComponent(email).replace('%40', '@') + '?subject=' + encodeURIComponent('Market sizes') +
        '&body=' + encodeURIComponent('Name:\nSet I want:\n\n' + summary + '\n\n' + state.code + '\n');
    } else {
      byId('m-email').href = '#';
      byId('m-shop').href = '#';
    }
  }

  // Every button needs all ten sizes first; if they are missing, say so instead of doing anything.
  function guard(e) {
    if (state.ready) return true;
    if (e) e.preventDefault();
    var err = byId('m-err');
    err.textContent = 'Please choose a size for every finger on both hands first.';
    err.classList.add('show');
    return false;
  }

  var saved = load();
  if (saved && saved.vals) {
    ids.forEach(function (id) { if (saved.vals[id]) byId(id).value = saved.vals[id]; });
    byId('m-banner').hidden = false;
  }
  ids.forEach(function (id) { byId(id).addEventListener('change', update); });
  update();

  byId('m-copy').addEventListener('click', function () {
    for (var i = 0; i < 5; i++) byId('MR' + i).value = byId('ML' + i).value;
    update();
    toast('Left copied to Right');
  });

  byId('m-copycode').addEventListener('click', function () {
    if (!guard()) return;
    var ta = byId('m-code');
    ta.select(); ta.setSelectionRange(0, 99999);
    var done = function () { toast('Copied!'); };
    try { navigator.clipboard.writeText(ta.value).then(done, function () { document.execCommand('copy'); done(); }); }
    catch (e) { document.execCommand('copy'); done(); }
  });

  byId('m-save').addEventListener('click', function () {
    if (!guard()) return;
    var vals = {};
    ids.forEach(function (id) { vals[id] = byId(id).value; });
    if (store({ code: state.code, vals: vals, savedAt: Date.now() })) toast('Sizes saved on this device');
    else toast('Saving is not available in this browser');
  });

  byId('m-email').addEventListener('click', guard);
  byId('m-shop').addEventListener('click', guard);

  byId('m-clear').addEventListener('click', function () {
    clearStore();
    ids.forEach(function (id) { byId(id).value = ''; });
    byId('m-banner').hidden = true;
    update();
    toast('Saved sizes cleared');
  });
})();
