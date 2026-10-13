// Custom nail sizing code generator (moved from emibunce.github.io/sizing; behaviour unchanged).
// Output looks like:  L: T(w/a/l) I(...) M(...) R(...) P(...) | R: ...   and must stay <= 140 characters
// to fit the Square "custom sizing" text field.
(function () {
  'use strict';
  var fingerKey = ['T', 'I', 'M', 'R', 'P'];
  var byId = function (id) { return document.getElementById(id); };

  byId('copyLR').addEventListener('click', function () {
    for (var i = 0; i < 5; i++) ['w', 'a', 'l'].forEach(function (k) { byId('R_' + k + '_' + i).value = byId('L_' + k + '_' + i).value; });
    showToast('Left copied to Right');
  });

  function readHand(side) {
    var parts = [];
    for (var i = 0; i < 5; i++) {
      var w = byId(side + '_w_' + i).value.trim(), a = byId(side + '_a_' + i).value.trim(), l = byId(side + '_l_' + i).value.trim();
      if (w === '' || a === '' || l === '') return null;
      parts.push(fingerKey[i] + '(' + w + '/' + a + '/' + l + ')');
    }
    return parts.join(' ');
  }

  byId('generate').addEventListener('click', function () {
    var err = byId('err'), out = byId('out');
    var L = readHand('L'), R = readHand('R');
    if (L === null || R === null) {
      err.textContent = 'Please enter Width, Arch, and Length (in mm) for all ten nails, both hands.';
      err.classList.add('show'); out.classList.remove('show');
      return;
    }
    err.classList.remove('show');
    var code = 'L: ' + L + ' | R: ' + R;
    byId('code').value = code;
    var count = byId('count');
    count.textContent = code.length + ' / 140 characters';
    count.style.color = code.length > 140 ? '#e0788a' : '';
    out.classList.add('show');
    out.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });

  // ---- saved sizes (this browser only, nothing is sent anywhere) ----
  var STORE = 'grimoireSizes.v1';
  var ids = [];
  ['L', 'R'].forEach(function (side) { for (var i = 0; i < 5; i++) ['w', 'a', 'l'].forEach(function (k) { ids.push(side + '_' + k + '_' + i); }); });
  function loadSaved() { try { return JSON.parse(localStorage.getItem(STORE)); } catch (e) { return null; } }
  function storeSaved(obj) { try { localStorage.setItem(STORE, JSON.stringify(obj)); return true; } catch (e) { return false; } }
  function clearSaved() { try { localStorage.removeItem(STORE); } catch (e) {} }

  var saved = loadSaved();
  if (saved && saved.vals) {
    ids.forEach(function (id) { if (saved.vals[id] != null) byId(id).value = saved.vals[id]; });
    byId('saved-banner').hidden = false;
  }
  byId('clear-saved').addEventListener('click', function () {
    clearSaved();
    ids.forEach(function (id) { byId(id).value = ''; });
    byId('saved-banner').hidden = true;
    byId('out').classList.remove('show');
    showToast('Saved sizes cleared');
  });
  byId('save-sizes').addEventListener('click', function () {
    var vals = {};
    ids.forEach(function (id) { vals[id] = byId(id).value.trim(); });
    if (storeSaved({ code: byId('code').value, vals: vals, savedAt: Date.now() })) showToast('Sizes saved on this device');
    else showToast('Saving is not available in this browser');
  });
  function syncEmailLink() {
    var code = byId('code').value;
    byId('email-code').href = 'mailto:?subject=' + encodeURIComponent('My Grimoire Glam sizing code') +
      '&body=' + encodeURIComponent('My custom sizing code for Grimoire Glam (paste it into the sizing box when you order):\n\n' + code + '\n');
  }
  byId('generate').addEventListener('click', syncEmailLink);

  byId('copycode').addEventListener('click', function () {
    var ta = byId('code');
    ta.select(); ta.setSelectionRange(0, 99999);
    var done = function () { showToast('Copied!'); };
    try {
      navigator.clipboard.writeText(ta.value).then(done, function () { document.execCommand('copy'); done(); });
    } catch (e) { document.execCommand('copy'); done(); }
  });

  function showToast(msg) {
    var t = byId('toast');
    t.textContent = msg || 'Copied!';
    t.classList.add('show');
    setTimeout(function () { t.classList.remove('show'); }, 1600);
  }
})();
