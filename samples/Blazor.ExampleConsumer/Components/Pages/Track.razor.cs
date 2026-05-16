// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Components.Pages;

public sealed partial class Track(IGeolocationService geolocation) : IDisposable
{
    const int MaxTimelineEntries = 25;

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

    readonly List<TimelineEntry> _timeline = [];

    GeolocationPosition? _position;
    GeolocationPositionError? _positionError;
    double _watchId;
    bool _isLoading = true;

    protected override void OnInitialized() =>
        _watchId = geolocation.WatchPosition(
            component: this,
            onSuccessCallbackMethodName: nameof(OnPositionReceived),
            onErrorCallbackMethodName: nameof(OnPositionError),
            options: _options);

    [JSInvokable]
    public void OnPositionReceived(GeolocationPosition position)
    {
        _isLoading = false;
        _position = position;

        var coords = position.Coords;
        if (coords is not null)
        {
            // Skip duplicates so we only "splat" fresh fixes onto the timeline.
            var newest = _timeline.Count > 0 ? _timeline[0] : null;
            var isDuplicate = newest is not null
                && Math.Abs(newest.Latitude - coords.Latitude) < 0.0000005
                && Math.Abs(newest.Longitude - coords.Longitude) < 0.0000005;

            if (!isDuplicate)
            {
                _timeline.Insert(0, new TimelineEntry(
                    Id: Guid.NewGuid(),
                    LocalTime: position.TimestampAsUtcDateTime.ToLocalTime(),
                    Latitude: coords.Latitude,
                    Longitude: coords.Longitude,
                    Accuracy: coords.Accuracy,
                    Speed: coords.Speed,
                    Heading: coords.Heading));

                if (_timeline.Count > MaxTimelineEntries)
                {
                    _timeline.RemoveRange(MaxTimelineEntries, _timeline.Count - MaxTimelineEntries);
                }
            }
        }

        StateHasChanged();
    }

    [JSInvokable]
    public void OnPositionError(GeolocationPositionError positionError)
    {
        _isLoading = false;
        _positionError = positionError;
        StateHasChanged();
    }

    public void Dispose() => geolocation.ClearWatch(_watchId);

    sealed record TimelineEntry(
        Guid Id,
        DateTime LocalTime,
        double Latitude,
        double Longitude,
        double Accuracy,
        double? Speed,
        double? Heading);
}
