using Blazor.ExampleConsumer.Services;
using MudBlazor;

namespace Blazor.ExampleConsumer.Components.Layout;

public partial class MainLayout(LayoutService layoutService) : LayoutComponentBase, IDisposable
{
    private MudThemeProvider _mudThemeProvider;

    /// <summary>
    /// Gets the text for the dark/light mode toggle button, indicating the next mode.
    /// </summary>
    public string DarkLightModeButtonText => layoutService.CurrentDarkLightMode switch
    {
        DarkLightMode.Dark => "Auto mode",
        DarkLightMode.Light => "Dark mode",
        _ => "Light mode"
    };

    /// <summary>
    /// Gets the icon for the dark/light mode toggle button.
    /// </summary>
    public string DarkLightModeButtonIcon => layoutService.CurrentDarkLightMode switch
    {
        DarkLightMode.Dark => Icons.Material.Rounded.AutoMode,
        DarkLightMode.Light => Icons.Material.Outlined.DarkMode,
        _ => Icons.Material.Filled.LightMode
    };

    protected override void OnInitialized()
    {
        layoutService.MajorUpdateOccurred += OnMajorUpdateOccured;
        base.OnInitialized();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var dark = await _mudThemeProvider.GetSystemDarkModeAsync();

            layoutService.UpdateDarkModeState(dark);

            layoutService.ApplyUserPreferences();

            await _mudThemeProvider.WatchSystemDarkModeAsync(layoutService.OnSystemModeChangedAsync);

            StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    public void Dispose() => layoutService.MajorUpdateOccurred -= OnMajorUpdateOccured;

    private void OnMajorUpdateOccured(object? sender, EventArgs e) => StateHasChanged();
}