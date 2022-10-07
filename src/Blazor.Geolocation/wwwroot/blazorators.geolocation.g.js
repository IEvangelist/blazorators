// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

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
    dotnetObj.invokeMethodAsync(successMethodName, result);
    dotnetObj.dispose();
};

const onError = (dotnetObj, errorMethodName, error) => {
    const result = {
        Code: error.code,
        Message: error.message,
        PERMISSION_DENIED: error.PERMISSION_DENIED,
        POSITION_UNAVAILABLE: error.POSITION_UNAVAILABLE,
        TIMEOUT: error.TIMEOUT
    };
    dotnetObj.invokeMethodAsync(errorMethodName, result);
    dotnetObj.dispose();
};

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

window.blazorators = Object.assign({}, window.blazorators, {
    geolocation: {
        getCurrentPosition,
        watchPosition
    }
});
