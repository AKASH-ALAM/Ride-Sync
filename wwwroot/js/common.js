// Shared map + realtime plumbing for the rider and driver screens.
(function () {
  const DHAKA = [23.7808, 90.41];
  const RS = (window.RS = { DHAKA });

  RS.map = L.map('map', { zoomControl: false }).setView(DHAKA, 12);
  L.control.zoom({ position: 'topright' }).addTo(RS.map);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors', maxZoom: 19 }).addTo(RS.map);
  RS.layer = L.layerGroup().addTo(RS.map);     // the active ride (markers + route)
  RS.preview = L.layerGroup().addTo(RS.map);   // booking preview (rider)

  RS.pin = (cls) => L.divIcon({ className: '', html: `<div class="pin ${cls || ''}"></div>`, iconSize: [18, 18], iconAnchor: [9, 9] });
  RS.carIcon = (emoji) => L.divIcon({ className: '', html: `<div class="pin car">${emoji || '🚗'}</div>`, iconSize: [34, 34], iconAnchor: [17, 17] });

  RS.haversine = (a, b) => {
    const r = (d) => (d * Math.PI) / 180, dLat = r(b[0] - a[0]), dLng = r(b[1] - a[1]);
    const h = Math.sin(dLat / 2) ** 2 + Math.cos(r(a[0])) * Math.cos(r(b[0])) * Math.sin(dLng / 2) ** 2;
    return 6371 * 2 * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
  };

  RS.toast = (msg, err) => {
    if (!msg) return;
    const el = document.createElement('div');
    el.className = 'rs-toast' + (err ? ' err' : '');
    el.textContent = msg;
    document.getElementById('toasts').appendChild(el);
    setTimeout(() => el.remove(), 4000);
  };

  // Road geometry from the public OSRM demo server — purely visual. Prices always come from the server.
  const routeCache = {};
  RS.route = async (a, b) => {
    const key = [a, b].join('|');
    if (routeCache[key]) return routeCache[key];
    try {
      const r = await fetch(`https://router.project-osrm.org/route/v1/driving/${a[1]},${a[0]};${b[1]},${b[0]}?overview=full&geometries=geojson`);
      const j = await r.json();
      return (routeCache[key] = j.routes[0].geometry.coordinates.map((c) => [c[1], c[0]]));
    } catch { return [a, b]; }
  };

  // Draw the active ride (pickup, drop, route) from the #rideData element rendered by the server panel.
  let drawnFor = null;
  async function draw(ride) {
    RS.layer.clearLayers();
    if (!ride) { drawnFor = null; return; }
    const a = [ride.plat, ride.plng], b = [ride.dlat, ride.dlng];
    L.marker(a, { icon: RS.pin() }).addTo(RS.layer);
    L.marker(b, { icon: RS.pin('sq') }).addTo(RS.layer);
    const pts = await RS.route(a, b);
    RS.layer.clearLayers();
    L.polyline(pts, { color: '#000', weight: 5 }).addTo(RS.layer);
    L.marker(a, { icon: RS.pin() }).addTo(RS.layer);
    L.marker(b, { icon: RS.pin('sq') }).addTo(RS.layer);
    if (drawnFor !== ride.id) { RS.map.fitBounds(L.latLngBounds(pts), { padding: [70, 70] }); drawnFor = ride.id; }
  }

  RS.afterPanel = () => {
    const d = document.getElementById('rideData');
    RS.ride = d ? JSON.parse(d.dataset.ride) : null;
    draw(RS.ride);
    if (RS.onPanel) RS.onPanel(RS.ride);
  };

  RS.refreshPanel = async () => {
    const el = document.getElementById('panel');
    try {
      const r = await fetch('/Rides/Panel', { headers: { 'X-Requested-With': 'fetch' } });
      if (r.redirected || r.status === 401) { location.href = '/Account/Login'; return; }
      if (r.status === 429) { RS.toast('Too many requests — slow down a little', true); return; }
      el.innerHTML = await r.text();
      RS.afterPanel();
    } catch { /* offline: keep the last panel */ }
  };

  // Every <form data-ajax> posts with fetch, shows the server's message as a toast, then re-renders the panel.
  document.addEventListener('submit', async (e) => {
    const f = e.target.closest('form[data-ajax]');
    if (!f) return;
    e.preventDefault();
    const btn = e.submitter;
    const body = new FormData(f, btn);   // build BEFORE disabling: a disabled submit button is left out (it carries the star rating's value)
    if (btn) btn.disabled = true;
    try {
      const r = await fetch(f.action, { method: 'POST', body, headers: { 'X-Requested-With': 'fetch' } });
      if (r.status === 429) RS.toast('Too many requests — try again in a moment', true);
      else { const j = await r.json(); RS.toast(j.message, !j.ok); }
    } catch { RS.toast('Network error — please try again', true); }
    await RS.refreshPanel();
  });

  // Realtime: SignalR (Redis backplane on the server). Any ride event → just re-fetch the panel.
  RS.hub = new signalR.HubConnectionBuilder().withUrl('/hubs/ride').withAutomaticReconnect().build();
  RS.hub.on('Refresh', () => RS.refreshPanel());
  RS.hub.on('DriverLocation', (p) => RS.onDriverLocation && RS.onDriverLocation(p));
  RS.hub.onreconnected(() => { RS.refreshPanel(); if (RS.onReconnected) RS.onReconnected(); });
  RS.start = async () => { try { await RS.hub.start(); if (RS.onConnected) RS.onConnected(); } catch { setTimeout(RS.start, 3000); } };

  RS.refreshPanel();
  RS.start();
})();
