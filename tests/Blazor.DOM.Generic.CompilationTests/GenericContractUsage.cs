using Blazor.DOM;

namespace Blazor.DOM.Generic.CompilationTests;

internal static class GenericContractUsage
{
    internal static async ValueTask<(string Name, double Held)>
        VerifyAsync(
        ILockManager manager,
        LockOptions options)
    {
        var name = await manager.RequestAsync(
            "fixture",
            static @lock => @lock?.Name ?? "");
        var held = await manager.RequestAsync(
            "fixture",
            options,
            static @lock => @lock?.Mode == LockMode.Exclusive
                ? 1d
                : 0d);
        LockGrantedCallback<string> callback =
            static @lock => @lock?.Name ?? "";
        _ = callback;
        return (name, held);
    }

    internal static ValueTask<T> ForwardAsync<T>(
        ILockManager manager,
        string name,
        LockGrantedCallback<T> callback)
        => manager.RequestAsync(
            name,
            callback);
}
