﻿// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace BlazorServer.ExampleConsumer.Pages;

public sealed partial class ClientPosition
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
    bool _isLoading = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender is false)
        {
            return;
        }

        await Geolocation.GetCurrentPositionAsync(
            component: this,
            onSuccessCallbackMethodName: nameof(OnPositionReceived),
            onErrorCallbackMethodName: nameof(OnPositionError),
            options: _options);
    }

    [JSInvokable]
    public void OnPositionReceived(GeolocationPosition position)
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
}
