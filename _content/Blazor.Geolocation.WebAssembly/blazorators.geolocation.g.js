﻿// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

console.groupCollapsed(
    '%cblazorators%c geolocation %cJavaScript loaded',
    'background: purple; color: white; padding: 1px 3px; border-radius: 3px;',
    'color: cyan;', 'color: initial;');

const onSuccess = (dotnetObj, successMethodName, position) => {
    // HACK: Blazor isn't correctly deserializing these.
    // It ignores the `JsonPropertyNameAttribute` :(
    const result = {
        Timestamp: position.timestamp,
        Coords: {
            Accuracy: position.coords.accuracy,
            Altitude: position.coords.altitude,
            AltitudeAccuracy: position.coords.altitudeAccuracy,
            Heading: position.coords.heading,
            Latitude: position.coords.latitude,
            Longitude: position.coords.longitude,
            Speed: position.coords.speed
        }
    };
    dotnetObj.invokeMethod(successMethodName, result);
};

console.log('%O %cfunction %cdefined ✅.', onSuccess, 'color: magenta;', 'color: initial;');

const onError = (dotnetObj, errorMethodName, error) => {
    const result = {
        Code: error.code,
        Message: error.message,
        PERMISSION_DENIED: error.PERMISSION_DENIED,
        POSITION_UNAVAILABLE: error.POSITION_UNAVAILABLE,
        TIMEOUT: error.TIMEOUT
    };
    dotnetObj.invokeMethod(errorMethodName, result);
};

console.log('%O %cfunction %cdefined ✅.', onError, 'color: magenta;', 'color: initial;');

const getCurrentPosition = (
    dotnetObj,
    successMethodName,
    errorMethodName,
    options) => {
    navigator.geolocation.getCurrentPosition(
        position => onSuccess(dotnetObj, successMethodName, position),
        error => onError(dotnetObj, errorMethodName, error),
        options);
}

console.log('%O %cfunction %cdefined ✅.', getCurrentPosition, 'color: magenta;', 'color: initial;');

const watchPosition = (
    dotnetObj,
    successMethodName,
    errorMethodName,
    options) => {
    return navigator.geolocation.watchPosition(
        position => onSuccess(dotnetObj, successMethodName, position),
        error => onError(dotnetObj, errorMethodName, error),
        options);
}

console.log('%O %cfunction %cdefined ✅.', watchPosition, 'color: magenta;', 'color: initial;');

window.blazorators = Object.assign({}, window.blazorators, {
    geolocation: {
        getCurrentPosition,
        watchPosition
    }
});

console.groupEnd();