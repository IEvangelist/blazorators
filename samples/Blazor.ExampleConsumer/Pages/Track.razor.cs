// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Pages;

public partial class Track : IDisposable
{
    readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    readonly PositionOptions _options = new()
    {
        EnableHighAccuracy = true,
        MaximumAge = null,
        Timeout = 15_000
    };

    GeolocationPosition? _position;
    GeolocationPositionError? _positionError;
    double _watchId;
    bool _isLoading = true;

    protected override void OnInitialized() =>
        _watchId = Geolocation.WatchPosition(
            component: this,
            onSuccessCallbackMethodName: nameof(OnPositionRecieved),
            onErrorCallbackMethodName: nameof(OnPositionError),
            options: _options);

    [JSInvokable]
    public void OnPositionRecieved(GeolocationPosition position)
    {
        _isLoading = false;
        _position = position;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnPositionError(GeolocationPositionError positionError)
    {
        _isLoading = false;
        _positionError = positionError;
        StateHasChanged();
    }

    public void Dispose() => Geolocation.ClearWatch(_watchId);
}
