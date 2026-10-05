// Partner applications (Stage 7 §11): the row's shared note input belongs to the approve form (via
// form=); submitting the reject form copies the note into its hidden field so either decision keeps it.
(function () {
    'use strict';

    document.querySelectorAll('[data-reject-form]').forEach(function (form) {
        form.addEventListener('submit', function () {
            const container = form.closest('[data-application-row]');
            const note = container ? container.querySelector('input[placeholder="Decision note (optional)"]') : null;
            const target = form.querySelector('[data-copy-note]');
            if (note && target) {
                target.value = note.value;
            }
        });
    });
})();
