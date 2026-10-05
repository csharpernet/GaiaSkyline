// Hero background video (Stage 7E-4). The poster <picture> is the LCP element and always renders; the video
// loads only after the page load event + idle time, then fades in over the poster once it can play. It stays
// poster-only when the user prefers reduced motion, Save-Data is on, the connection is slow, or autoplay is
// blocked — and pauses when scrolled out of view or the tab is hidden. A small toggle honours WCAG 2.2.2.
(function () {
  'use strict';

  var container = document.getElementById('hero-video');
  if (!container) return;
  var video = container.querySelector('video');
  if (!video) return;
  var toggle = document.querySelector('[data-hero-toggle]');

  // Poster-only conditions: never show a broken or costly video.
  var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var conn = navigator.connection || navigator.webkitConnection || {};
  var saveData = conn.saveData === true;
  var slow = /(^|-)(slow-2g|2g|3g)$/.test(conn.effectiveType || '');
  if (reduceMotion || saveData || slow) {
    return; // poster stays; the toggle remains hidden
  }

  // Fallback when the browser ignores media= on <source> inside <video>: pick the first source whose media
  // query matches and whose type is playable, and set it directly.
  function pickSource() {
    var sources = video.querySelectorAll('source');
    for (var i = 0; i < sources.length; i++) {
      var s = sources[i];
      var media = s.getAttribute('media');
      if (media && window.matchMedia && !window.matchMedia(media).matches) continue;
      var type = s.getAttribute('type') || '';
      if (type && video.canPlayType && video.canPlayType(type) === '') continue;
      return s.getAttribute('src');
    }
    return null;
  }

  var started = false;
  var userPaused = false;

  function fadeIn() {
    video.classList.remove('opacity-0');
    video.classList.add('opacity-100');
    if (toggle) {
      // Toggle classes, not the `hidden` attribute: the inline-flex display utility would override [hidden].
      toggle.classList.remove('hidden');
      toggle.classList.add('inline-flex');
      updateToggle();
    }
  }

  function posterOnly() {
    if (toggle) {
      toggle.classList.add('hidden');
      toggle.classList.remove('inline-flex');
    }
  }

  function tryPlay() {
    var p = video.play();
    if (p && typeof p.catch === 'function') {
      p.catch(function () { posterOnly(); });
    }
  }

  function start() {
    if (started) return;
    started = true;
    // Mark that the script committed to playing the video (not a poster-only bail). The browser sets
    // video.currentSrc from passive <source> selection regardless, so this is the reliable "started" signal.
    container.setAttribute('data-hero-started', 'true');
    if (!video.currentSrc) {
      var src = pickSource();
      if (src) video.src = src;
    }
    video.load();
    video.addEventListener('playing', fadeIn, { once: true });
    tryPlay();
  }

  function deferredStart() {
    var idle = window.requestIdleCallback || function (cb) { return window.setTimeout(cb, 200); };
    idle(start);
  }

  if (document.readyState === 'complete') {
    deferredStart();
  } else {
    window.addEventListener('load', deferredStart, { once: true });
  }

  // Pause off-screen / when the tab is hidden; resume when visible again (unless the user paused it).
  if ('IntersectionObserver' in window) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!started || userPaused) return;
        if (e.isIntersecting) { tryPlay(); } else { video.pause(); }
      });
    }, { threshold: 0.1 });
    io.observe(video);
  }

  document.addEventListener('visibilitychange', function () {
    if (!started || userPaused) return;
    if (document.hidden) { video.pause(); } else { tryPlay(); }
  });

  function updateToggle() {
    if (!toggle) return;
    var playing = !video.paused;
    toggle.setAttribute('aria-pressed', String(playing));
    toggle.setAttribute('aria-label', playing ? 'Pause background video' : 'Play background video');
    var pauseIcon = toggle.querySelector('[data-icon-pause]');
    var playIcon = toggle.querySelector('[data-icon-play]');
    if (pauseIcon) pauseIcon.hidden = !playing;
    if (playIcon) playIcon.hidden = playing;
  }

  if (toggle) {
    toggle.addEventListener('click', function () {
      if (video.paused) {
        userPaused = false;
        tryPlay();
      } else {
        userPaused = true;
        video.pause();
      }
      updateToggle();
    });
    video.addEventListener('play', updateToggle);
    video.addEventListener('pause', updateToggle);
  }
})();
