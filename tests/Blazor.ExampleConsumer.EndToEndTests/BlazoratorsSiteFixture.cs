using System.Diagnostics;
using System.Net;
using Microsoft.Playwright;

namespace Blazor.ExampleConsumer.EndToEndTests;

public sealed class BlazoratorsSiteFixture : IAsyncLifetime
{
    const string BaseUrlEnvironmentVariable = "BLAZORATORS_E2E_BASE_URL";
    const string DefaultLocalUrl = "http://127.0.0.1:5127";

    Process? _server;

    public string BaseUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var configuredUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            BaseUrl = NormalizeBaseUrl(configuredUrl);
            await WaitForSiteAsync(BaseUrl);
            return;
        }

        BaseUrl = DefaultLocalUrl;

        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "samples", "Blazor.ExampleConsumer", "Blazor.ExampleConsumer.csproj");

        _server = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --no-restore --project \"{projectPath}\" --framework net10.0 --no-launch-profile --urls {BaseUrl}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });

        if (_server is null)
        {
            throw new InvalidOperationException("Unable to start the Blazor example app for end-to-end tests.");
        }

        await WaitForSiteAsync(BaseUrl);
    }

    public async Task DisposeAsync()
    {
        if (_server is { HasExited: false })
        {
            _server.Kill(entireProcessTree: true);
            await _server.WaitForExitAsync();
        }

        _server?.Dispose();
    }

    public string UrlFor(string route)
    {
        var path = route.TrimStart('/');
        return string.IsNullOrEmpty(path)
            ? BaseUrl
            : $"{BaseUrl}/{path}";
    }

    static string NormalizeBaseUrl(string value) =>
        value.Trim().TrimEnd('/');

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "blazorators.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing blazorators.sln.");
    }

    static async Task WaitForSiteAsync(string baseUrl)
    {
        using var client = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        Exception? lastException = null;

        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(baseUrl, timeout.Token);
                if (response.StatusCode is HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;
            }

            await Task.Delay(500, CancellationToken.None);
        }

        throw new TimeoutException($"Timed out waiting for {baseUrl} to become available.", lastException);
    }
}
