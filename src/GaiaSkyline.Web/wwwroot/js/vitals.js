// Reports Core Web Vitals (LCP, INP, CLS, FCP, TTFB) to /api/vitals using the self-hosted
// web-vitals library. In production the server forwards these to Application Insights.
(function () {
    'use strict';
    if (!window.webVitals) {
        return;
    }

    var endpoint = '/api/vitals';

    function send(metric) {
        var body = JSON.stringify({
            name: metric.name,
            value: metric.value,
            rating: metric.rating,
            id: metric.id,
            path: location.pathname
        });
        try {
            if (navigator.sendBeacon) {
                navigator.sendBeacon(endpoint, new Blob([body], { type: 'application/json' }));
            } else {
                fetch(endpoint, { method: 'POST', body: body, headers: { 'Content-Type': 'application/json' }, keepalive: true });
            }
        } catch (e) {
            /* best effort */
        }
    }

    var wv = window.webVitals;
    wv.onLCP(send);
    wv.onINP(send);
    wv.onCLS(send);
    wv.onFCP(send);
    wv.onTTFB(send);
})();
