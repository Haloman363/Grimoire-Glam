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
