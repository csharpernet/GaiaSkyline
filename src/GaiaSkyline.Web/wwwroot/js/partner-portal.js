// Partner dashboard link builder (Stage 8 Part A): page + language → absolute URL with ?ref=CODE, plus
// copy-to-clipboard. External file — inline scripts are CSP-blocked.
(function () {
  'use strict';

  var builder = document.querySelector('[data-link-builder]');
  if (!builder) return;

  var code = builder.getAttribute('data-code') || '';
  var pageSelect = builder.querySelector('[data-link-page]');
  var langSelect = builder.querySelector('[data-link-lang]');
  var output = builder.querySelector('[data-link-output]');
  var copyButton = builder.querySelector('[data-link-copy]');
  var copied = builder.querySelector('[data-link-copied]');

  function update() {
    var lang = langSelect.value || 'en';
    var page = pageSelect.value || '';
    output.value = window.location.origin + '/' + lang + page + '?ref=' + encodeURIComponent(code);
  }

  pageSelect.addEventListener('change', update);
  langSelect.addEventListener('change', update);
  update();

  copyButton.addEventListener('click', function () {
    var done = function () {
      copied.classList.remove('hidden');
      window.setTimeout(function () { copied.classList.add('hidden'); }, 2000);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(output.value).then(done, function () {
        output.select();
        done();
      });
    } else {
      output.select();
      done();
    }
  });
})();
