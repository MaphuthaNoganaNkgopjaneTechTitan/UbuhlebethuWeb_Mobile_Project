// Progressive enhancement for the public site – everything still works without JavaScript.
(function () {
  // mobile menu
  var btn = document.getElementById('menuBtn'), nav = document.getElementById('mainNav');
  if (btn && nav) btn.addEventListener('click', function () {
    var open = nav.classList.toggle('open');
    btn.setAttribute('aria-expanded', open ? 'true' : 'false');
  });

  // project filter chips
  var chips = document.querySelectorAll('.chip[data-filter]');
  var cards = document.querySelectorAll('.card[data-category]');
  var count = document.getElementById('projCount');
  function apply(f) {
    var n = 0;
    cards.forEach(function (c) {
      var show = f === 'all' || c.dataset.category === f;
      c.hidden = !show; if (show) n++;
    });
    chips.forEach(function (ch) { ch.setAttribute('aria-pressed', ch.dataset.filter === f ? 'true' : 'false'); });
    if (count) count.textContent = 'Showing ' + n + ' of ' + cards.length + ' completed projects';
  }
  chips.forEach(function (ch) { ch.addEventListener('click', function () { apply(ch.dataset.filter); }); });

  // proposal form: file picker feedback + client-side checks (server re-validates everything)
  var file = document.getElementById('Document');
  if (file) {
    var picked = document.getElementById('picked'), drop = document.getElementById('drop'), fe = document.getElementById('fileError');
    var ok = ['pdf', 'doc', 'docx'], max = 10 * 1024 * 1024;
    function show(f) {
      fe.textContent = '';
      if (!f) { picked.textContent = ''; return; }
      var ext = (f.name.split('.').pop() || '').toLowerCase();
      if (ok.indexOf(ext) < 0) { fe.textContent = 'Only PDF, DOC or DOCX files are accepted.'; file.value = ''; picked.textContent = ''; return; }
      if (f.size > max) { fe.textContent = 'That file is larger than 10 MB.'; file.value = ''; picked.textContent = ''; return; }
      picked.textContent = '📎 ' + f.name + ' (' + (f.size / 1024 / 1024).toFixed(2) + ' MB)';
    }
    file.addEventListener('change', function () { show(file.files[0]); });
    ['dragenter', 'dragover'].forEach(function (e) { drop.addEventListener(e, function (ev) { ev.preventDefault(); drop.classList.add('drag'); }); });
    ['dragleave', 'drop'].forEach(function (e) { drop.addEventListener(e, function (ev) { ev.preventDefault(); drop.classList.remove('drag'); }); });
    drop.addEventListener('drop', function (ev) {
      if (ev.dataTransfer.files.length) { file.files = ev.dataTransfer.files; show(file.files[0]); }
    });
  }
  var form = document.getElementById('proposalForm');
  if (form) form.addEventListener('submit', function () {
    var b = document.getElementById('submitBtn');
    if (b) { b.disabled = true; b.textContent = 'Uploading… please wait'; }
  });

  // photo viewer ("View N photos") - opens a gallery of the project's other completed photos
  var dlg = document.getElementById('galleryDialog');
  if (dlg && typeof dlg.showModal === 'function') {
    var gImg = document.getElementById('gImg'), gCount = document.getElementById('gCount'),
        gThumbs = document.getElementById('gThumbs'), gTitle = document.getElementById('galleryTitle');
    var photos = [], cur = 0, title = '';
    function showPhoto(i) {
      cur = (i + photos.length) % photos.length;
      gImg.src = photos[cur];
      gImg.alt = title + ' – photo ' + (cur + 1) + ' of ' + photos.length;
      gCount.textContent = 'Photo ' + (cur + 1) + ' of ' + photos.length;
      Array.prototype.forEach.call(gThumbs.children, function (b, k) { b.setAttribute('aria-current', k === cur ? 'true' : 'false'); });
    }
    document.querySelectorAll('[data-gallery]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        photos = btn.dataset.gallery.split('|').filter(Boolean);
        title = btn.dataset.title || '';
        gTitle.textContent = title;
        gThumbs.textContent = '';
        photos.forEach(function (src, k) {
          var b = document.createElement('button'); b.type = 'button'; b.setAttribute('aria-label', 'Show photo ' + (k + 1));
          var im = document.createElement('img'); im.src = src; im.alt = ''; b.appendChild(im);
          b.addEventListener('click', function () { showPhoto(k); });
          gThumbs.appendChild(b);
        });
        var single = photos.length < 2;
        document.getElementById('gPrev').hidden = single; document.getElementById('gNext').hidden = single;
        gThumbs.hidden = single;
        showPhoto(0);
        dlg.showModal();
      });
    });
    document.getElementById('gPrev').addEventListener('click', function () { showPhoto(cur - 1); });
    document.getElementById('gNext').addEventListener('click', function () { showPhoto(cur + 1); });
    document.getElementById('gClose').addEventListener('click', function () { dlg.close(); });
    dlg.addEventListener('click', function (e) { if (e.target === dlg) dlg.close(); });
    dlg.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowLeft') showPhoto(cur - 1);
      if (e.key === 'ArrowRight') showPhoto(cur + 1);
    });
  }
})();
