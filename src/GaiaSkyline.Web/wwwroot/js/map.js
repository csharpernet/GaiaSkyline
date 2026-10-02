// Lazily renders the coarse location map. The self-hosted MapLibre GL library (~800 KB) and its
// stylesheet are only fetched once the map container scrolls near the viewport, so they never touch
// the initial load or the Core Web Vitals budget. The map shows an approximate area (a translucent
// circle), never the exact address. Tile source and attribution come from data-* attributes.
(function () {
    'use strict';

    var el = document.getElementById('location-map');
    if (!el || !('IntersectionObserver' in window)) {
        return;
    }

    var started = false;

    function loadStylesheet(href) {
        if (!href || document.querySelector('link[data-maplibre]')) {
            return;
        }
        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        link.setAttribute('data-maplibre', '');
        document.head.appendChild(link);
    }

    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            var script = document.createElement('script');
            script.src = src;
            script.defer = true;
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    function render() {
        if (!window.maplibregl) {
            return;
        }

        var lat = parseFloat(el.getAttribute('data-lat'));
        var lng = parseFloat(el.getAttribute('data-lng'));
        if (isNaN(lat) || isNaN(lng)) {
            return;
        }
        var zoom = parseFloat(el.getAttribute('data-zoom')) || 13;
        var tiles = el.getAttribute('data-tiles');
        var attribution = el.getAttribute('data-attribution') || '';

        var map = new maplibregl.Map({
            container: el,
            center: [lng, lat],
            zoom: zoom,
            cooperativeGestures: true,
            style: {
                version: 8,
                sources: {
                    basemap: { type: 'raster', tiles: [tiles], tileSize: 256, attribution: attribution }
                },
                layers: [{ id: 'basemap', type: 'raster', source: 'basemap' }]
            }
        });

        map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');

        map.on('load', function () {
            map.addSource('here', {
                type: 'geojson',
                data: { type: 'Feature', geometry: { type: 'Point', coordinates: [lng, lat] }, properties: {} }
            });
            map.addLayer({
                id: 'here-area',
                type: 'circle',
                source: 'here',
                paint: {
                    'circle-radius': 46,
                    'circle-color': '#B85D3A',
                    'circle-opacity': 0.18,
                    'circle-stroke-color': '#B85D3A',
                    'circle-stroke-width': 2,
                    'circle-stroke-opacity': 0.55
                }
            });
        });
    }

    function start() {
        if (started) {
            return;
        }
        started = true;
        loadStylesheet(el.getAttribute('data-css'));
        loadScript(el.getAttribute('data-js')).then(render).catch(function () {
            // The map is non-essential; fail silently and leave the fallback text in place.
        });
    }

    var observer = new IntersectionObserver(function (entries) {
        for (var i = 0; i < entries.length; i++) {
            if (entries[i].isIntersecting) {
                observer.disconnect();
                start();
                break;
            }
        }
    }, { rootMargin: '200px' });

    observer.observe(el);
})();
