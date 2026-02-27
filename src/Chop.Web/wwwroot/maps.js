/* global ymaps */
(function () {
  const state = {
    apiLoading: null,
    maps: new Map(), // elementId -> { map, markers: Map(id -> placemark), resizeObserver? }
  };

  function loadYandexApi(apiKey) {
    if (state.apiLoading) return state.apiLoading;
    if (window.ymaps && typeof window.ymaps.ready === "function") {
      state.apiLoading = Promise.resolve();
      return state.apiLoading;
    }

    state.apiLoading = new Promise((resolve, reject) => {
      const candidates = [
        `https://api-maps.yandex.ru/2.1/?${new URLSearchParams({ apikey: apiKey, lang: "ru_RU" }).toString()}`,
        `https://api-maps.yandex.ru/2.1/?${new URLSearchParams({ lang: "ru_RU" }).toString()}`,
      ];

      let idx = 0;
      const tryNext = () => {
        if (idx >= candidates.length) {
          reject(new Error("Не удалось загрузить скрипт Yandex Maps API."));
          return;
        }

        const script = document.createElement("script");
        script.src = candidates[idx++];
        script.async = true;
        script.onerror = () => tryNext();
        script.onload = () => {
          if (!window.ymaps || typeof window.ymaps.ready !== "function") {
            tryNext();
            return;
          }
          window.ymaps.ready(resolve);
        };
        document.head.appendChild(script);
      };

      tryNext();
    });

    return state.apiLoading;
  }

  function ensureMap(elementId, centerLat, centerLon, zoom) {
    if (state.maps.has(elementId)) return state.maps.get(elementId);
    const el = document.getElementById(elementId);
    if (!el) throw new Error(`Контейнер карты '${elementId}' не найден.`);

    const map = new ymaps.Map(elementId, {
      center: [centerLat, centerLon],
      zoom: zoom,
      controls: ["zoomControl"],
    }, {
      suppressMapOpenBlock: true,
    });

    const entry = { map, markers: new Map(), resizeObserver: null };
    if (typeof ResizeObserver !== "undefined") {
      entry.resizeObserver = new ResizeObserver(() => {
        map.container.fitToViewport();
      });
      entry.resizeObserver.observe(el);
    } else {
      window.addEventListener("resize", () => map.container.fitToViewport());
    }

    setTimeout(() => map.container.fitToViewport(), 0);
    state.maps.set(elementId, entry);
    return entry;
  }

  function makePlacemark(marker) {
    const preset = marker.preset || "islands#redDotIcon";
    const hint = marker.hint || "";
    const balloon = marker.balloon || "";
    return new ymaps.Placemark([marker.lat, marker.lon], {
      hintContent: hint,
      balloonContent: balloon,
    }, {
      preset: preset,
    });
  }

  function setMarkersInternal(entry, markers) {
    const keepIds = new Set(markers.map((m) => m.id));

    for (const [id, placemark] of entry.markers.entries()) {
      if (!keepIds.has(id)) {
        entry.map.geoObjects.remove(placemark);
        entry.markers.delete(id);
      }
    }

    for (const m of markers) {
      const existing = entry.markers.get(m.id);
      if (existing) {
        existing.geometry.setCoordinates([m.lat, m.lon]);
        if (m.hint !== undefined) existing.properties.set("hintContent", m.hint);
        if (m.balloon !== undefined) existing.properties.set("balloonContent", m.balloon);
      } else {
        const placemark = makePlacemark(m);
        entry.map.geoObjects.add(placemark);
        entry.markers.set(m.id, placemark);
      }
    }
  }

  function fitToMarkers(entry) {
    entry.map.container.fitToViewport();
    const objects = entry.map.geoObjects;
    if (objects.getLength() === 0) return;
    const bounds = objects.getBounds();
    if (!bounds) return;
    entry.map.setBounds(bounds, { checkZoomRange: true, zoomMargin: 30 });
  }

  window.chopMaps = {
    async init(elementId, apiKey, centerLat, centerLon, zoom) {
      if (!apiKey) throw new Error("Отсутствует API-ключ Yandex Maps.");
      await loadYandexApi(apiKey);
      ensureMap(elementId, centerLat, centerLon, zoom);
    },

    async setMarkers(elementId, markers) {
      const entry = state.maps.get(elementId);
      if (!entry) throw new Error(`Карта '${elementId}' не инициализирована.`);
      setMarkersInternal(entry, markers || []);
      fitToMarkers(entry);
    },

    async focusMarker(elementId, markerId, zoom) {
      const entry = state.maps.get(elementId);
      if (!entry) throw new Error(`Карта '${elementId}' не инициализирована.`);
      const placemark = entry.markers.get(markerId);
      if (!placemark) return false;
      const coordinates = placemark.geometry.getCoordinates();
      entry.map.setCenter(coordinates, zoom || Math.max(entry.map.getZoom(), 14), { duration: 200 });
      if (placemark.balloon) {
        placemark.balloon.open();
      }
      return true;
    },

    async scrollToElement(elementId) {
      const el = document.getElementById(elementId);
      if (!el) return false;
      el.scrollIntoView({ behavior: "smooth", block: "start" });
      return true;
    },
  };
})();
