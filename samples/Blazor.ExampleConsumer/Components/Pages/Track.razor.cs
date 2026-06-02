// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Components.Pages;

public sealed partial class Track(IGeolocationService geolocation) : IAsyncDisposable
{
    const int MaxTimelineEntries = 25;
    const string WatchKey = "watch";

    static readonly RefreshIntervalChoice[] IntervalChoices =
    [
        new(WatchKey, "Watch", "Live watchPosition subscription", null),
        new("5s",  "5s",  "Poll every 5 seconds",  TimeSpan.FromSeconds(5)),
        new("15s", "15s", "Poll every 15 seconds", TimeSpan.FromSeconds(15)),
        new("30s", "30s", "Poll every 30 seconds", TimeSpan.FromSeconds(30)),
        new("60s", "60s", "Poll every 60 seconds", TimeSpan.FromSeconds(60)),
    ];

    readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    readonly PositionOptions _watchOptions = new()
    {
        EnableHighAccuracy = true,
        MaximumAge = null,
        Timeout = 15_000
    };
    readonly PositionOptions _pollOptions = new()
    {
        EnableHighAccuracy = true,
        MaximumAge = 0,
        Timeout = 10_000
    };

    readonly List<TimelineEntry> _timeline = [];

    GeolocationPosition? _position;
    GeolocationPositionError? _positionError;
    double? _watchId;
    bool _isLoading = true;
    bool _disposed;
    bool _isFixInFlight;

    string _selectedIntervalKey = WatchKey;
    CancellationTokenSource? _pollCts;
    Task? _pollTask;

    bool IsWatching => _selectedIntervalKey == WatchKey;

    string ModeHint => IsWatching
        ? "Subscribed to navigator.geolocation.watchPosition. The browser only re-fires when it detects movement."
        : $"Polling navigator.geolocation.getCurrentPosition every {_selectedIntervalKey}. Useful when the device is stationary.";

    protected override void OnInitialized() => StartWatch();

    void StartWatch()
    {
        if (_disposed || _watchId is not null)
        {
            return;
        }

        _watchId = geolocation.WatchPosition(
            component: this,
            onSuccessCallbackMethodName: nameof(OnPositionReceived),
            onErrorCallbackMethodName: nameof(OnPositionError),
            options: _watchOptions);
    }

    void StopWatch()
    {
        if (_watchId is double id)
        {
            geolocation.ClearWatch(id);
            _watchId = null;
        }
    }

    void RequestOneShotFix()
    {
        if (_disposed || _isFixInFlight)
        {
            return;
        }

        _isFixInFlight = true;
        geolocation.GetCurrentPosition(
            component: this,
            onSuccessCallbackMethodName: nameof(OnPositionReceived),
            onErrorCallbackMethodName: nameof(OnPositionError),
            options: _pollOptions);
    }

    void StartPolling(TimeSpan period)
    {
        StopPolling();

        if (_disposed)
        {
            return;
        }

        // Trigger an immediate fix so the cadence feels responsive.
        RequestOneShotFix();

        var cts = new CancellationTokenSource();
        _pollCts = cts;
        var token = cts.Token;

        _pollTask = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(period);
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    if (_disposed || token.IsCancellationRequested)
                    {
                        break;
                    }

                    await InvokeAsync(RequestOneShotFix).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    void StopPolling()
    {
        if (_pollCts is { } cts)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            cts.Dispose();
            _pollCts = null;
        }
        _pollTask = null;
    }

    void SelectInterval(string key)
    {
        if (_disposed || _selectedIntervalKey == key)
        {
            return;
        }

        var choice = Array.Find(IntervalChoices, c => c.Key == key);
        if (choice is null)
        {
            return;
        }

        _selectedIntervalKey = key;

        if (choice.Period is TimeSpan period)
        {
            StopWatch();
            StartPolling(period);
        }
        else
        {
            StopPolling();
            StartWatch();
        }
    }

    Task RefreshNowAsync()
    {
        RequestOneShotFix();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public void OnPositionReceived(GeolocationPosition position)
    {
        _isFixInFlight = false;

        if (_disposed)
        {
            return;
        }

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
        _isFixInFlight = false;

        if (_disposed)
        {
            return;
        }

        _isLoading = false;
        _positionError = positionError;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var pollTask = _pollTask;
        StopPolling();
        StopWatch();

        if (pollTask is not null)
        {
            try
            {
                await pollTask.ConfigureAwait(false);
            }
            catch
            {
                // Swallow any teardown faults; cancellation is expected.
            }
        }
    }

    sealed record TimelineEntry(
        Guid Id,
        DateTime LocalTime,
        double Latitude,
        double Longitude,
        double Accuracy,
        double? Speed,
        double? Heading);

    sealed record RefreshIntervalChoice(string Key, string Label, string Description, TimeSpan? Period);
}
