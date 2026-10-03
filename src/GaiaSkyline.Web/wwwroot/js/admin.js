// Admin UX foundation: toasts, typed confirmation for destructive/money actions, and keyboard
// shortcuts. Served from 'self' so it needs no CSP nonce; it reads server state from data-* attributes
// (no inline script) and mutates styles via the CSSOM (allowed under the strict style-src).
(function () {
  'use strict';

  function autoDismiss(el) {
    if (!el) return;
    window.setTimeout(function () {
      el.style.transition = 'opacity .3s';
      el.style.opacity = '0';
      window.setTimeout(function () { el.remove(); }, 300);
    }, 4500);
  }

  // Server-rendered toast (TempData) + a client API for inline feedback.
  document.querySelectorAll('[data-toast]').forEach(autoDismiss);
  window.gaiaToast = function (message, kind) {
    var el = document.createElement('div');
    el.setAttribute('data-toast', '');
    el.setAttribute('data-kind', kind || 'success');
    el.className = 'fixed bottom-4 right-4 z-50 max-w-sm rounded-md border px-4 py-3 text-sm shadow-lg bg-white ' +
      (kind === 'error' ? 'border-clay text-clay' : 'border-river text-ink');
    el.textContent = message;
    document.body.appendChild(el);
    autoDismiss(el);
  };

  // Typed confirmation: <form data-confirm="msg"> prompts yes/no; add data-confirm-type="WORD" to
  // require typing WORD (destructive or money actions).
  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!(form instanceof HTMLFormElement)) return;
    var word = form.getAttribute('data-confirm-type');
    var msg = form.getAttribute('data-confirm');
    if (word) {
      var answer = window.prompt((msg || 'This action cannot be undone.') + '\n\nType ' + word + ' to confirm:');
      if (answer !== word) e.preventDefault();
    } else if (msg) {
      if (!window.confirm(msg)) e.preventDefault();
    }
  }, true);

  // Help overlay.
  var help = document.getElementById('admin-help');
  function toggleHelp(show) {
    if (!help) return;
    // Toggle classes, not the `hidden` attribute: Tailwind's display utilities (author CSS) override
    // the attribute's UA-stylesheet display:none, so the attribute alone would never hide the overlay.
    var showing = (show === undefined) ? help.classList.contains('hidden') : show;
    help.classList.toggle('hidden', !showing);
    help.classList.toggle('flex', showing);
  }
  var helpBtn = document.getElementById('admin-help-btn');
  if (helpBtn) helpBtn.addEventListener('click', function () { toggleHelp(true); });
  var helpClose = document.getElementById('admin-help-close');
  if (helpClose) helpClose.addEventListener('click', function () { toggleHelp(false); });

  // Navigate only to sections that exist (an enabled sidebar link); otherwise nudge the owner.
  function navTo(href) {
    if (document.querySelector('aside a[href="' + href + '"]')) {
      window.location.href = href;
    } else {
      window.gaiaToast('That section ships in a later increment.', 'info');
    }
  }

  // Shortcuts: / focus search, ? help, Esc close, and the g-prefixed jumps (g h / g c / g b).
  var gPending = false, gTimer = null;
  document.addEventListener('keydown', function (e) {
    var t = e.target, tag = (t.tagName || '').toLowerCase();
    var typing = tag === 'input' || tag === 'textarea' || tag === 'select' || (t && t.isContentEditable);
    if (e.key === 'Escape') { toggleHelp(false); return; }
    if (typing || e.metaKey || e.ctrlKey || e.altKey) return;
    if (e.key === '/') { e.preventDefault(); var s = document.getElementById('admin-search'); if (s) s.focus(); return; }
    if (e.key === '?') { e.preventDefault(); toggleHelp(); return; }
    if (gPending) {
      gPending = false; window.clearTimeout(gTimer);
      if (e.key === 'h') navTo('/admin');
      else if (e.key === 'c') navTo('/admin/calendar');
      else if (e.key === 'b') navTo('/admin/bookings');
      return;
    }
    if (e.key === 'g') { gPending = true; gTimer = window.setTimeout(function () { gPending = false; }, 1200); }
  });
})();
