namespace Blazor.ExampleConsumer.Components.Pages;

internal static class DomCapabilityCatalog
{
    public static IReadOnlyList<DomCapabilityDefinition> All { get; } =
    [
        new()
        {
            Slug = "permissions",
            Name = "Permissions",
            Category = "Essentials",
            Icon = "check",
            Summary = "Query browser permission state and observe typed change events.",
            UseCase = "Decide whether to show, defer, or explain a permission-gated feature before starting it.",
            TrySteps =
            [
                "Choose a permission your app depends on.",
                "Query the browser without triggering a permission prompt.",
                "Use the typed state to shape the next user-visible step."
            ],
            Success = "The browser returns granted, prompt, or denied as a typed PermissionState value.",
            Package = "Blazor.Permissions.WebAssembly",
            Service = "IPermissionsCapability",
            Registration = "AddPermissionsCapability",
            Roots = ["navigator.permissions"],
            Features = ["permissions"],
            Highlights = ["Typed permission descriptors", "Live PermissionStatus proxies", "Owned state-change subscriptions"],
            Symbols = 13,
            Members = 22,
            Operations = 23,
            Sample = """
                builder.Services.AddPermissionsCapability();

                await using var permissions = Capability.GetPermissions();
                await using var status = await permissions.QueryAsync(
                    new PermissionDescriptor { Name = PermissionName.Geolocation });

                var state = status.State;
                """
        },
        new()
        {
            Slug = "clipboard",
            Name = "Clipboard",
            Category = "Essentials",
            Icon = "copy",
            Summary = "Read and write plain text through a secure, typed clipboard root.",
            UseCase = "Copy application text, then prove the system clipboard contains the exact value a user expects.",
            TrySteps =
            [
                "Enter a recognizable value and write it from the button click.",
                "Paste into the verification box using the browser or operating-system shortcut.",
                "Compare the pasted value with the original without requesting clipboard-read permission."
            ],
            Success = "The pasted value exactly matches the source text and the page reports a verified round trip.",
            Package = "Blazor.Clipboard.WebAssembly",
            Service = "IClipboardCapability",
            Registration = "AddClipboardCapability",
            Roots = ["navigator.clipboard"],
            Features = ["clipboard-read", "clipboard-write"],
            Highlights = ["Promise-based text transport", "Explicit navigator root", "Browser permission errors stay visible"],
            Symbols = 2,
            Members = 6,
            Operations = 7,
            SecureContext = true,
            Sample = """
                builder.Services.AddClipboardCapability();

                await using var clipboard = Capability.GetClipboard();
                await clipboard.WriteTextAsync("Generated DOM interop");
                var text = await clipboard.ReadTextAsync();
                """
        },
        new()
        {
            Slug = "web-share",
            Name = "Web Share",
            Category = "Essentials",
            Icon = "navigation",
            Summary = "Validate and invoke the platform share sheet with typed data.",
            UseCase = "Prepare a share payload, check browser support, then open the native share sheet only when it is valid.",
            TrySteps =
            [
                "Enter the title and text your app wants to share.",
                "Validate the typed ShareData dictionary with navigator.canShare.",
                "On a supported device, open the browser-owned share sheet from a user gesture."
            ],
            Success = "The payload is either accepted for sharing or rejected with an explicit browser capability result.",
            Package = "Blazor.Share.WebAssembly",
            Service = "IShareCapability",
            Registration = "AddShareCapability",
            Roots = ["navigator"],
            Features = ["web-share"],
            Highlights = ["Typed ShareData dictionary", "Safe canShare capability check", "User-activated Promise invocation"],
            Symbols = 14,
            Members = 7,
            Operations = 5,
            SecureContext = true,
            UserActivation = true,
            Sample = """
                builder.Services.AddShareCapability();

                var data = new ShareData
                {
                    Title = "Blazorators",
                    Url = Navigation.Uri
                };

                await using var navigator = Capability.GetNavigator();
                if (navigator.CanShare(data))
                    await navigator.ShareAsync(data);
                """
        },
        new()
        {
            Slug = "wake-lock",
            Name = "Wake Lock",
            Category = "Essentials",
            Icon = "sun",
            Summary = "Keep the display awake with an owned sentinel and typed release event.",
            UseCase = "Keep a recipe, presentation, or monitoring screen awake only while the user needs it.",
            TrySteps =
            [
                "Acquire the screen wake lock while the document is visible.",
                "Inspect the owned sentinel returned by the browser.",
                "Release and dispose the same sentinel when the workflow ends."
            ],
            Success = "The sentinel changes from active to released and its live proxy is disposed.",
            Package = "Blazor.WakeLock.WebAssembly",
            Service = "IWakeLockCapability",
            Registration = "AddWakeLockCapability",
            Roots = ["navigator.wakeLock"],
            Features = ["screen-wake-lock"],
            Highlights = ["Owned WakeLockSentinel proxy", "Idempotent release", "Typed release subscription"],
            Symbols = 12,
            Members = 23,
            Operations = 24,
            SecureContext = true,
            Sample = """
                builder.Services.AddWakeLockCapability();

                await using var wakeLock = Capability.GetWakeLock();
                await using var sentinel =
                    await wakeLock.RequestAsync(WakeLockType.Screen);

                await sentinel.ReleaseAsync();
                """
        },
        new()
        {
            Slug = "storage-management",
            Name = "Storage management",
            Category = "Essentials",
            Icon = "database",
            Summary = "Measure origin storage, inspect persistence, and reach the private file system.",
            UseCase = "Show storage pressure before saving offline data and request persistence for important local work.",
            TrySteps =
            [
                "Read current usage, quota, and persistence state.",
                "Compare usage visually against the browser-provided quota.",
                "Optionally ask the browser to make this origin's storage persistent."
            ],
            Success = "Usage and quota are returned as numbers and persistence is reported as a boolean decision.",
            Package = "Blazor.StorageManagement.WebAssembly",
            Service = "IStorageManagementCapability",
            Registration = "AddStorageManagementCapability",
            Roots = ["navigator.storage"],
            Features = ["storage", "origin-private-file-system"],
            Highlights = ["Typed usage and quota estimate", "Persistence state and request", "Owned OPFS directory handle"],
            Symbols = 4,
            Members = 10,
            Operations = 11,
            SecureContext = true,
            Sample = """
                builder.Services.AddStorageManagementCapability();

                await using var storage = Capability.GetStorageManager();
                var estimate = await storage.EstimateAsync();
                var persisted = await storage.PersistedAsync();
                """
        },
        new()
        {
            Slug = "screen",
            Name = "Screen",
            Category = "Essentials",
            Icon = "monitor",
            Summary = "Inspect display geometry and subscribe to orientation changes.",
            UseCase = "Adapt a full-screen or visual workspace to the current display and layout viewport.",
            TrySteps =
            [
                "Read physical screen geometry and color depth.",
                "Compare it with the current browser viewport.",
                "Resize or rotate the device and refresh to observe the new values."
            ],
            Success = "The refreshed geometry matches the browser's current screen and viewport dimensions.",
            Package = "Blazor.Screen.WebAssembly",
            Service = "IScreenCapability",
            Registration = "AddScreenCapability",
            Roots = ["window.screen"],
            Features = ["screen", "screen-orientation"],
            Highlights = ["Synchronous WASM geometry", "Exact orientation enum", "Typed change-event ownership"],
            Symbols = 12,
            Members = 29,
            Operations = 30,
            Sample = """
                builder.Services.AddScreenCapability();

                await using var screen = Capability.GetScreen();
                await using var orientation = screen.Orientation;
                var viewport = $"{screen.Width} x {screen.Height}";
                var orientationType = orientation.Type;
                """
        },
        new()
        {
            Slug = "performance",
            Name = "Performance",
            Category = "Platform",
            Icon = "refresh",
            Summary = "Measure marks, navigation, resources, and observer-delivered timeline entries.",
            UseCase = "Measure an application operation with the browser's high-resolution monotonic clock.",
            TrySteps =
            [
                "Choose a target delay for the sample workload.",
                "Capture Performance.now before and after the asynchronous work.",
                "Compare requested and measured durations, including the scheduling delta."
            ],
            Success = "The measured duration is close to or greater than the requested delay and is returned in milliseconds.",
            Package = "Blazor.Performance.WebAssembly",
            Service = "IPerformanceCapability",
            Registration = "AddPerformanceCapability",
            Roots = ["performance", "PerformanceObserver"],
            Features = ["performance-timeline", "user-timing", "performance-observer", "navigation-timing", "resource-timing"],
            Highlights = ["High-resolution synchronous clock", "Owned browser-array results", "Persistent observer callbacks"],
            Symbols = 27,
            Members = 134,
            Operations = 126,
            Sample = """
                builder.Services.AddPerformanceCapability();

                await using var performance = Capability.GetPerformance();
                var start = performance.Now();
                await using var mark = performance.Mark("render-ready");
                var elapsed = performance.Now() - start;
                """
        },
        new()
        {
            Slug = "web-crypto",
            Name = "Web Crypto",
            Category = "Platform",
            Icon = "check",
            Summary = "Generate secure randomness and run typed SubtleCrypto operations.",
            UseCase = "Create browser-backed secure identifiers without JavaScript wrappers or weak random-number fallbacks.",
            TrySteps =
            [
                "Generate a UUID through crypto.randomUUID.",
                "Inspect the RFC 4122 version and variant bits.",
                "Generate additional values to confirm each identifier is unique."
            ],
            Success = "Each generated value is a unique, valid version 4 UUID.",
            Package = "Blazor.WebCrypto.WebAssembly",
            Service = "IWebCryptoCapability",
            Registration = "AddWebCryptoCapability",
            Roots = ["crypto", "crypto.subtle"],
            Features = ["web-crypto"],
            Highlights = ["Cryptographic random UUIDs", "Binary BufferSource transport", "Live CryptoKey proxy ownership"],
            Symbols = 30,
            Members = 75,
            Operations = 29,
            SecureContext = true,
            Sample = """
                builder.Services.AddWebCryptoCapability();

                await using var crypto = Capability.GetCrypto();
                var id = crypto.RandomUUID();

                await using var subtle = crypto.Subtle;
                var digest = await subtle.DigestAsync(
                    AlgorithmIdentifier.FromString("SHA-256"), bytes);
                """
        },
        new()
        {
            Slug = "credentials",
            Name = "Credentials & WebAuthn",
            Category = "Platform",
            Icon = "compass",
            Summary = "Preserve credential, authenticator, and binary WebAuthn response identity.",
            UseCase = "End silent credential mediation before beginning an explicit sign-in or passkey ceremony.",
            TrySteps =
            [
                "Invoke preventSilentAccess for the current secure origin.",
                "Observe the completed Promise without flattening credential objects.",
                "Follow with an explicit user-activated credential request in your application."
            ],
            Success = "The browser confirms that future credential access for the origin must be explicit.",
            Package = "Blazor.Credentials.WebAssembly",
            Service = "ICredentialsCapability",
            Registration = "AddCredentialsCapability",
            Roots = ["navigator.credentials"],
            Features = ["credential-management", "webauthn"],
            Highlights = ["Typed creation and request options", "Live credential response proxies", "Binary authenticator payload transport"],
            Symbols = 38,
            Members = 101,
            Operations = 43,
            SecureContext = true,
            UserActivation = true,
            Sample = """
                builder.Services.AddCredentialsCapability();

                await using var credentials =
                    Capability.GetCredentialsContainer();

                await using var credential =
                    await credentials.GetAsync(requestOptions);
                """
        },
        new()
        {
            Slug = "offline-storage",
            Name = "Offline storage",
            Category = "Platform",
            Icon = "database",
            Summary = "Use Cache Storage and IndexedDB without flattening browser-owned objects.",
            UseCase = "Create a named application cache, verify it exists, and remove it with the same generated surface.",
            TrySteps =
            [
                "Choose a cache name that belongs to your application.",
                "Create or remove that cache through Cache Storage.",
                "Query the browser again to verify the resulting state."
            ],
            Success = "The raw result shows the cache's state before and after the operation.",
            Package = "Blazor.OfflineStorage.WebAssembly",
            Service = "IOfflineStorageCapability",
            Registration = "AddOfflineStorageCapability",
            Roots = ["caches", "indexedDB"],
            Features = ["cache-storage", "indexed-db", "structured-clone"],
            Highlights = ["Owned Request and Response proxies", "Typed IndexedDB request events", "Reviewed structured-clone boundaries"],
            Symbols = 77,
            Members = 247,
            Operations = 252,
            SecureContext = true,
            Sample = """
                builder.Services.AddOfflineStorageCapability();

                await using var caches = Capability.GetCacheStorage();
                var names = await caches.KeysAsync();

                await using var indexedDb = Capability.GetIDBFactory();
                var databases = await indexedDb.DatabasesAsync();
                """
        },
        new()
        {
            Slug = "browser-coordination",
            Name = "Browser coordination",
            Category = "Platform",
            Icon = "route",
            Summary = "Coordinate tabs with BroadcastChannel, Web Locks, and page visibility.",
            UseCase = "Inspect whether other tabs or workers currently hold or await origin-scoped Web Locks.",
            TrySteps =
            [
                "Query the current Web Locks snapshot.",
                "Inspect held and pending lock counts independently.",
                "Use the same typed manager before coordinating work across tabs."
            ],
            Success = "The result contains separate held and pending arrays represented by exact counts.",
            Package = "Blazor.BrowserCoordination.WebAssembly",
            Service = "IBrowserCoordinationCapability",
            Registration = "AddBrowserCoordinationCapability",
            Roots = ["BroadcastChannel", "navigator.locks", "document"],
            Features = ["broadcast-channel", "web-locks", "page-visibility"],
            Highlights = ["Owned channel lifecycle", "Persistent message callbacks", "Typed lock snapshots and visibility events"],
            Symbols = 29,
            Members = 67,
            Operations = 64,
            SecureContext = true,
            Sample = """
                builder.Services.AddBrowserCoordinationCapability();

                await using var locks = Capability.GetLockManager();
                var snapshot = await locks.QueryAsync();

                await using var channelFactory =
                    Capability.GetBroadcastChannel();
                await using var channel =
                    channelFactory.Create("updates");
                """
        },
        new()
        {
            Slug = "media-devices",
            Name = "Media devices",
            Category = "Platform",
            Icon = "mic",
            Summary = "Enumerate devices and retain live stream and track ownership.",
            UseCase = "Populate a camera or microphone picker from devices the browser is allowed to reveal.",
            TrySteps =
            [
                "Enumerate the current media devices from a secure context.",
                "Inspect each typed kind, label, and opaque device identifier.",
                "Use the result to populate device choices before requesting a stream."
            ],
            Success = "The page renders every returned device and preserves the browser's privacy-redacted labels.",
            Package = "Blazor.MediaDevices.WebAssembly",
            Service = "IMediaDevicesCapability",
            Registration = "AddMediaDevicesCapability",
            Roots = ["navigator.mediaDevices"],
            Features = ["media-devices", "display-capture"],
            Permissions = ["camera", "microphone", "display-capture"],
            Highlights = ["Typed constraints and settings", "Owned MediaStream and track proxies", "Device and track event subscriptions"],
            Symbols = 37,
            Members = 148,
            Operations = 76,
            SecureContext = true,
            UserActivation = true,
            Sample = """
                builder.Services.AddMediaDevicesCapability();

                await using var media = Capability.GetMediaDevices();
                var supported = media.GetSupportedConstraints();

                await using var devices =
                    await media.EnumerateDevicesAsync();
                """
        },
        new()
        {
            Slug = "notifications",
            Name = "Notifications",
            Category = "Platform",
            Icon = "info",
            Summary = "Request permission and own typed notification lifecycle events.",
            UseCase = "Request notification permission in context, then show a user-authored notification only after consent.",
            TrySteps =
            [
                "Customize the notification title and body.",
                "Request permission from a user gesture and inspect the typed enum.",
                "If granted, create a live Notification proxy and show the browser-owned visual."
            ],
            Success = "Permission is explicit; granted browsers display a notification with the entered title and body.",
            Package = "Blazor.Notifications.WebAssembly",
            Service = "INotificationsCapability",
            Registration = "AddNotificationsCapability",
            Roots = ["Notification"],
            Features = ["notifications"],
            Permissions = ["notifications"],
            Highlights = ["Static permission state", "Typed NotificationOptions", "Owned click, close, error, and show events"],
            Symbols = 14,
            Members = 41,
            Operations = 30,
            SecureContext = true,
            UserActivation = true,
            Sample = """
                builder.Services.AddNotificationsCapability();

                await using var factory = Capability.GetNotification();
                var permission = await factory.RequestPermissionAsync();

                if (permission == NotificationPermission.Granted)
                {
                    await using var notification = factory.Create(
                        "Build complete",
                        new NotificationOptions
                        {
                            Body = "All checks passed."
                        });
                }
                """
        },
        new()
        {
            Slug = "file-system-access",
            Name = "File System Access",
            Category = "Specialized",
            Icon = "code",
            Summary = "Open browser-managed files and directories while preserving handle identity.",
            UseCase = "Let a user choose a local file, then inspect metadata through its browser-owned handle.",
            TrySteps =
            [
                "Open the browser-owned picker from the button click.",
                "Choose a disposable test file or cancel without side effects.",
                "Inspect the selected file's name, MIME type, size, and modified time."
            ],
            Success = "The selected file remains a live owned proxy and its exact metadata appears in the result.",
            Package = "Blazor.FileSystemAccess.WebAssembly",
            Service = "IFileSystemAccessCapability",
            Registration = "AddFileSystemAccessCapability",
            Roots = ["window.showOpenFilePicker", "window.showSaveFilePicker", "window.showDirectoryPicker"],
            Features = ["file-system-access", "local-file-system"],
            Highlights = ["Owned file and directory handles", "Live async directory iterators", "Binary Blob and File reads"],
            Symbols = 30,
            Members = 56,
            Operations = 52,
            Exclusions = 3,
            SecureContext = true,
            UserActivation = true,
            Sample = """
                builder.Services.AddFileSystemAccessCapability();

                await using var window = Capability.GetWindow();
                await using var handles = await window.ShowOpenFilePickerAsync(
                    new OpenFilePickerOptions { Multiple = true });

                await using var handle = await handles.GetAsync(0);
                await using var file = await handle.GetFileAsync();
                var bytes = await file.BytesAsync();
                """
        }
    ];

    public static DomCapabilityDefinition? Find(string? slug) =>
        All.FirstOrDefault(
            capability => string.Equals(capability.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static int IndexOf(DomCapabilityDefinition capability)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (ReferenceEquals(All[index], capability))
            {
                return index;
            }
        }

        return -1;
    }

    public static IReadOnlyList<string> Categories { get; } =
        All.Select(capability => capability.Category).Distinct(StringComparer.Ordinal).ToArray();
}

public sealed record DomCapabilityDefinition
{
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Icon { get; init; }
    public required string Summary { get; init; }
    public required string UseCase { get; init; }
    public required string[] TrySteps { get; init; }
    public required string Success { get; init; }
    public required string Package { get; init; }
    public required string Service { get; init; }
    public required string Registration { get; init; }
    public required string[] Roots { get; init; }
    public required string[] Features { get; init; }
    public required string[] Highlights { get; init; }
    public required string Sample { get; init; }
    public string[] Permissions { get; init; } = [];
    public int Symbols { get; init; }
    public int Members { get; init; }
    public int Operations { get; init; }
    public int Exclusions { get; init; }
    public bool SecureContext { get; init; }
    public bool UserActivation { get; init; }
    public string Href => $"/dom-e2e/{Slug}";
}
