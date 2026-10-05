// SEO page-meta editor glue (admin-only, served from 'self'): live character counters (coloured by the
// recommended range) and a Google-style snippet preview for the focused page+language. Classes/attributes
// only, so it needs no CSP nonce.
(function () {
  'use strict';

  var form = document.querySelector('[data-seo-meta]');
  if (!form) return;

  var preview = document.getElementById('seo-preview');
  var prevUrl = document.getElementById('seo-prev-url');
  var prevTitle = document.getElementById('seo-prev-title');
  var prevDesc = document.getElementById('seo-prev-desc');

  function counterClass(len, min, max) {
    if (len === 0) return 'text-xs w-12 text-right text-ink/40';
    return 'text-xs w-12 text-right ' + (len >= min && len <= max ? 'text-river' : 'text-clay');
  }

  function updateCounter(input) {
    var counter = input.parentElement.querySelector('[data-counter]');
    if (!counter) return;
    var len = input.value.length;
    var max = input.getAttribute('data-max');
    counter.textContent = len + '/' + max;
    counter.className = counterClass(len, +input.getAttribute('data-min'), +max);
  }

  function showPreview(input) {
    var row = input.closest('.space-y-1');
    if (!row) return;
    var title = row.querySelector('[data-seo-field="title"]');
    var desc = row.querySelector('[data-seo-field="desc"]');
    var page = input.getAttribute('data-page') || '/';
    var lang = (input.getAttribute('data-lang') || 'en').toLowerCase();
    var path = '/' + lang + (page === '/' ? '' : page);

    preview.hidden = false;
    prevUrl.textContent = window.location.origin + path;
    prevTitle.textContent = (title && title.value) || '(built-in title)';
    prevDesc.textContent = (desc && desc.value) || '(built-in description)';
  }

  form.querySelectorAll('[data-seo-field]').forEach(function (input) {
    updateCounter(input);
    input.addEventListener('input', function () { updateCounter(input); showPreview(input); });
    input.addEventListener('focus', function () { showPreview(input); });
  });
})();
