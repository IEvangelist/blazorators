using Blazor.ExampleConsumer.Serialization;
using MudBlazor;

namespace Blazor.ExampleConsumer.Services;

public sealed class LayoutService(ILocalStorageService localStorageService)
{
    private bool _systemDarkMode;
    private Preferences? _userPreferences;
    private const string PreferencesKey = "preferences";

    /// <summary>
    /// The user's preferred dark/light mode setting.
    /// This preference is used to determine the actual <see cref="IsDarkMode"/> state.
    /// </summary>
    public DarkLightMode CurrentDarkLightMode { get; private set; }

    public bool IsDarkMode { get; private set; }

    /// <summary>
    /// Observes system theme changes to update dark/light mode.
    /// </summary>
    public bool ObserveSystemThemeChange { get; private set; }

    /// <summary>
    /// The currently active MudBlazor theme.
    /// </summary>
    public MudTheme CurrentTheme { get; private set; } = new();

    /// <summary>
    /// Occurs when a change happens that requires a UI refresh.
    /// </summary>
    public event EventHandler? MajorUpdateOccurred;

    private void OnMajorUpdateOccurred() => MajorUpdateOccurred?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Updates the dark mode state based on user preference and, optionally, the system's dark mode setting.
    /// </summary>
    /// <param name="systemMode">The current system dark mode setting. If <c>null</c>, the existing known system mode is used.</param>
    public void UpdateDarkModeState(bool? systemMode = null)
    {
        if (systemMode.HasValue)
        {
            _systemDarkMode = systemMode.Value;
        }

        IsDarkMode = CurrentDarkLightMode switch
        {
            DarkLightMode.Dark => true,
            DarkLightMode.Light => false,
            _ => _systemDarkMode,
        };
    }

    public void ApplyUserPreferences()
    {
        _userPreferences = localStorageService.GetItem<Preferences>(
            PreferencesKey,
            AppJsonSerializerContext.Default.Preferences);

        if (_userPreferences is null)
        {
            _userPreferences = new()
            {
                DarkLightTheme = DarkLightMode.System,
            };

            localStorageService.SetItem(
                PreferencesKey,
                _userPreferences,
                AppJsonSerializerContext.Default.Preferences);
        }
        else
        {
            CurrentDarkLightMode = _userPreferences.DarkLightTheme;

            UpdateDarkModeState();
        }
    }

    /// <summary>
    /// Handles changes in the system's dark mode setting.
    /// </summary>
    /// <param name="isSystemDarkMode"><c>true</c> if the system is in dark mode, otherwise <c>false</c>.</param>
    public Task OnSystemModeChangedAsync(bool isSystemDarkMode)
    {
        _systemDarkMode = isSystemDarkMode;

        UpdateDarkModeState();
        OnMajorUpdateOccurred();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cycles through the available dark/light mode options (System, Light, Dark) and saves the new preference.
    /// </summary>
    public void CycleDarkLightMode()
    {
        CurrentDarkLightMode = CurrentDarkLightMode switch
        {
            DarkLightMode.System => DarkLightMode.Light,
            DarkLightMode.Light => DarkLightMode.Dark,
            DarkLightMode.Dark => DarkLightMode.System,
            _ => DarkLightMode.System, // Default case, should not happen.
        };

        ObserveSystemThemeChange = CurrentDarkLightMode is DarkLightMode.System;

        UpdateDarkModeState();

        if (_userPreferences is null)
        {
            return;
        }

        _userPreferences.DarkLightTheme = CurrentDarkLightMode;
        
        localStorageService.SetItem(
            PreferencesKey,
            _userPreferences,
            AppJsonSerializerContext.Default.Preferences);

        OnMajorUpdateOccurred();
    }

    public void SetBaseTheme(MudTheme theme)
    {
        CurrentTheme = theme;
        OnMajorUpdateOccurred();
    }
}

public enum DarkLightMode
{
    /// <summary>
    /// The theme is determined by the operating system or browser.
    /// </summary>
    System = 0,

    /// <summary>
    /// Light theme is used.
    /// </summary>
    Light = 1,

    /// <summary>
    /// Dark theme is used.
    /// </summary>
    Dark = 2
}