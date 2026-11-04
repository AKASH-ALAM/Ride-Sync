// Driver screen: go online/offline, heartbeat + location over SignalR, optional "simulate driving" demo mode.
(function () {
  const $ = (s) => document.querySelector(s);
  const get = (k) => { try { return localStorage.getItem(k); } catch { return null; } };
  const set = (k, v) => { try { localStorage.setItem(k, v); } catch { } };
  let online = get('rs_online') === '1', sim = get('rs_sim') === '1', pos = null, marker = null, ride = null, centered = false;

  function ui() {
    document.body.dataset.online = online;
    $('#btnOnline').classList.toggle('on', online);
    $('#btnOnline').querySelector('.t').textContent = online ? "You're online" : 'Go online';
    $('#btnSim').classList.toggle('on', sim);
    $('#btnSim').querySelector('.t').textContent = 'Simulate driving: ' + (sim ? 'on' : 'off');
  }

  // Real GPS (ignored in simulation mode). Falls back to central Dhaka so the demo works on any laptop.
  if (navigator.geolocation) navigator.geolocation.watchPosition((p) => { if (!sim) setPos([p.coords.latitude, p.coords.longitude]); }, () => {}, { enableHighAccuracy: true });
  setTimeout(() => { if (!pos) setPos([RS.DHAKA[0] + (Math.random() - 0.5) * 0.01, RS.DHAKA[1] + (Math.random() - 0.5) * 0.01]); }, 2500);

  function setPos(p) {
    pos = p;
    if (!marker) marker = L.marker(p, { icon: RS.carIcon('🚗'), zIndexOffset: 1000 }).addTo(RS.map); else marker.setLatLng(p);
    if (!centered && !RS.ride) { RS.map.setView(p, 14); centered = true; }
  }

  RS.onPanel = (r) => { ride = r; };

  // Heartbeat: marks the driver online in Redis (expires after 30 s of silence) and relays position to the rider on a trip.
  async function beat() {
    if (!online || !pos || RS.hub.state !== 'Connected') return;
    if (sim && ride && (ride.status === 'accepted' || ride.status === 'driver_arrived' || ride.status === 'in_progress')) {
      const target = ride.status === 'in_progress' ? [ride.dlat, ride.dlng] : [ride.plat, ride.plng];
      const dLat = target[0] - pos[0], dLng = target[1] - pos[1], dist = Math.hypot(dLat, dLng), step = 0.0007;   // ≈ 75 m per tick
      if (dist > step) setPos([pos[0] + (dLat / dist) * step, pos[1] + (dLng / dist) * step]); else setPos(target);
    }
    const active = ride && ['accepted', 'driver_arrived', 'in_progress'].includes(ride.status) ? ride.id : null;
    try { await RS.hub.invoke('UpdateLocation', pos[0], pos[1], active); } catch { }
  }
  setInterval(beat, 1500);
  setInterval(() => { if (online && !ride) RS.refreshPanel(); }, 15000);   // safety net in case a realtime event is missed

  $('#btnOnline').onclick = async () => {
    online = !online; set('rs_online', online ? '1' : '0'); ui();
    if (online) { if (pos) await RS.hub.invoke('GoOnline', pos[0], pos[1]).catch(() => {}); RS.refreshPanel(); }
    else await RS.hub.invoke('GoOffline').catch(() => {});
  };
  $('#btnSim').onclick = () => { sim = !sim; set('rs_sim', sim ? '1' : '0'); ui(); if (sim && !pos) setPos(RS.DHAKA); };

  RS.onConnected = () => { if (online && pos) RS.hub.invoke('GoOnline', pos[0], pos[1]).catch(() => {}); };
  RS.onReconnected = RS.onConnected;
  ui();
})();
