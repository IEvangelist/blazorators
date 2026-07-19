// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

// Shared ES module for Blazor DOM interop.
// Exposes low-level primitives (global lookup, property get/set, method
// invocation, constructor, index access, event add/remove) that the C# runtime
// layer targets.  The Blazor JS interop engine marshals IJSObjectReference
// handles across the boundary so this module never maintains its own object
// registry; Blazor's handle table is the sole authority.

'use strict';

// ─── Global lookup ────────────────────────────────────────────────────────────

/**
 * Resolve a dotted path from the global scope (window).
 * @param {string} path  e.g. "window", "document", "navigator.geolocation"
 * @returns {*}
 */
export function getGlobal(path) {
    if (!path || path === 'window') return window;
    const parts = path.split('.');
    let obj = window;
    for (const part of parts) {
        if (obj === null || obj === undefined) return obj;
        obj = obj[part];
    }
    return obj;
}

// ─── Property access ──────────────────────────────────────────────────────────

/**
 * @param {object} ref   Live JS object (Blazor handle unwrapped automatically)
 * @param {string} name  Property name
 * @returns {*}
 */
export function getProperty(ref, name) {
    return ref[name];
}

/**
 * @param {object} ref    Live JS object
 * @param {string} name   Property name
 * @param {*}      value  New value
 */
export function setProperty(ref, name, value) {
    ref[name] = value;
}

// ─── Method invocation ────────────────────────────────────────────────────────

/**
 * Invoke a method on a live JS object.  The return value is marshalled by the
 * Blazor runtime: primitives/records are JSON-serialised; if the caller uses
 * InvokeAsync<IJSObjectReference> the returned object gets a JS handle.
 *
 * @param {object}   ref   Live JS object
 * @param {string}   name  Method name
 * @param {Array}    args  Arguments (already unwrapped by Blazor)
 * @returns {*}
 */
export function invokeMethod(ref, name, args) {
    return ref[name](...(args ?? []));
}

// ─── Constructor ──────────────────────────────────────────────────────────────

/**
 * Instantiate a constructor found at a dotted global path.
 *
 * @param {string} ctorPath  e.g. "EventTarget", "URL"
 * @param {Array}  args
 * @returns {object}
 */
export function construct(ctorPath, args) {
    const ctor = getGlobal(ctorPath);
    if (typeof ctor !== 'function') {
        throw new TypeError(`'${ctorPath}' is not a constructor (got ${typeof ctor})`);
    }
    return new ctor(...(args ?? []));
}

// ─── Index access ─────────────────────────────────────────────────────────────

/**
 * @param {object}       ref
 * @param {number|string} index
 * @returns {*}
 */
export function getIndex(ref, index) {
    return ref[index];
}

/**
 * @param {object}       ref
 * @param {number|string} index
 * @param {*}            value
 */
export function setIndex(ref, index, value) {
    ref[index] = value;
}

// ─── Event listeners ──────────────────────────────────────────────────────────

/** @type {Map<number, {target: EventTarget, type: string, listener: function, dotnetRef: DotNetObjectReference}>} */
const _listeners = new Map();
let _nextListenerId = 1;

/**
 * Attach a dotnet-backed event listener.  The dotnet object must expose a
 * [JSInvokable] method with the given name that accepts (string eventJson).
 *
 * @param {EventTarget}          target            DOM target
 * @param {string}               type              Event type, e.g. "click"
 * @param {DotNetObjectReference} dotnetRef        Callback holder (DomCallbackHandler)
 * @param {string}               callbackMethodName JSInvokable method name on the dotnet side
 * @returns {number}             Listener ID – pass to removeEventListener to unsubscribe
 */
export function addDotNetEventListener(target, type, dotnetRef, callbackMethodName) {
    const id = _nextListenerId++;
    const listener = (event) => {
        const eventData = _serializeEvent(event);
        dotnetRef.invokeMethodAsync(callbackMethodName, JSON.stringify(eventData))
            .catch((err) => console.error(`[blazorators.dom] event callback error (${type}):`, err));
    };
    target.addEventListener(type, listener);
    _listeners.set(id, { target, type, listener, dotnetRef });
    return id;
}

/**
 * Remove a previously registered listener and release its dotnet reference.
 *
 * @param {number} id  Value returned by addDotNetEventListener
 */
export function removeDotNetEventListener(id) {
    const entry = _listeners.get(id);
    if (!entry) return;
    entry.target.removeEventListener(entry.type, entry.listener);
    try { entry.dotnetRef.dispose(); } catch { /* already disposed */ }
    _listeners.delete(id);
}

// ─── Internal helpers ─────────────────────────────────────────────────────────

/**
 * Produce a plain-object snapshot of an event suitable for JSON.stringify.
 * Walks prototype chain for getter-only properties; excludes functions,
 * complex objects, and target/currentTarget to avoid circular refs.
 *
 * @param {Event} event
 * @returns {object}
 */
function _serializeEvent(event) {
    const SKIP = new Set(['target', 'currentTarget', 'srcElement', 'relatedTarget', 'path',
        'composedPath', 'view', 'constructor']);
    const result = {};

    let proto = event;
    while (proto && proto !== Object.prototype) {
        for (const key of Object.getOwnPropertyNames(proto)) {
            if (key in result || SKIP.has(key)) continue;
            try {
                const val = event[key];
                const t = typeof val;
                if (t !== 'function' && t !== 'object') {
                    result[key] = val;
                }
            } catch { /* getter may throw */ }
        }
        proto = Object.getPrototypeOf(proto);
    }

    // Always include type and target info as strings
    result.type = event.type;
    if (event.target instanceof Element) {
        result.targetId = event.target.id ?? null;
        result.targetTagName = event.target.tagName ?? null;
    }
    return result;
}
