#nullable enable

namespace Blazor.DOM.NonEventDeferrals.CompilationTests;

public static class NonEventDeferralsConsumer
{
    public static void Consume(
        ICrypto crypto,
        ICSSNumericArray numericValues,
        IPromiseRejectionEvent promiseRejectionEvent)
    {
        _ = crypto.RandomUUID();
        _ = numericValues.Entries();
        _ = promiseRejectionEvent.Promise;
    }
}
