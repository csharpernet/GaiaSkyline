// Checkout: mount the Stripe Payment Element, then on submit create the booking + PaymentIntent
// server-side (which re-quotes — the client price is never trusted) and confirm the payment.
(function () {
    'use strict';

    var root = document.getElementById('checkout');
    if (!root || typeof Stripe === 'undefined') {
        return;
    }

    var stripe = Stripe(root.getAttribute('data-pk'));
    var elements = stripe.elements({
        mode: 'payment',
        amount: parseInt(root.getAttribute('data-amount'), 10),
        currency: 'eur',
    });
    elements.create('payment').mount('#payment-element');

    var form = document.getElementById('checkout-form');
    var payButton = document.getElementById('pay');
    var errorEl = document.getElementById('payment-error');
    var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');

    function value(id) {
        var el = document.getElementById(id);
        return el ? el.value : '';
    }

    function showError(message) {
        errorEl.textContent = message || 'Something went wrong. Please try again.';
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        showError('');
        payButton.disabled = true;

        elements.submit().then(function (submitResult) {
            if (submitResult.error) {
                showError(submitResult.error.message);
                payButton.disabled = false;
                return;
            }

            var body = {
                checkIn: root.getAttribute('data-checkin'),
                checkOut: root.getAttribute('data-checkout'),
                adults: parseInt(root.getAttribute('data-adults'), 10),
                children: parseInt(root.getAttribute('data-children'), 10),
                infants: parseInt(root.getAttribute('data-infants'), 10),
                promoCode: root.getAttribute('data-promo') || null,
                language: root.getAttribute('data-language'),
                guestName: value('name'),
                guestEmail: value('email'),
                guestPhone: value('phone'),
                guestCountry: value('country'),
                arrivalEstimateLocal: value('arrival') || null,
                specialRequests: value('requests') || null,
                createAccount: document.getElementById('create-account').checked,
            };

            fetch('/api/book/checkout', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': tokenInput ? tokenInput.value : '',
                },
                body: JSON.stringify(body),
            })
                .then(function (res) {
                    if (res.status === 409) {
                        showError('Those dates were just taken — please choose new dates.');
                        return null;
                    }
                    if (res.status === 422) {
                        return res.json().then(function (data) {
                            showError((data.errors && data.errors[0]) || data.error || 'Please check your details.');
                            return null;
                        });
                    }
                    if (!res.ok) {
                        showError('Couldn’t start checkout. Please try again.');
                        return null;
                    }
                    return res.json();
                })
                .then(function (data) {
                    if (!data) {
                        payButton.disabled = false;
                        return;
                    }
                    return stripe.confirmPayment({
                        elements: elements,
                        clientSecret: data.clientSecret,
                        confirmParams: { return_url: data.returnUrl },
                    }).then(function (confirmResult) {
                        if (confirmResult.error) {
                            showError(confirmResult.error.message);
                            payButton.disabled = false;
                        }
                        // On success Stripe redirects the browser to return_url (the confirmation page).
                    });
                })
                .catch(function () {
                    showError('Couldn’t start checkout. Please try again.');
                    payButton.disabled = false;
                });
        });
    });
})();
