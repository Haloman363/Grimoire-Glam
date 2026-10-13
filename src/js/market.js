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

  // ---- save as a picture (share sheet on phones, on-screen picture otherwise) ----
  function drawCard(L, R, done) {
    var W = 1080, H = 1620;
    var c = document.createElement('canvas'); c.width = W; c.height = H;
    var x = c.getContext('2d');
    x.fillStyle = '#f4ecdf'; x.fillRect(0, 0, W, H);
    x.strokeStyle = '#3d0a0c'; x.lineWidth = 6; x.strokeRect(36, 36, W - 72, H - 72);
    var finish = function (logo) {
      if (logo) { var h = 230, w = h * logo.width / logo.height; x.drawImage(logo, (W - w) / 2, 96, w, h); }
      x.textAlign = 'center'; x.fillStyle = '#1c0f0f';
      x.font = "400 92px 'Bodoni Moda', Georgia, serif"; x.fillText('My Sizes', W / 2, 470);
      x.fillStyle = '#6b1219'; x.font = "500 30px 'Jost', Arial, sans-serif";
      x.fillText('GRIMOIRE GLAM  ·  SIZES 1 TO 10', W / 2, 530);
      [['LEFT HAND', L, 300], ['RIGHT HAND', R, 780]].forEach(function (col) {
        x.fillStyle = '#6a5a55'; x.font = "500 28px 'Jost', Arial, sans-serif"; x.fillText(col[0], col[2], 650);
        for (var i = 0; i < 5; i++) {
          var y = 760 + i * 150;
          x.strokeStyle = '#d8cab6'; x.lineWidth = 2; x.beginPath(); x.moveTo(col[2] - 190, y - 70); x.lineTo(col[2] + 190, y - 70); x.stroke();
          x.fillStyle = '#4a3a36'; x.textAlign = 'left'; x.font = "400 34px 'Jost', Arial, sans-serif"; x.fillText(fingers[i], col[2] - 190, y);
          x.fillStyle = '#1c0f0f'; x.textAlign = 'right'; x.font = "500 84px 'Jost', Arial, sans-serif"; x.fillText(col[1][i], col[2] + 190, y + 8);
          x.textAlign = 'center';
        }
      });
      x.fillStyle = '#6a5a55'; x.font = "400 30px 'Jost', Arial, sans-serif";
      x.fillText('Show this when you order, or choose Custom size and paste your sizes.', W / 2, 1500);
      x.fillText('grimoireglam.com', W / 2, 1550);
      c.toBlob(done, 'image/png');
    };
    var logo = new Image();
    logo.onload = function () { finish(logo); };
    logo.onerror = function () { finish(null); };
    logo.src = (document.body.getAttribute('data-base') || '') + '/assets/img/brand/logo-character.webp';
  }
  function showPicture(blob) {
    var url = URL.createObjectURL(blob);
    byId('m-pic-img').src = url;
    byId('m-pic-dl').href = url;
    byId('m-pic').hidden = false;
    byId('m-pic').scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
  byId('m-pic-btn').addEventListener('click', function () {
    if (!guard()) return;
    var L = readHand('L'), R = readHand('R');
    var fontsReady = document.fonts && document.fonts.load
      ? Promise.all([document.fonts.load("400 40px 'Bodoni Moda'"), document.fonts.load("500 30px 'Jost'")]).catch(function () {})
      : Promise.resolve();
    fontsReady.then(function () {
      drawCard(L, R, function (blob) {
        if (!blob) { toast('Could not make the picture'); return; }
        var file = null;
        try { file = new File([blob], 'grimoire-glam-sizes.png', { type: 'image/png' }); } catch (e) {}
        if (file && navigator.canShare && navigator.canShare({ files: [file] })) {
          navigator.share({ files: [file], title: 'My Grimoire Glam sizes' }).catch(function (err) {
            if (!err || err.name !== 'AbortError') showPicture(blob);
          });
        } else {
          showPicture(blob);
        }
      });
    });
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
