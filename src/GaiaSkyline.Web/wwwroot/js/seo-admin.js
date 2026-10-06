// SEO page-meta editor glue (admin-only, served from 'self'): live character counters (coloured by the
// recommended range) plus Google-snippet and Open Graph card previews for the focused page+language.
// Classes/attributes only, so it needs no CSP nonce.
(function () {
  'use strict';

  var form = document.querySelector('[data-seo-meta]');
  if (!form) return;

  var preview = document.getElementById('seo-preview');
  var prevUrl = document.getElementById('seo-prev-url');
  var prevTitle = document.getElementById('seo-prev-title');
  var prevDesc = document.getElementById('seo-prev-desc');
  var ogSite = document.getElementById('seo-og-site');
  var ogTitle = document.getElementById('seo-og-title');
  var ogDesc = document.getElementById('seo-og-desc');
  var ogImage = document.getElementById('seo-og-image');

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

    // The Open Graph card mirrors what _Layout emits: og:title/og:description follow the page title and
    // description; og:image is the page's share image (the home hero rendition serves as the stand-in).
    if (ogTitle) {
      ogSite.textContent = window.location.host;
      ogTitle.textContent = prevTitle.textContent;
      ogDesc.textContent = prevDesc.textContent;
      if (ogImage && !ogImage.getAttribute('src')) {
        ogImage.src = '/media/home-hero-poster-800.jpg';
      }
    }
  }

  if (ogImage) {
    // No stand-in image on this install (e.g. a bare test database) — keep the grey placeholder box.
    ogImage.addEventListener('error', function () {
      ogImage.removeAttribute('src');
    });
  }

  form.querySelectorAll('[data-seo-field]').forEach(function (input) {
    updateCounter(input);
    input.addEventListener('input', function () { updateCounter(input); showPreview(input); });
    input.addEventListener('focus', function () { showPreview(input); });
  });
})();
