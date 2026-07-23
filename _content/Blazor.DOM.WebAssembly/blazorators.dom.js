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

export function hasGlobal(path) {
    try {
        const value = getGlobal(path);
        return value !== null && value !== undefined;
    }
    catch {
        return false;
    }
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

export function selectUnionArm(value, arms) {
    for (let index = 0; index < arms.length; index++) {
        const arm = arms[index];
        if (arm.kind === 'string-literal' &&
            typeof value === 'string' &&
            value === arm.literal) {
            return index;
        }
    }
    for (let index = 0; index < arms.length; index++) {
        const arm = arms[index];
        if (arm.kind === 'string' && typeof value === 'string') return index;
        if (arm.kind === 'reference') {
            const ctor = globalThis[arm.brand];
            if (typeof ctor === 'function' && value instanceof ctor) return index;
        }
    }
    throw new TypeError('JavaScript value does not match a proven union arm.');
}

export async function invokeMethodUnion(
    ref,
    name,
    args,
    arms,
    dotnetRef,
    jsonCallbackMethodName,
    referenceCallbackMethodName) {
    const value = await ref[name](...(args ?? []));
    const armIndex = selectUnionArm(value, arms);
    const arm = arms[armIndex];
    if (arm.kind !== 'reference') {
        const accepted = await dotnetRef.invokeMethodAsync(
            jsonCallbackMethodName,
            armIndex,
            value);
        _requireDeliveryAcknowledgement(accepted, 'union JSON');
        return;
    }

    const reference = DotNet.createJSObjectReference(value);
    try {
        const accepted = await dotnetRef.invokeMethodAsync(
            referenceCallbackMethodName,
            armIndex,
            reference);
        _requireDeliveryAcknowledgement(accepted, 'union reference');
    } catch (error) {
        _disposeJSReference(reference);
        throw error;
    }
}

export async function getPropertyDotNetObjectReference(
    ref,
    name,
    dotnetRef,
    callbackMethodName) {
    await _sendDotNetObjectReference(
        ref[name],
        dotnetRef,
        callbackMethodName);
}

export async function invokeMethodDotNetObjectReference(
    ref,
    name,
    args,
    dotnetRef,
    callbackMethodName) {
    const value = await ref[name](...(args ?? []));
    await _sendDotNetObjectReference(value, dotnetRef, callbackMethodName);
}

/**
 * Invoke a method whose one-shot callback receives a live JS object. The
 * callback object is passed through Blazor's supported JS object-reference
 * table and the returned Promise remains pending until the managed callback
 * finishes.
 *
 * @param {object} ref
 * @param {string} name
 * @param {Array} args Arguments excluding the callback.
 * @param {number} callbackArgumentIndex Callback insertion index.
 * @param {DotNetObjectReference} dotnetRef
 * @param {string} callbackMethodName
 * @returns {Promise<void>}
 */
export function invokeMethodReferenceCallback(
    ref,
    name,
    args,
    callbackArgumentIndex,
    dotnetRef,
    callbackMethodName) {
    return new Promise((resolve, reject) => {
        const invocationArgs = [...(args ?? [])];
        if (!Number.isInteger(callbackArgumentIndex) ||
            callbackArgumentIndex < 0 ||
            callbackArgumentIndex > invocationArgs.length) {
            reject(new RangeError(`Invalid callback insertion index ${callbackArgumentIndex}.`));
            return;
        }

        let settled = false;
        const callback = (value) => {
            if (settled) return;
            settled = true;
            Promise.resolve(
                _sendDotNetObjectReference(
                    value,
                    dotnetRef,
                    callbackMethodName))
                .then(resolve, reject);
        };
        invocationArgs.splice(callbackArgumentIndex, 0, callback);

        try {
            const invocation = ref[name](...invocationArgs);
            if (invocation && typeof invocation.then === 'function') {
                invocation.catch((error) => {
                    if (!settled) {
                        settled = true;
                        reject(error);
                    }
                });
            }

        } catch (error) {
            settled = true;
            reject(error);
        }
    });
}

/**
 * Invoke a method whose one-shot callback receives a live JS object and returns
 * the JSON-valued result used to settle the JavaScript operation.
 */
export function invokeMethodReferenceResultCallback(
    ref,
    name,
    args,
    callbackArgumentIndex,
    dotnetRef,
    callbackMethodName) {
    const invocationArgs = [...(args ?? [])];
    if (!Number.isInteger(callbackArgumentIndex) ||
        callbackArgumentIndex < 0 ||
        callbackArgumentIndex > invocationArgs.length) {
        throw new RangeError(`Invalid callback insertion index ${callbackArgumentIndex}.`);
    }

    const callback = async (value) => {
        const reference = value === null || value === undefined
            ? null
            : DotNet.createJSObjectReference(value);
        try {
            const delivery = await dotnetRef.invokeMethodAsync(
                callbackMethodName,
                reference);
            if (delivery?.accepted !== true) {
                throw new Error('.NET rejected callback reference delivery.');
            }
            return delivery.result;
        } catch (error) {
            _disposeJSReference(reference);
            throw error;
        }
    };
    invocationArgs.splice(callbackArgumentIndex, 0, callback);
    return ref[name](...invocationArgs);
}

/**
 * Construct an object whose persistent callback receives two live JS objects.
 */
export function constructReferencePairCallback(
    constructorPath,
    args,
    callbackArgumentIndex,
    dotnetRef,
    callbackMethodName) {
    const ctor = _resolvePath(constructorPath);
    const constructorArgs = [...(args ?? [])];
    if (!Number.isInteger(callbackArgumentIndex) ||
        callbackArgumentIndex < 0 ||
        callbackArgumentIndex > constructorArgs.length) {
        throw new RangeError(`Invalid callback insertion index ${callbackArgumentIndex}.`);
    }
    const callback = (first, second) => {
        const firstReference = DotNet.createJSObjectReference(first);
        const secondReference = DotNet.createJSObjectReference(second);
        dotnetRef.invokeMethodAsync(
            callbackMethodName,
            firstReference,
            secondReference)
            .then((accepted) => {
                if (accepted !== true) {
                    _disposeJSReference(firstReference);
                    _disposeJSReference(secondReference);
                }
            })
            .catch((error) => {
                _disposeJSReference(firstReference);
                _disposeJSReference(secondReference);
                console.error('[blazorators.dom] callback delivery failed:', error);
            });
    };
    constructorArgs.splice(callbackArgumentIndex, 0, callback);
    return new ctor(...constructorArgs);
}

/**
 * Pass Blob, ArrayBuffer, or typed-array bytes to .NET through the official
 * JS stream-reference path.
 *
 * @param {Blob|ArrayBuffer|ArrayBufferView|null|undefined} value
 * @param {DotNetObjectReference} dotnetRef
 * @param {string} callbackMethodName
 * @returns {Promise<void>}
 */
export async function createDotNetStreamReference(value, dotnetRef, callbackMethodName) {
    if (value === null || value === undefined) {
        const accepted = await dotnetRef.invokeMethodAsync(
            callbackMethodName,
            null,
            0,
            false);
        _requireDeliveryAcknowledgement(accepted, 'stream');
        return;
    }

    const length = value instanceof Blob
        ? value.size
        : value instanceof ArrayBuffer || ArrayBuffer.isView(value)
            ? value.byteLength
            : -1;
    if (length < 0) {
        throw new TypeError('Supplied value is not an ArrayBuffer, typed array, or Blob.');
    }
    if (length === 0) {
        const accepted = await dotnetRef.invokeMethodAsync(
            callbackMethodName,
            null,
            0,
            true);
        _requireDeliveryAcknowledgement(accepted, 'stream');
        return;
    }

    const streamReference = DotNet.createJSStreamReference(value);
    try {
        const accepted = await dotnetRef.invokeMethodAsync(
            callbackMethodName,
            streamReference,
            length,
            true);
        _requireDeliveryAcknowledgement(accepted, 'stream');
    } catch (error) {
        _disposeJSReference(streamReference);
        throw error;
    }
}

/**
 * Invoke a method, await its Blob/ArrayBuffer/typed-array result, and pass the
 * bytes to .NET through an official JS stream reference.
 *
 * @param {object} ref
 * @param {string} name
 * @param {Array} args
 * @param {DotNetObjectReference} dotnetRef
 * @param {string} callbackMethodName
 * @returns {Promise<void>}
 */
export async function invokeMethodDotNetStreamReference(
    ref,
    name,
    args,
    dotnetRef,
    callbackMethodName) {
    const value = await ref[name](...(args ?? []));
    await createDotNetStreamReference(value, dotnetRef, callbackMethodName);
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

export async function getIndexDotNetObjectReference(
    ref,
    index,
    dotnetRef,
    callbackMethodName) {
    await _sendDotNetObjectReference(
        ref[index],
        dotnetRef,
        callbackMethodName);
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
 * @param {DotNetObjectReference} registrationDotnetRef Registration receiver
 * @param {string} registrationCallbackMethodName Registration callback method
 * @param {boolean|AddEventListenerOptions|null} options
 * @returns {Promise<void>}
 */
export async function addDotNetEventListener(
    target,
    type,
    dotnetRef,
    callbackMethodName,
    registrationDotnetRef,
    registrationCallbackMethodName,
    options) {
    const id = _nextListenerId++;
    const listener = (event) => {
        const eventData = _serializeEvent(event);
        dotnetRef.invokeMethodAsync(callbackMethodName, JSON.stringify(eventData))
            .catch((err) => console.error(`[blazorators.dom] event callback error (${type}):`, err));
    };
    target.addEventListener(type, listener, options ?? false);
    _listeners.set(id, { target, type, listener, dotnetRef, options });
    try {
        const accepted = await registrationDotnetRef.invokeMethodAsync(
            registrationCallbackMethodName,
            id);
        _requireDeliveryAcknowledgement(accepted, 'event registration');
    } catch (error) {
        removeDotNetEventListener(id);
        throw error;
    }
}

/**
 * Attach a typed event listener without a manual object-handle registry. Each
 * event is represented by DotNet.createJSObjectReference and the returned
 * registration object is itself owned by IJSObjectReference on the .NET side.
 *
 * @param {EventTarget} target
 * @param {string} type
 * @param {DotNetObjectReference} dotnetRef
 * @param {string} callbackMethodName
 * @param {DotNetObjectReference} registrationDotnetRef
 * @param {string} registrationCallbackMethodName
 * @param {boolean|AddEventListenerOptions|null} options
 * @returns {Promise<void>}
 */
export async function addDotNetReferenceEventListener(
    target,
    type,
    dotnetRef,
    callbackMethodName,
    registrationDotnetRef,
    registrationCallbackMethodName,
    options) {
    let disposed = false;
    const listener = (event) => {
        if (disposed) return;
        _sendDotNetObjectReference(event, dotnetRef, callbackMethodName)
            .catch((error) => {
                console.error(`[blazorators.dom] event callback error (${type}):`, error);
            });
    };

    const registration = {
        dispose() {
            if (disposed) return;
            disposed = true;
            target.removeEventListener(type, listener, options ?? false);
        }
    };
    target.addEventListener(type, listener, options ?? false);

    let registrationReference;
    try {
        registrationReference = DotNet.createJSObjectReference(registration);
        const accepted = await registrationDotnetRef.invokeMethodAsync(
            registrationCallbackMethodName,
            registrationReference);
        _requireDeliveryAcknowledgement(accepted, 'event registration');
    } catch (error) {
        registration.dispose();
        _disposeJSReference(registrationReference);
        throw error;
    }
}

/**
 * Remove a previously registered listener and release its dotnet reference.
 *
 * @param {number} id  Value returned by addDotNetEventListener
 */
export function removeDotNetEventListener(id) {
    const entry = _listeners.get(id);
    if (!entry) return;
    entry.target.removeEventListener(
        entry.type,
        entry.listener,
        entry.options ?? false);
    try { entry.dotnetRef.dispose(); } catch { /* already disposed */ }
    _listeners.delete(id);
}

// ─── Internal helpers ─────────────────────────────────────────────────────────

function _disposeJSReference(reference) {
    if (reference === null || reference === undefined) return;
    try {
        DotNet.disposeJSObjectReference(reference);
    } catch {
        // The managed side may already have released a borrowed reference.
    }
}

async function _sendDotNetObjectReference(value, dotnetRef, callbackMethodName) {
    const reference = value === null || value === undefined
        ? null
        : DotNet.createJSObjectReference(value);
    try {
        const accepted = await dotnetRef.invokeMethodAsync(
            callbackMethodName,
            reference);
        _requireDeliveryAcknowledgement(accepted, 'object reference');
    } catch (error) {
        _disposeJSReference(reference);
        throw error;
    }
}

function _requireDeliveryAcknowledgement(accepted, kind) {
    if (accepted !== true) {
        throw new Error(`.NET rejected ${kind} delivery; JavaScript rolled it back.`);
    }
}

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
