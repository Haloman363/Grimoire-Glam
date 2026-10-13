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

  function readHand(side) {
    var out = [];
    for (var i = 0; i < 5; i++) {
      var v = byId('M' + side + i).value;
      if (!v) return null;
      out.push(v);
    }
    return out;
  }
  function describe(vals) { return fingers.map(function (f, i) { return f + ' ' + vals[i]; }).join(', '); }
  function codeOf(L, R) {
    var part = function (v) { return keys.map(function (k, i) { return k + v[i]; }).join(' '); };
    return 'MARKET SIZES 1-10  L: ' + part(L) + ' | R: ' + part(R);
  }

  var saved = load();
  if (saved && saved.vals) {
    ids.forEach(function (id) { if (saved.vals[id]) byId(id).value = saved.vals[id]; });
    byId('m-banner').hidden = false;
  }

  byId('m-copy').addEventListener('click', function () {
    for (var i = 0; i < 5; i++) byId('MR' + i).value = byId('ML' + i).value;
    toast('Left copied to Right');
  });

  byId('m-show').addEventListener('click', function () {
    var err = byId('m-err'), out = byId('m-out');
    var L = readHand('L'), R = readHand('R');
    if (!L || !R) {
      err.textContent = 'Please choose a size for every finger on both hands.';
      err.classList.add('show'); out.classList.remove('show');
      return;
    }
    err.classList.remove('show');
    var code = codeOf(L, R);
    byId('m-left').textContent = describe(L);
    byId('m-right').textContent = describe(R);
    byId('m-code').value = code;
    var email = document.body.getAttribute('data-email') || '';
    var subject = encodeURIComponent('My Grimoire Glam sizes');
    var body = encodeURIComponent('Left hand: ' + describe(L) + '\nRight hand: ' + describe(R) + '\n\nSizing code (paste into the sizing box when you order):\n' + code + '\n');
    byId('m-email').href = 'mailto:?subject=' + subject + '&body=' + body;
    byId('m-shop').href = 'mailto:' + encodeURIComponent(email).replace('%40', '@') + '?subject=' + encodeURIComponent('Market sizes') +
      '&body=' + encodeURIComponent('Name:\nSet I want:\n\nLeft hand: ' + describe(L) + '\nRight hand: ' + describe(R) + '\n\n' + code + '\n');
    out.classList.add('show');
    out.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });

  byId('m-copycode').addEventListener('click', function () {
    var ta = byId('m-code');
    ta.select(); ta.setSelectionRange(0, 99999);
    var done = function () { toast('Copied!'); };
    try { navigator.clipboard.writeText(ta.value).then(done, function () { document.execCommand('copy'); done(); }); }
    catch (e) { document.execCommand('copy'); done(); }
  });

  byId('m-save').addEventListener('click', function () {
    var vals = {};
    ids.forEach(function (id) { vals[id] = byId(id).value; });
    if (store({ code: byId('m-code').value, vals: vals, savedAt: Date.now() })) toast('Sizes saved on this device');
    else toast('Saving is not available in this browser');
  });

  byId('m-clear').addEventListener('click', function () {
    clearStore();
    ids.forEach(function (id) { byId(id).value = ''; });
    byId('m-banner').hidden = true;
    byId('m-out').classList.remove('show');
    toast('Saved sizes cleared');
  });
})();
