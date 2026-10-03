// Home booking widget: persists the date/guest selection to sessionStorage and links into /book with
// it prefilled. Deliberately tiny — no network, no calendar library — so the home budgets hold.
(function () {
    'use strict';

    var form = document.getElementById('booking-widget');
    if (!form) {
        return;
    }

    var slug = form.getAttribute('data-slug') || 'en';
    var checkIn = document.getElementById('w-checkin');
    var checkOut = document.getElementById('w-checkout');
    var guests = document.getElementById('w-guests');
    var storeKey = 'gs-booking-widget';

    try {
        var saved = JSON.parse(sessionStorage.getItem(storeKey) || '{}');
        if (saved.checkIn) { checkIn.value = saved.checkIn; }
        if (saved.checkOut) { checkOut.value = saved.checkOut; }
        if (saved.guests) { guests.value = saved.guests; }
    } catch (e) { /* ignore */ }

    function persist() {
        try {
            sessionStorage.setItem(storeKey, JSON.stringify({
                checkIn: checkIn.value,
                checkOut: checkOut.value,
                guests: guests.value,
            }));
        } catch (e) { /* ignore */ }
    }

    [checkIn, checkOut, guests].forEach(function (el) { el.addEventListener('change', persist); });

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        persist();
        var params = new URLSearchParams();
        if (checkIn.value) { params.set('checkIn', checkIn.value); }
        if (checkOut.value) { params.set('checkOut', checkOut.value); }
        params.set('adults', guests.value);
        var query = params.toString();
        window.location.href = '/' + slug + '/book' + (query ? '?' + query : '');
    });
})();
