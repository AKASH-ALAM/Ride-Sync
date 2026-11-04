// Rider screen: address search, map picking, server-side fare estimate, live driver tracking.
(function () {
  let pickup = null, drop = null, vehicle = 'car', mapHandler = null, driverMarker = null, estSeq = 0;
  const $ = (s) => document.querySelector(s);
  const money = (n) => '৳' + Math.round(n).toLocaleString();

  RS.onPanel = (ride) => { if (!ride) initBooking(); else { teardownBooking(); updateEta(null); } if (!ride && driverMarker) { driverMarker.remove(); driverMarker = null; } };

  // ───────── live tracking ─────────
  RS.onDriverLocation = (p) => {
    if (!RS.ride || RS.ride.id !== p.rideId) return;
    if (!driverMarker) {
      driverMarker = L.marker([p.lat, p.lng], { icon: RS.carIcon('🚗'), zIndexOffset: 1000 }).addTo(RS.map);
      // first sighting: zoom so the driver AND the pickup are both visible
      RS.map.fitBounds(L.latLngBounds([[p.lat, p.lng], [RS.ride.plat, RS.ride.plng], [RS.ride.dlat, RS.ride.dlng]]), { padding: [80, 80], maxZoom: 15 });
    } else driverMarker.setLatLng([p.lat, p.lng]);
    updateEta([p.lat, p.lng]);
  };
  function updateEta(pos) {
    const el = $('#eta'); if (!el || !pos) return;
    const toPickup = el.dataset.status === 'accepted';
    const target = toPickup ? [RS.ride.plat, RS.ride.plng] : [RS.ride.dlat, RS.ride.dlng];
    const km = RS.haversine(pos, target) * 1.3, min = Math.max(1, Math.round((km / 25) * 60));
    if (el.dataset.status === 'driver_arrived') { el.textContent = 'Meet your driver at the pickup point'; return; }
    el.textContent = toPickup ? `Arriving in about ${min} min · ${km.toFixed(1)} km away` : `About ${min} min to your destination`;
  }

  // ───────── booking ─────────
  function teardownBooking() { if (mapHandler) { RS.map.off('click', mapHandler); mapHandler = null; } RS.preview.clearLayers(); }

  function initBooking() {
    const form = $('#bookForm'); if (!form) return;
    pickup = drop = null; vehicle = 'car'; RS.preview.clearLayers();
    attachSearch($('#pickup'), 'pickup', (p) => setPoint('pickup', p));
    attachSearch($('#drop'), 'drop', (p) => setPoint('drop', p));
    $('#locate').onclick = locate;
    teardownBooking();
    mapHandler = async (e) => {
      const kind = !pickup ? 'pickup' : 'drop';
      const label = await reverse(e.latlng.lat, e.latlng.lng);
      setPoint(kind, { lat: e.latlng.lat, lng: e.latlng.lng, label });
    };
    RS.map.on('click', mapHandler);
  }

  function setPoint(kind, p) {
    const f = $('#bookForm'); if (!f) return;
    if (kind === 'pickup') { pickup = p; f.plat.value = p.lat; f.plng.value = p.lng; f.pickup.value = p.label; }
    else { drop = p; f.dlat.value = p.lat; f.dlng.value = p.lng; f.drop.value = p.label; }
    RS.preview.clearLayers();
    if (pickup) L.marker([pickup.lat, pickup.lng], { icon: RS.pin() }).addTo(RS.preview);
    if (drop) L.marker([drop.lat, drop.lng], { icon: RS.pin('sq') }).addTo(RS.preview);
    if (pickup && !drop) RS.map.setView([pickup.lat, pickup.lng], Math.max(RS.map.getZoom(), 14));
    if (pickup && drop) estimate(); else $('#hint').textContent = 'Now choose where you are going.';
  }

  async function estimate() {
    const seq = ++estSeq;
    $('#hint').textContent = 'Calculating prices…';
    const q = new URLSearchParams({ plat: pickup.lat, plng: pickup.lng, dlat: drop.lat, dlng: drop.lng });
    const r = await fetch('/Rides/Estimate?' + q, { headers: { 'X-Requested-With': 'fetch' } });
    if (seq !== estSeq) return;
    if (!r.ok) { const j = await r.json().catch(() => ({})); $('#hint').textContent = j.message || 'Could not estimate this trip'; $('#estimate').hidden = true; return; }
    const est = await r.json();
    $('#hint').hidden = true; $('#estimate').hidden = false;
    $('#surgeNote').innerHTML = est.surge > 1
      ? `<div class="surge">⚡ Prices are ×${est.surge.toFixed(1)} — more riders than drivers right now.</div>`
      : (est.nearbyDrivers > 0 ? `<div class="chip green mb-2">${est.nearbyDrivers} driver${est.nearbyDrivers > 1 ? 's' : ''} nearby</div>` : '<div class="chip mb-2">No drivers online nearby yet — you can still request</div>');
    $('#options').innerHTML = est.options.map((o) => `
      <button type="button" class="opt ${o.id === vehicle ? 'sel' : ''}" data-id="${o.id}" data-name="${o.name}" data-fare="${o.fare}">
        <span class="ic">${o.icon}</span>
        <span><span class="nm">${o.name} <small>· ${o.seats}</small></span><div class="sub">${o.etaMin} min trip · ${o.tagline}</div></span>
        <span class="price">${money(o.fare)}</span>
      </button>`).join('');
    document.querySelectorAll('#options .opt').forEach((b) => (b.onclick = () => choose(b)));
    choose(document.querySelector(`#options .opt[data-id="${vehicle}"]`) || document.querySelector('#options .opt'));
    const pts = await RS.route([pickup.lat, pickup.lng], [drop.lat, drop.lng]);
    if (seq !== estSeq) return;
    L.polyline(pts, { color: '#000', weight: 5 }).addTo(RS.preview);
    RS.map.fitBounds(L.latLngBounds(pts), { padding: [70, 70] });
  }

  function choose(btn) {
    document.querySelectorAll('#options .opt').forEach((b) => b.classList.remove('sel'));
    btn.classList.add('sel'); vehicle = btn.dataset.id; $('#bookForm').vehicle.value = vehicle;
    $('#confirm').textContent = `Request ${btn.dataset.name} · ${money(btn.dataset.fare)}`;
  }

  function locate() {
    if (!navigator.geolocation) return RS.toast('Geolocation not available in this browser', true);
    navigator.geolocation.getCurrentPosition(async (pos) => {
      const { latitude: lat, longitude: lng } = pos.coords;
      setPoint('pickup', { lat, lng, label: await reverse(lat, lng) });
    }, () => RS.toast('Could not get your location — tap the map instead', true), { enableHighAccuracy: true, timeout: 8000 });
  }

  // ───────── geocoding (OpenStreetMap Nominatim) ─────────
  async function reverse(lat, lng) {
    try {
      const r = await fetch(`https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${lat}&lon=${lng}`);
      const j = await r.json();
      return (j.display_name || `${lat.toFixed(5)}, ${lng.toFixed(5)}`).split(',').slice(0, 3).join(',');
    } catch { return `${lat.toFixed(5)}, ${lng.toFixed(5)}`; }
  }

  // ───────── location dropdown: current location · recent · popular places · live search ─────────
  const PLACES = [
    ['Gulshan 1', 'Gulshan, Dhaka', 23.7806, 90.4167], ['Banani', 'Banani, Dhaka', 23.7937, 90.4066],
    ['Dhanmondi', 'Dhanmondi, Dhaka', 23.7465, 90.3760], ['Mohammadpur', 'Mohammadpur, Dhaka', 23.7662, 90.3586],
    ['Mirpur 10', 'Mirpur, Dhaka', 23.8069, 90.3687], ['Uttara Sector 7', 'Uttara, Dhaka', 23.8759, 90.3795],
    ['Farmgate', 'Tejgaon, Dhaka', 23.7561, 90.3872], ['Shahbag', 'Shahbag, Dhaka', 23.7389, 90.3957],
    ['Motijheel', 'Motijheel, Dhaka', 23.7330, 90.4172], ['Bashundhara R/A', 'Bashundhara, Dhaka', 23.8196, 90.4310],
    ['Hazrat Shahjalal Airport', 'Airport, Dhaka', 23.8433, 90.3978], ['Sadarghat', 'Old Dhaka', 23.7104, 90.4074],
  ].map(([name, area, lat, lng]) => ({ label: `${name}, ${area}`, name, area, lat, lng }));

  const recent = () => { try { return JSON.parse(localStorage.getItem('rs_recent') || '[]'); } catch { return []; } };
  const remember = (p) => {
    try { localStorage.setItem('rs_recent', JSON.stringify([p, ...recent().filter((x) => x.label !== p.label)].slice(0, 4))); } catch { }
  };
  const esc = (t) => String(t).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

  // one global listener closes every open dropdown when you click elsewhere
  if (!window.__rsSuggestClose) {
    window.__rsSuggestClose = true;
    document.addEventListener('click', (e) => document.querySelectorAll('.suggest').forEach((b) => { if (!b.parentElement.contains(e.target)) b.hidden = true; }));
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') document.querySelectorAll('.suggest').forEach((b) => (b.hidden = true)); });
  }

  function attachSearch(input, kind, onPick) {
    const box = input.parentElement.querySelector('.suggest'); let t, seq = 0, remote = [];
    const caret = input.parentElement.querySelector('.caret');

    const item = (cls, icon, title, sub, data) => `<button type="button" class="${cls}" data-d='${esc(JSON.stringify(data))}'><span class="si">${icon}</span><span>${esc(title)}${sub ? `<small>${esc(sub)}</small>` : ''}</span></button>`;

    function render() {
      const q = input.value.trim().toLowerCase();
      const match = (p) => !q || (p.label || '').toLowerCase().includes(q);
      let html = '';
      if (kind === 'pickup') html += item('', '📍', 'Use my current location', 'Uses your device GPS', { act: 'locate' });
      const rec = recent().filter(match);
      if (rec.length) html += '<div class="hd">Recent</div>' + rec.map((p) => item('', '🕘', p.label.split(',')[0], p.label.split(',').slice(1).join(',').trim(), p)).join('');
      const pop = PLACES.filter(match);
      if (pop.length) html += '<div class="hd">Popular places</div>' + pop.map((p) => item('', '📌', p.name, p.area, p)).join('');
      if (remote.length) html += '<div class="hd">Search results</div>' + remote.map((p) => item('', '🔎', p.label.split(',')[0], p.label.split(',').slice(1).join(',').trim(), p)).join('');
      if (!html) html = '<div class="hd">No matches — keep typing to search the map</div>';
      box.innerHTML = html; box.hidden = false;
    }

    box.addEventListener('click', (e) => {
      const b = e.target.closest('button[data-d]'); if (!b) return;
      const d = JSON.parse(b.dataset.d); box.hidden = true;
      clearTimeout(t); seq++; remote = [];            // a place was chosen: cancel any pending search so it can't re-open the list
      if (d.act === 'locate') return locate();
      const p = { lat: d.lat, lng: d.lng, label: d.label }; remember(p); onPick(p);
    });

    const open = () => { remote = []; render(); };
    input.addEventListener('focus', open);
    input.addEventListener('click', () => { if (box.hidden) open(); });
    if (caret) caret.addEventListener('click', () => { if (box.hidden) { input.focus(); open(); } else box.hidden = true; });

    input.addEventListener('input', () => {
      clearTimeout(t); render();                     // instant: filter recent + popular places
      const q = input.value.trim();
      if (q.length < 3) { remote = []; return; }
      t = setTimeout(async () => {                    // debounced live search (Nominatim asks for ≤ 1 request/second)
        const my = ++seq;
        try {
          const c = RS.map.getCenter();
          const r = await fetch(`https://nominatim.openstreetmap.org/search?format=jsonv2&limit=5&q=${encodeURIComponent(q)}&viewbox=${c.lng - 1},${c.lat + 1},${c.lng + 1},${c.lat - 1}`);
          const res = await r.json();
          if (my !== seq || document.activeElement !== input) return;   // stale, or the user already moved on
          remote = res.map((x) => ({ label: x.display_name.split(',').slice(0, 3).join(','), lat: +x.lat, lng: +x.lon }));
          render();
        } catch { /* offline: presets still work */ }
      }, 450);
    });
  }
})();
