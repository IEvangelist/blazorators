// TODO: source-generate this.

const getCurrentPosition = (
    dotnetObj,
    successMethodName,
    errorMethodName,
    options) => {
    navigator.geolocation.getCurrentPosition(
        position => {
            const result = {
                Timestamp: new Date(position.timestamp).toISOString(),
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
            var json = JSON.stringify(result);
            dotnetObj.invokeMethodAsync(successMethodName, result);
        },
        error => dotnetObj.invokeMethodAsync(errorMethodName, error),
        options);
}

const watchPosition = (
    dotnetObj,
    successMethodName,
    errorMethodName,
    options) => {
    return navigator.geolocation.watchPosition(
        position => dotnetObj.invokeMethodAsync(successMethodName, position),
        error => dotnetObj.invokeMethodAsync(errorMethodName, error),
        options);
}

window.blazorators = {
    getCurrentPosition,
    watchPosition
};