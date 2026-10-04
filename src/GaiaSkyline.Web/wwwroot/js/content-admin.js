// Content editor glue (admin-only, served from 'self'): language tabs, the rich-text toolbar driving the
// self-hosted TipTap bundle (window.GaiaEditor), and the media-picker highlight. No inline script, so it
// needs no CSP nonce; it mutates classes/attributes only (allowed under the strict CSP).
(function () {
  'use strict';

  var root = document.querySelector('[data-content-editor]');
  if (!root) return;

  // ----- Language tabs -----
  var tabs = Array.prototype.slice.call(root.querySelectorAll('[data-lang-tab]'));
  var panels = Array.prototype.slice.call(root.querySelectorAll('[data-lang-panel]'));
  function showLang(lang) {
    tabs.forEach(function (t) {
      var on = t.getAttribute('data-lang-tab') === lang;
      t.setAttribute('aria-selected', on ? 'true' : 'false');
      t.classList.toggle('border-clay', on);
      t.classList.toggle('text-clay', on);
      t.classList.toggle('font-semibold', on);
      t.classList.toggle('border-transparent', !on);
    });
    panels.forEach(function (p) {
      p.classList.toggle('hidden', p.getAttribute('data-lang-panel') !== lang);
    });
  }
  tabs.forEach(function (t) {
    t.addEventListener('click', function () { showLang(t.getAttribute('data-lang-tab')); });
  });

  // ----- Media picker: reflect the chosen radio with a highlighted border -----
  root.querySelectorAll('fieldset').forEach(function (fs) {
    fs.addEventListener('change', function (e) {
      if (!(e.target && e.target.type === 'radio')) return;
      fs.querySelectorAll('label').forEach(function (label) {
        var input = label.querySelector('input[type=radio]');
        if (!input) return;
        var on = input.checked;
        label.classList.toggle('border-clay', on);
        label.classList.toggle('ring-1', on);
        label.classList.toggle('ring-clay', on);
        label.classList.toggle('border-fog', !on);
      });
    });
  });

  // ----- Rich-text editors -----
  if (!window.GaiaEditor) return; // bundle missing (should never happen in admin); inputs still post raw HTML.

  root.querySelectorAll('[data-editor]').forEach(function (wrap) {
    var mountEl = wrap.querySelector('.gaia-editor-mount');
    var source = wrap.querySelector('.gaia-editor-source');
    var toolbar = wrap.querySelector('.gaia-editor-toolbar');
    if (!mountEl || !source) return;

    var editor = window.GaiaEditor.mount(mountEl, {
      content: source.value,
      onUpdate: function (html) { source.value = html; },
    });

    function refresh() {
      if (!toolbar) return;
      toolbar.querySelectorAll('[data-cmd]').forEach(function (btn) {
        var cmd = btn.getAttribute('data-cmd');
        var active =
          (cmd === 'bold' && editor.isActive('bold')) ||
          (cmd === 'italic' && editor.isActive('italic')) ||
          (cmd === 'bulletList' && editor.isActive('bulletList')) ||
          (cmd === 'orderedList' && editor.isActive('orderedList')) ||
          (cmd === 'link' && editor.isActive('link'));
        btn.classList.toggle('is-active', !!active);
      });
    }
    editor.on('selectionUpdate', refresh);
    editor.on('update', refresh);

    if (toolbar) {
      toolbar.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-cmd]');
        if (!btn) return;
        e.preventDefault();
        var cmd = btn.getAttribute('data-cmd');
        var chain = editor.chain().focus();
        if (cmd === 'bold') chain.toggleBold().run();
        else if (cmd === 'italic') chain.toggleItalic().run();
        else if (cmd === 'bulletList') chain.toggleBulletList().run();
        else if (cmd === 'orderedList') chain.toggleOrderedList().run();
        else if (cmd === 'unlink') chain.unsetLink().run();
        else if (cmd === 'link') {
          var prev = editor.getAttributes('link').href || '';
          var url = window.prompt('Link URL (http(s):// or mailto:)', prev);
          if (url === null) { chain.run(); return; } // cancelled
          if (url === '') { chain.unsetLink().run(); return; }
          chain.extendMarkRange('link').setLink({ href: url }).run();
        }
        refresh();
      });
    }

    refresh();
  });
})();
