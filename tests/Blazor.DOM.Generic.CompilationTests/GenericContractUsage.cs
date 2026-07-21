using Blazor.DOM;

namespace Blazor.DOM.Generic.CompilationTests;

internal static class GenericContractUsage
{
    internal static string Verify(
        LockGrantedCallback<string> callback,
        ILock? @lock)
    {
        return callback(@lock);
    }

    internal static T Forward<T>(
        LockGrantedCallback<T> callback,
        ILock? @lock)
        => callback(@lock);
}
