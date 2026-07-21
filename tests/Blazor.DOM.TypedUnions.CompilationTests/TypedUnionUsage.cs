using Blazor.DOM.AdvancedTypes;
using Microsoft.JSInterop;

namespace Blazor.DOM.TypedUnions.CompilationTests;

public static class TypedUnionUsage
{
    public static ClipboardItemData CreateTextPromise(
        string text,
        IBrowserPromise<ClipboardItemDataUnionShape_14acc04c76> promise)
    {
        _ = ClipboardItemDataUnionShape_14acc04c76.FromString(text);
        return new ClipboardItemData(promise);
    }

    public static ClipboardItemData CreateBlobPromise(
        IBlob blob,
        IBrowserPromise<ClipboardItemDataUnionShape_14acc04c76> promise)
    {
        _ = ClipboardItemDataUnionShape_14acc04c76.FromBlob(blob);
        return new ClipboardItemData(promise);
    }

    public static string ReadText(ClipboardItemDataUnionShape_14acc04c76 value)
    {
        if (value.TryGetString(out var text))
            return text;
        return value.Kind.ToString();
    }

    public static BlobCallback CreateNullableBlobCallback(
        Action<IBlob?> callback) =>
        blob => callback(blob);

    public static void SetUnionProperty(
        IReadableStreamReadDoneResult<string> result,
        string value)
    {
        result.Value =
            ReadableStreamReadDoneResultUnionShape_f2d5ea4ede<string>.FromT(value);
    }

    public static ValueTask<string> CallBlobMethod(IBlob blob) =>
        blob.TextAsync();

    public static BlobPart CreateBinaryOrString(
        BufferSource bytes,
        string text,
        bool binary) =>
        binary ? BlobPart.FromBufferSource(bytes) : BlobPart.FromString(text);
}
