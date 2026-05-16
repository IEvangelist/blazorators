// theme.js
// ES module loaded via Blazor JS interop (IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./theme.js")).
// Exposes get/set/resolve/subscribe helpers used by the ThemeToggle component
// and the MapLibre map component (so the map style follows the site theme).
// The pre-paint theme apply lives in index.html as a tiny inline <script> to
// avoid a flash before Blazor boots; that script writes the same localStorage
// key this module reads.

const STORAGE_KEY = 'theme';
const VALID = new Set(['light', 'dark', 'system']);

const subscribers = new Map(); // id -> dotNetRef
let mql = null;
let listenerAttached = false;

function applyResolved(resolved) {
    const root = document.documentElement;
    if (resolved === 'dark') {
        root.classList.add('dark');
    } else {
        root.classList.remove('dark');
    }
    root.setAttribute('data-theme', resolved);
    root.style.colorScheme = resolved;
}

function resolve(theme) {
    if (theme === 'light' || theme === 'dark') {
        return theme;
    }
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    return prefersDark ? 'dark' : 'light';
}

function ensureSystemListener() {
    if (listenerAttached) {
        return;
    }
    listenerAttached = true;
    mql = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = () => {
        // only react when current setting is "system"
        if (getTheme() !== 'system') {
            return;
        }
        const resolved = resolve('system');
        applyResolved(resolved);
        notifySubscribers(resolved);
    };
    if (mql.addEventListener) {
        mql.addEventListener('change', handler);
    } else if (mql.addListener) {
        mql.addListener(handler);
    }
}

function notifySubscribers(resolved) {
    for (const ref of subscribers.values()) {
        try {
            ref.invokeMethodAsync('OnThemeChanged', resolved);
        } catch (err) {
            console.warn('[theme] subscriber notify failed', err);
        }
    }
}

export function getTheme() {
    try {
        const v = localStorage.getItem(STORAGE_KEY);
        return VALID.has(v) ? v : 'system';
    } catch {
        return 'system';
    }
}

export function getResolved() {
    return resolve(getTheme());
}

export function setTheme(value) {
    if (!VALID.has(value)) {
        value = 'system';
    }
    try {
        localStorage.setItem(STORAGE_KEY, value);
    } catch { /* ignore */ }
    const resolved = resolve(value);
    applyResolved(resolved);
    notifySubscribers(resolved);
    return resolved;
}

let nextId = 1;

export function subscribe(dotNetRef) {
    ensureSystemListener();
    const id = nextId++;
    subscribers.set(id, dotNetRef);
    // immediately echo current resolved theme so subscriber can sync up
    try {
        dotNetRef.invokeMethodAsync('OnThemeChanged', getResolved());
    } catch { /* ignore */ }
    return id;
}

export function unsubscribe(id) {
    subscribers.delete(id);
}

// Ensure the resolved theme matches storage on first load (defensive — the
// inline pre-paint script in index.html should already have done this).
applyResolved(resolve(getTheme()));
ensureSystemListener();
