// Polls booking status every 30 seconds while payment is pending (e.g. Multibanco). When the webhook
// confirms the booking, reload so the confirmed view (check-in instructions, add-to-calendar) shows.
(function () {
    'use strict';

    var root = document.getElementById('confirmation');
    if (!root) {
        return;
    }

    var reference = root.getAttribute('data-reference');
    var token = root.getAttribute('data-token');
    if (!reference || !token) {
        return;
    }

    var url = '/api/book/status/' + encodeURIComponent(reference) + '?token=' + encodeURIComponent(token);

    var timer = setInterval(function () {
        fetch(url)
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (data && data.status && data.status !== 'AwaitingPayment') {
                    clearInterval(timer);
                    window.location.reload();
                }
            })
            .catch(function () { /* transient; keep polling */ });
    }, 30000);
})();
