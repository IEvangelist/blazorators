using Blazor.DOM.AdvancedTypes;
using Microsoft.JSInterop;

namespace Blazor.DOM.TypedUnions.CompilationTests;

public static class TypedUnionUsage
{
    public static ClipboardItemData CreateTextPromise(
        string text,
        IBrowserPromise<ClipboardItemDataStringOrBlobUnion> promise)
    {
        _ = ClipboardItemDataStringOrBlobUnion.FromString(text);
        return new ClipboardItemData(promise);
    }

    public static ClipboardItemData CreateBlobPromise(
        IBlob blob,
        IBrowserPromise<ClipboardItemDataStringOrBlobUnion> promise)
    {
        _ = ClipboardItemDataStringOrBlobUnion.FromBlob(blob);
        return new ClipboardItemData(promise);
    }

    public static string ReadText(ClipboardItemDataStringOrBlobUnion value)
    {
        if (value.TryGetString(out var text))
            return text;
        return value.Kind.ToString();
    }

    public static BlobCallback CreateNullableBlobCallback(
        Action<IBlob?> callback) =>
        blob => callback(blob);

    public static void RoundTripUnionProperty(
        IReadableStreamReadDoneResult<string> result)
    {
        var value = result.Value;
        result.Value = value;
    }

    public static ValueTask<string> CallBlobMethod(IBlob blob) =>
        blob.TextAsync();

    public static BlobPart CreateBinaryOrString(
        BufferSource bytes,
        string text,
        bool binary) =>
        binary ? BlobPart.FromBufferSource(bytes) : BlobPart.FromString(text);
}
