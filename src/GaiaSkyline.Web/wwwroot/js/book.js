// Booking page: two availability-aware Flatpickr calendars + a live quote. No Stripe here — the
// payment step loads on /book/checkout only. Flatpickr is loaded (deferred) before this script.
(function () {
    'use strict';

    var form = document.getElementById('booking-form');
    if (!form || typeof flatpickr === 'undefined') {
        return;
    }

    var slug = form.getAttribute('data-slug') || 'en';
    var quotePanel = document.getElementById('quote-panel');
    var reserveButton = document.getElementById('reserve');
    var sofaNote = document.getElementById('sofa-note');
    var adults = document.getElementById('adults');
    var children = document.getElementById('children');
    var infants = document.getElementById('infants');
    var promo = document.getElementById('promo');

    var state = { checkIn: null, checkOut: null };
    var checkInPicker;
    var checkOutPicker;

    function euro(value) {
        return '€' + Number(value).toFixed(2);
    }

    function iso(date) {
        var y = date.getFullYear();
        var m = String(date.getMonth() + 1).padStart(2, '0');
        var d = String(date.getDate()).padStart(2, '0');
        return y + '-' + m + '-' + d;
    }

    function guestCounts() {
        return {
            adults: parseInt(adults.value, 10) || 1,
            children: parseInt(children.value, 10) || 0,
            infants: parseInt(infants.value, 10) || 0,
        };
    }

    function toggleSofaNote() {
        var g = guestCounts();
        sofaNote.hidden = (g.adults + g.children) <= 4;
    }

    function setReserve(enabled) {
        reserveButton.disabled = !enabled;
    }

    function renderError(message) {
        quotePanel.innerHTML = '<p class="text-clay">' + message + '</p>';
        setReserve(false);
    }

    function renderQuote(q) {
        var rows = '';
        rows += row('Nights', String(q.nights));
        rows += row('Accommodation', euro(q.nightlySubtotal));
        if (q.discount && q.discount.amount > 0) {
            rows += row('Discount (' + q.discount.percent + '%)', '−' + euro(q.discount.amount));
        }
        if (q.cleaningFee > 0) {
            rows += row('Cleaning fee', euro(q.cleaningFee));
        }
        if (q.touristTax > 0) {
            rows += row('Tourist tax', euro(q.touristTax));
        }
        var total = '<div class="mt-3 pt-3 border-t border-fog flex justify-between font-semibold text-base">' +
            '<span>Total</span><span>' + euro(q.total) + '</span></div>';
        var invalidPromo = q.promoRequestedButInvalid
            ? '<p class="mt-2 text-xs text-clay">That promo code isn’t valid.</p>' : '';
        quotePanel.innerHTML = '<dl class="space-y-1">' + rows + '</dl>' + total + invalidPromo;
        setReserve(true);
    }

    function row(label, value) {
        return '<div class="flex justify-between"><dt class="text-ink/70">' + label + '</dt><dd>' + value + '</dd></div>';
    }

    function updateQuote() {
        toggleSofaNote();
        if (!state.checkIn || !state.checkOut) {
            return;
        }
        var g = guestCounts();
        var body = JSON.stringify({
            checkIn: state.checkIn,
            checkOut: state.checkOut,
            adults: g.adults,
            children: g.children,
            infants: g.infants,
            promoCode: promo.value ? promo.value.trim() : null,
        });
        fetch('/api/quote', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: body })
            .then(function (r) {
                if (r.ok) {
                    return r.json().then(renderQuote);
                }
                if (r.status === 422) {
                    return r.json().then(function (e) { renderError(e.error || 'Those dates aren’t available.'); });
                }
                renderError('Couldn’t get a price just now. Please try again.');
            })
            .catch(function () { renderError('Couldn’t get a price just now. Please try again.'); });
    }

    function reserve() {
        if (!state.checkIn || !state.checkOut) {
            return;
        }
        var g = guestCounts();
        var params = new URLSearchParams({
            checkIn: state.checkIn,
            checkOut: state.checkOut,
            adults: g.adults,
            children: g.children,
            infants: g.infants,
        });
        if (promo.value) {
            params.set('promo', promo.value.trim());
        }
        window.location.href = '/' + slug + '/book/checkout?' + params.toString();
    }

    function initCalendars(blockedDates) {
        var today = new Date();
        checkInPicker = flatpickr('#checkin-cal', {
            inline: true,
            dateFormat: 'Y-m-d',
            minDate: today,
            disable: blockedDates,
            onChange: function (dates) {
                state.checkIn = dates.length ? iso(dates[0]) : null;
                if (state.checkIn && checkOutPicker) {
                    var min = new Date(dates[0].getTime());
                    min.setDate(min.getDate() + 1);
                    checkOutPicker.set('minDate', min);
                }
                updateQuote();
            },
        });
        checkOutPicker = flatpickr('#checkout-cal', {
            inline: true,
            dateFormat: 'Y-m-d',
            minDate: today,
            disable: blockedDates,
            onChange: function (dates) {
                state.checkOut = dates.length ? iso(dates[0]) : null;
                updateQuote();
            },
        });
    }

    function applyPrefill() {
        var params = new URLSearchParams(window.location.search);
        var a = params.get('adults');
        if (a) { adults.value = a; }
        var c = params.get('children');
        if (c) { children.value = c; }
        var inf = params.get('infants');
        if (inf) { infants.value = inf; }
        if (params.get('promo')) { promo.value = params.get('promo'); }
        // Setting dates triggers onChange, which refreshes the quote.
        if (params.get('checkIn') && checkInPicker) { checkInPicker.setDate(params.get('checkIn'), true); }
        if (params.get('checkOut') && checkOutPicker) { checkOutPicker.setDate(params.get('checkOut'), true); }
        toggleSofaNote();
    }

    function loadAvailabilityThenInit() {
        var from = new Date();
        var to = new Date();
        to.setDate(to.getDate() + 365);
        fetch('/api/availability', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ from: iso(from), to: iso(to) }),
        })
            .then(function (r) { return r.ok ? r.json() : { blockedDates: [] }; })
            .then(function (data) { initCalendars(data.blockedDates || []); applyPrefill(); })
            .catch(function () { initCalendars([]); applyPrefill(); });
    }

    [adults, children, infants].forEach(function (el) { el.addEventListener('change', updateQuote); });
    promo.addEventListener('change', updateQuote);
    reserveButton.addEventListener('click', reserve);

    loadAvailabilityThenInit();
})();
