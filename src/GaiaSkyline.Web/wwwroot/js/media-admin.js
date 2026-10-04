// Media manager drag-and-drop upload (admin-only, served from 'self'). Posts each image to the existing
// cookie-authenticated /api/admin/media endpoint (SameSite=Lax covers CSRF), then reloads the grid.
(function () {
  'use strict';

  var zone = document.getElementById('media-dropzone');
  if (!zone) return;
  var input = document.getElementById('media-file-input');
  var status = document.getElementById('media-upload-status');
  var url = zone.getAttribute('data-upload-url');

  function setStatus(msg) { if (status) status.textContent = msg; }

  async function uploadAll(fileList) {
    var files = Array.prototype.filter.call(fileList, function (f) { return f.type.indexOf('image/') === 0; });
    if (!files.length) { setStatus('Only image files are supported.'); return; }

    var done = 0, failed = 0;
    for (var i = 0; i < files.length; i++) {
      setStatus('Uploading ' + (i + 1) + ' of ' + files.length + '…');
      var data = new FormData();
      data.append('file', files[i]);
      try {
        var res = await fetch(url, { method: 'POST', body: data, credentials: 'same-origin' });
        if (res.ok) { done++; } else { failed++; }
      } catch (e) {
        failed++;
      }
    }

    setStatus('Uploaded ' + done + (failed ? (', ' + failed + ' failed') : '') + '. Reloading…');
    window.location.reload();
  }

  ['dragover', 'dragenter'].forEach(function (ev) {
    zone.addEventListener(ev, function (e) { e.preventDefault(); zone.classList.add('border-river'); });
  });
  ['dragleave', 'drop'].forEach(function (ev) {
    zone.addEventListener(ev, function (e) { e.preventDefault(); zone.classList.remove('border-river'); });
  });
  zone.addEventListener('drop', function (e) {
    if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length) {
      uploadAll(e.dataTransfer.files);
    }
  });
  if (input) {
    input.addEventListener('change', function () {
      if (input.files && input.files.length) { uploadAll(input.files); }
    });
  }
})();
