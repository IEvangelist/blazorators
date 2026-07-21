using System.Diagnostics;
using System.Net;
using Microsoft.Playwright;

namespace Blazor.ExampleConsumer.EndToEndTests;

public sealed class BlazoratorsSiteFixture()
    : BlazorSiteFixture(
        "BLAZORATORS_E2E_BASE_URL",
        "http://127.0.0.1:5127",
        "Blazor.ExampleConsumer");

public sealed class BlazorServerSiteFixture()
    : BlazorSiteFixture(
        "BLAZORATORS_SERVER_E2E_BASE_URL",
        "http://127.0.0.1:5128",
        "BlazorServer.ExampleConsumer");

public abstract class BlazorSiteFixture(
    string baseUrlEnvironmentVariable,
    string defaultLocalUrl,
    string projectName) : IAsyncLifetime
{
    Process? _server;

    public string BaseUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var configuredUrl = Environment.GetEnvironmentVariable(baseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            BaseUrl = NormalizeBaseUrl(configuredUrl);
            await WaitForSiteAsync(BaseUrl, null, projectName);
            return;
        }

        BaseUrl = defaultLocalUrl;

        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "samples", projectName, $"{projectName}.csproj");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        if (configuration is not ("debug" or "release"))
        {
            throw new InvalidOperationException(
                $"Unable to infer the build configuration from {AppContext.BaseDirectory}.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --no-restore --configuration {configuration} --project \"{projectPath}\" --framework net10.0 --no-launch-profile --urls {BaseUrl}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        _server = Process.Start(startInfo);

        if (_server is null)
        {
            throw new InvalidOperationException($"Unable to start {projectName} for end-to-end tests.");
        }

        await WaitForSiteAsync(BaseUrl, _server, projectName);
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

    internal static string FindRepositoryRoot()
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

    static async Task WaitForSiteAsync(string baseUrl, Process? server, string projectName)
    {
        using var client = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        Exception? lastException = null;

        while (!timeout.IsCancellationRequested)
        {
            if (server is { HasExited: true })
            {
                var output = await server.StandardOutput.ReadToEndAsync();
                var error = await server.StandardError.ReadToEndAsync();
                throw new InvalidOperationException(
                    $"{projectName} exited before becoming available.{Environment.NewLine}{output}{error}");
            }

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
