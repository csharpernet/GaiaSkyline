// Admin hero-video upload (Stage 7E-4). Previews the chosen file, lets the owner set the loop trim and the
// mobile focal point, uploads with a progress bar, then polls the transcode status until the new video is live.
(function () {
  'use strict';

  var form = document.getElementById('hero-upload-form');
  if (!form) return;

  var fileInput = document.getElementById('hero-file');
  var previewWrap = form.querySelector('[data-hero-preview]');
  var preview = document.getElementById('hero-preview-video');
  var focalTrack = document.getElementById('hero-focal-track');
  var focalLine = document.getElementById('hero-focal-line');
  var focalInput = document.getElementById('hero-focal');
  var startInput = document.getElementById('hero-trim-start');
  var endInput = document.getElementById('hero-trim-end');
  var loopNote = document.getElementById('hero-loop-note');
  var submit = document.getElementById('hero-submit');
  var progress = document.getElementById('hero-progress');
  var progressBar = document.getElementById('hero-progress-bar');
  var progressText = document.getElementById('hero-progress-text');
  var statusBadge = document.querySelector('[data-status-badge]');

  var minLoop = parseFloat(form.getAttribute('data-min-loop')) || 10;
  var maxLoop = parseFloat(form.getAttribute('data-max-loop')) || 20;
  var statusUrl = form.getAttribute('data-status-url');
  var objectUrl = null;

  function round1(n) { return Math.round(n * 10) / 10; }

  function updateLoopNote() {
    var start = parseFloat(startInput.value) || 0;
    var end = parseFloat(endInput.value) || 0;
    var loop = round1(end - start);
    var ok = loop >= minLoop && loop <= maxLoop;
    loopNote.textContent = 'Loop length: ' + loop + ' s' + (ok ? '' : ' — must be between ' + minLoop + ' and ' + maxLoop + ' s');
    loopNote.className = 'mt-2 text-xs ' + (ok ? 'text-ink/50' : 'text-clay');
    submit.disabled = !ok;
  }

  fileInput.addEventListener('change', function () {
    var file = fileInput.files && fileInput.files[0];
    if (!file) return;
    if (objectUrl) URL.revokeObjectURL(objectUrl);
    objectUrl = URL.createObjectURL(file);
    preview.src = objectUrl;
    previewWrap.hidden = false;
    preview.addEventListener('loadedmetadata', function () {
      // Default the end to a loop that fits the clip.
      var d = preview.duration || 0;
      if (d > 0) {
        endInput.value = round1(Math.min(d, Math.max(minLoop, Math.min(maxLoop, d))));
      }
      updateLoopNote();
    }, { once: true });
  });

  [startInput, endInput].forEach(function (el) { el.addEventListener('input', updateLoopNote); });

  form.querySelectorAll('[data-set]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var which = btn.getAttribute('data-set');
      (which === 'start' ? startInput : endInput).value = round1(preview.currentTime || 0);
      updateLoopNote();
    });
  });

  // Focal point: drag the vertical line (0 = left, 1 = right).
  function setFocal(x) {
    var clamped = Math.max(0, Math.min(1, x));
    focalInput.value = clamped.toFixed(2);
    focalLine.style.left = (clamped * 100) + '%';
  }
  function focalFromEvent(e) {
    var rect = focalTrack.getBoundingClientRect();
    var clientX = e.touches ? e.touches[0].clientX : e.clientX;
    setFocal((clientX - rect.left) / rect.width);
  }
  var dragging = false;
  focalTrack.addEventListener('pointerdown', function (e) { dragging = true; focalFromEvent(e); focalTrack.setPointerCapture && focalTrack.setPointerCapture(e.pointerId); });
  focalTrack.addEventListener('pointermove', function (e) { if (dragging) focalFromEvent(e); });
  focalTrack.addEventListener('pointerup', function () { dragging = false; });
  focalInput.addEventListener('input', function () { setFocal(parseFloat(focalInput.value) || 0); });

  function setBadge(text, cls) {
    if (!statusBadge) return;
    statusBadge.textContent = text;
    statusBadge.className = 'text-xs uppercase tracking-wide border rounded px-2 py-1 ' + cls;
  }

  function poll() {
    if (!statusUrl) { window.location.reload(); return; }
    fetch(statusUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
      .then(function (r) { return r.json(); })
      .then(function (s) {
        if (!s.exists) { window.setTimeout(poll, 3000); return; }
        if (s.status === 'Ready') {
          progressText.textContent = 'Done — the new hero video is live.';
          window.setTimeout(function () { window.location.reload(); }, 800);
        } else if (s.status === 'Failed') {
          setBadge('Failed', 'border-clay text-clay');
          progressText.textContent = 'Processing failed: ' + (s.error || 'unknown error');
          submit.disabled = false;
        } else {
          setBadge(s.status, 'border-ink/30 text-ink/60');
          progressText.textContent = 'Processing (' + s.status + ')…';
          window.setTimeout(poll, 3000);
        }
      })
      .catch(function () { window.setTimeout(poll, 4000); });
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var file = fileInput.files && fileInput.files[0];
    if (!file) return;

    var data = new FormData();
    data.append('file', file);
    data.append('trimStart', startInput.value);
    data.append('trimEnd', endInput.value);
    data.append('crossfade', document.getElementById('hero-crossfade').value);
    data.append('focalX', focalInput.value);

    var xhr = new XMLHttpRequest();
    xhr.open('POST', '/api/admin/media/hero');
    xhr.withCredentials = true;
    submit.disabled = true;
    progress.hidden = false;
    progressBar.style.width = '0%';
    progressText.textContent = 'Uploading…';

    xhr.upload.onprogress = function (ev) {
      if (ev.lengthComputable) {
        var pct = Math.round((ev.loaded / ev.total) * 100);
        progressBar.style.width = pct + '%';
        progressText.textContent = 'Uploading… ' + pct + '%';
      }
    };
    xhr.onload = function () {
      if (xhr.status >= 200 && xhr.status < 300) {
        progressBar.style.width = '100%';
        progressText.textContent = 'Uploaded — processing in the background…';
        setBadge('Transcoding', 'border-ink/30 text-ink/60');
        poll();
      } else {
        var msg = 'Upload failed.';
        try { msg = JSON.parse(xhr.responseText).error || msg; } catch (_) { /* ignore */ }
        progressText.textContent = msg;
        progressText.className = 'mt-1 text-xs text-clay';
        submit.disabled = false;
      }
    };
    xhr.onerror = function () {
      progressText.textContent = 'Upload failed — check your connection and try again.';
      submit.disabled = false;
    };
    xhr.send(data);
  });
})();
