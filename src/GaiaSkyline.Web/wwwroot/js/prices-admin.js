// Prices admin (Stage 7 §8).
// Grid page: clicking cells fills the bulk-edit range — first click sets "from", second sets "to";
// clicking before the current "from" restarts. Hidden clear/lock forms mirror the range on submit.
// Setup page: add/remove cancellation-tier rows.
(function () {
    'use strict';

    const fromInput = document.getElementById('bulk-from');
    const toInput = document.getElementById('bulk-to');
    if (!fromInput || !toInput) return;

    function repaint() {
        const from = fromInput.value;
        const to = toInput.value || from;
        document.querySelectorAll('[data-rate-cell]').forEach(function (cell) {
            const date = cell.getAttribute('data-rate-cell');
            const selected = from && date >= from && date <= to;
            cell.classList.toggle('ring-2', selected);
            cell.classList.toggle('ring-river', selected);
        });
    }

    document.querySelectorAll('[data-rate-cell]').forEach(function (cell) {
        cell.addEventListener('click', function () {
            const date = cell.getAttribute('data-rate-cell');
            if (!fromInput.value || (fromInput.value && toInput.value) || date < fromInput.value) {
                fromInput.value = date;
                toInput.value = '';
            } else {
                toInput.value = date;
            }
            repaint();
        });
    });

    document.querySelectorAll('form[data-sync-range]').forEach(function (form) {
        form.addEventListener('submit', function () {
            const from = form.querySelector('[data-range-from]');
            const to = form.querySelector('[data-range-to]');
            if (from) from.value = fromInput.value;
            if (to) to.value = toInput.value || fromInput.value;
        });
    });

    fromInput.addEventListener('change', repaint);
    toInput.addEventListener('change', repaint);
})();

(function () {
    'use strict';

    const rows = document.querySelector('[data-policy-rows]');
    const addButton = document.querySelector('[data-add-row]');
    if (!rows || !addButton) return;

    addButton.addEventListener('click', function () {
        const row = document.createElement('div');
        row.className = 'flex items-center gap-2 text-sm';
        row.setAttribute('data-policy-row', '');
        row.innerHTML =
            '<input name="days" type="number" min="0" max="365" required value="7" class="w-20 rounded-md border border-fog px-2 py-1.5 text-xs" />' +
            '<span class="text-xs text-ink/55">days before check-in →</span>' +
            '<input name="pct" type="number" min="0" max="100" required value="50" class="w-16 rounded-md border border-fog px-2 py-1.5 text-xs" />' +
            '<span class="text-xs text-ink/55">% refund</span>' +
            '<button type="button" class="text-xs text-clay hover:underline" data-remove-row>Remove</button>';
        rows.appendChild(row);
    });

    rows.addEventListener('click', function (event) {
        if (event.target.matches('[data-remove-row]')) {
            event.target.closest('[data-policy-row]').remove();
        }
    });
})();
