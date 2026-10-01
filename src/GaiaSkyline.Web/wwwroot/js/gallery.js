(function () {
    'use strict';

    var lightbox = document.getElementById('lightbox');
    var image = document.getElementById('lightbox-image');
    var grid = document.getElementById('gallery-grid');
    if (!lightbox || !image || !grid) {
        return;
    }

    var closeBtn = lightbox.querySelector('[data-gallery-close]');
    var prevBtn = lightbox.querySelector('[data-gallery-prev]');
    var nextBtn = lightbox.querySelector('[data-gallery-next]');

    var thumbs = Array.prototype.slice.call(grid.querySelectorAll('[data-gallery-open]'));
    var images = thumbs.map(function (btn) {
        var img = btn.querySelector('img');
        return { src: img.getAttribute('src'), alt: img.getAttribute('alt') || '' };
    });
    var current = 0;
    var opener = null;

    function setParam(value) {
        var url = new URL(window.location.href);
        if (value === null) {
            url.searchParams.delete('p');
        } else {
            url.searchParams.set('p', String(value));
        }
        window.history.replaceState(null, '', url.toString());
    }

    function show(index) {
        current = (index + images.length) % images.length;
        image.setAttribute('src', images[current].src);
        image.setAttribute('alt', images[current].alt);
        setParam(current + 1);
    }

    function onKey(e) {
        if (e.key === 'Escape') {
            close();
        } else if (e.key === 'ArrowRight') {
            show(current + 1);
        } else if (e.key === 'ArrowLeft') {
            show(current - 1);
        } else if (e.key === 'Tab') {
            var focusables = lightbox.querySelectorAll('button');
            var first = focusables[0];
            var last = focusables[focusables.length - 1];
            if (e.shiftKey && document.activeElement === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        }
    }

    function open(index) {
        opener = document.activeElement;
        show(index);
        lightbox.classList.remove('hidden');
        lightbox.classList.add('flex');
        document.body.style.overflow = 'hidden';
        closeBtn.focus();
        document.addEventListener('keydown', onKey);
    }

    function close() {
        lightbox.classList.add('hidden');
        lightbox.classList.remove('flex');
        document.body.style.overflow = '';
        document.removeEventListener('keydown', onKey);
        setParam(null);
        if (opener && opener.focus) {
            opener.focus();
        }
    }

    closeBtn.addEventListener('click', close);
    prevBtn.addEventListener('click', function () { show(current - 1); });
    nextBtn.addEventListener('click', function () { show(current + 1); });
    lightbox.addEventListener('click', function (e) { if (e.target === lightbox) { close(); } });
    thumbs.forEach(function (btn, i) { btn.addEventListener('click', function () { open(i); }); });

    var p = new URL(window.location.href).searchParams.get('p');
    if (p) {
        var n = parseInt(p, 10);
        if (n >= 1 && n <= images.length) {
            open(n - 1);
        }
    }
})();
