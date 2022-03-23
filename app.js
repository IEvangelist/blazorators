let _map = null;

const loadMap = (mapId, latitude, longitude) => {
    const element = document.getElementById(mapId);
    if (!!element) {
        const navigationBarMode = Microsoft.Maps.NavigationBarMode;
        const location = new Microsoft.Maps.Location(latitude, longitude);
        _map = new Microsoft.Maps.Map(element, {
            center: location,
            navigationBarMode: navigationBarMode.compact,
            supportedMapTypes: [
                Microsoft.Maps.MapTypeId.road,
                Microsoft.Maps.MapTypeId.aerial,
                Microsoft.Maps.MapTypeId.canvasLight
            ]
        });
        _map.setView({ zoom: 18 });
        Microsoft.Maps.loadModule('Microsoft.Maps.Search', () => {
            var searchManager = new Microsoft.Maps.Search.SearchManager(_map);
            var reverseGeocodeRequestOptions = {
                location: location,
                callback: (answer, userData) => {
                    _map.setView({ bounds: answer.bestView });
                    _map.entities.push(new Microsoft.Maps.Pushpin(reverseGeocodeRequestOptions.location));
                }
            };
            searchManager.reverseGeocode(reverseGeocodeRequestOptions);
        });
    }
};

window.app = {
    loadMap
};