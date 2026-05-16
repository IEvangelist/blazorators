// map.js
// ES module loaded via Blazor JS interop.
// MapLibre GL JS is itself loaded dynamically via `await import(...)` from an
// ESM CDN so it never sits in a top-level <script> tag. Tiles come from
// OpenFreeMap (https://openfreemap.org) — MIT, OpenStreetMap data, no key.

const MAPLIBRE_ESM = 'https://esm.sh/maplibre-gl@4.7.1';
const STYLES = {
    light: 'https://tiles.openfreemap.org/styles/positron',
    dark: 'https://tiles.openfreemap.org/styles/dark'
};

let maplibrePromise = null;
const instances = new Map(); // containerId -> { map, marker }

function loadMaplibre() {
    if (!maplibrePromise) {
        maplibrePromise = import(/* webpackIgnore: true */ MAPLIBRE_ESM)
            .then(mod => mod.default ?? mod);
    }
    return maplibrePromise;
}

function styleForTheme(theme) {
    return STYLES[theme === 'dark' ? 'dark' : 'light'];
}

export async function createMap(containerId, latitude, longitude, theme) {
    const el = document.getElementById(containerId);
    if (!el) {
        console.warn(`[map] container '${containerId}' not found`);
        return false;
    }

    // tear down any prior instance for this container
    await dispose(containerId);

    const maplibregl = await loadMaplibre();

    const map = new maplibregl.Map({
        container: el,
        style: styleForTheme(theme),
        center: [longitude, latitude],
        zoom: 14,
        attributionControl: { compact: true },
        cooperativeGestures: false
    });

    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');
    map.addControl(new maplibregl.GeolocateControl({
        positionOptions: { enableHighAccuracy: true },
        trackUserLocation: false
    }), 'top-right');

    const marker = new maplibregl.Marker({ color: getCssVar('--primary-hex') })
        .setLngLat([longitude, latitude])
        .addTo(map);

    instances.set(containerId, { map, marker, maplibregl });
    return true;
}

export function setCenter(containerId, latitude, longitude) {
    const inst = instances.get(containerId);
    if (!inst) return;
    inst.map.setCenter([longitude, latitude]);
    inst.marker.setLngLat([longitude, latitude]);
}

export function setTheme(containerId, theme) {
    const inst = instances.get(containerId);
    if (!inst) return;
    inst.map.setStyle(styleForTheme(theme));
}

export async function dispose(containerId) {
    const inst = instances.get(containerId);
    if (!inst) {
        return;
    }
    try {
        inst.map.remove();
    } catch { /* ignore */ }
    instances.delete(containerId);
}

function getCssVar(name) {
    // MapLibre Marker expects a hex; resolve from CSS custom property if set,
    // otherwise fall back to a sensible default that matches the primary token.
    try {
        const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        if (v) return v;
    } catch { /* ignore */ }
    return '#7c3aed';
}
