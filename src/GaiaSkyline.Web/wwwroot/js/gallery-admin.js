// Gallery manager (admin-only, served from 'self'): drag-to-reorder, set hero, add/remove, then save the
// ordered list to the existing cookie-authenticated PUT /api/admin/media/collections/{key}/items endpoint.
(function () {
  'use strict';

  var root = document.querySelector('[data-gallery]');
  if (!root) return;
  var itemsEl = document.getElementById('gallery-items');
  var availEl = document.getElementById('gallery-available');
  var statusEl = document.getElementById('gallery-status');
  var saveBtn = document.getElementById('gallery-save');
  var saveUrl = root.getAttribute('data-save-url');

  function setStatus(msg) { if (statusEl) statusEl.textContent = msg; }

  function badge(ready) {
    return ready === 'true'
      ? ''
      : '<span class="absolute top-1 right-1 text-[10px] uppercase tracking-wide bg-clay text-white rounded px-1">needs alt</span>';
  }

  // Build a gallery-item <li> (draggable, hero radio, remove) from an available tile.
  function makeItem(assetId, ready, thumb) {
    var li = document.createElement('li');
    li.setAttribute('data-asset-id', assetId);
    li.setAttribute('data-ready', ready);
    li.setAttribute('draggable', 'true');
    li.className = 'relative rounded-md border border-fog bg-white overflow-hidden cursor-move';
    li.innerHTML =
      '<div class="relative aspect-[3/2] bg-fog">' +
      '<img src="' + thumb + '" alt="" loading="lazy" class="absolute inset-0 h-full w-full object-cover" />' + badge(ready) +
      '</div>' +
      '<div class="flex items-center justify-between px-2 py-1.5 text-xs">' +
      '<label class="flex items-center gap-1"><input type="radio" name="hero" value="' + assetId + '" data-hero /> hero</label>' +
      '<button type="button" data-remove class="text-clay hover:underline">Remove</button>' +
      '</div>';
    wireItem(li);
    return li;
  }

  // ----- Drag and drop reorder -----
  var dragging = null;
  function wireItem(li) {
    li.addEventListener('dragstart', function () { dragging = li; li.classList.add('opacity-50'); });
    li.addEventListener('dragend', function () { li.classList.remove('opacity-50'); dragging = null; });
    li.querySelector('[data-remove]').addEventListener('click', function () { li.remove(); });
  }

  itemsEl.addEventListener('dragover', function (e) {
    e.preventDefault();
    if (!dragging) return;
    var after = afterElement(itemsEl, e.clientY, e.clientX);
    if (after == null) { itemsEl.appendChild(dragging); }
    else { itemsEl.insertBefore(dragging, after); }
  });

  function afterElement(container, y, x) {
    var tiles = Array.prototype.slice.call(container.querySelectorAll('li[data-asset-id]:not(.opacity-50)'));
    var closest = null, closestDist = Number.NEGATIVE_INFINITY;
    tiles.forEach(function (tile) {
      var box = tile.getBoundingClientRect();
      // Offset to the tile centre; the largest negative offset is the nearest tile we're before.
      var offset = (y - box.top - box.height / 2) + (x - box.left - box.width / 2) * 0.0001;
      if (offset < 0 && offset > closestDist) { closestDist = offset; closest = tile; }
    });
    return closest;
  }

  // Existing items get wired on load.
  Array.prototype.forEach.call(itemsEl.querySelectorAll('li[data-asset-id]'), wireItem);

  // ----- Add from available -----
  if (availEl) {
    availEl.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-add]');
      if (!btn) return;
      var li = btn.closest('li[data-asset-id]');
      if (!li) return;
      var img = li.querySelector('img');
      itemsEl.appendChild(makeItem(li.getAttribute('data-asset-id'), li.getAttribute('data-ready'), img ? img.getAttribute('src') : ''));
      li.remove();
    });
  }

  // ----- Save -----
  saveBtn.addEventListener('click', async function () {
    var tiles = Array.prototype.slice.call(itemsEl.querySelectorAll('li[data-asset-id]'));
    var heroInput = itemsEl.querySelector('input[data-hero]:checked');
    var heroId = heroInput ? heroInput.value : (tiles[0] ? tiles[0].getAttribute('data-asset-id') : null);
    var payload = tiles.map(function (t) {
      var id = t.getAttribute('data-asset-id');
      return { mediaAssetId: id, isHero: id === heroId };
    });

    setStatus('Saving…');
    try {
      var res = await fetch(saveUrl, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify(payload),
      });
      if (res.ok) { setStatus('Saved. Reloading…'); window.location.reload(); }
      else { setStatus('Save failed (' + res.status + ').'); }
    } catch (err) {
      setStatus('Save failed — check your connection.');
    }
  });
})();
