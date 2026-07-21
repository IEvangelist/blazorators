#nullable enable

using Blazor.DOM.AdvancedTypes;
using Blazor.DOM.Namespaces.WebAssembly;
using Blazor.DOM.StandardTypes;

namespace Blazor.DOM.ResolvedFailures.CompilationTests;

public static class ResolvedFailuresConsumer
{
    public static void ConsumeStandardAndHeritage(
        OnErrorEventHandlerNonNull onError,
        ITypeScriptError error,
        ISubtleCrypto subtleCrypto,
        ICryptoKey key,
        IByteLengthQueuingStrategy byteStrategy,
        ICountQueuingStrategy countStrategy,
        IDOMException domException,
        ICompileError compileError,
        ILinkError linkError,
        IRuntimeError runtimeError,
        IValueTypeMap valueTypes)
    {
        _ = onError(
            OnErrorEventHandlerNonNullUnionShape_993b9fe32c.FromString("failure"),
            error: error);

        _ = subtleCrypto.ExportKeyAsync(SubtleCryptoStringShape_5694ca1bfd.Jwk, key);
        _ = subtleCrypto.ExportKeyAsync(SubtleCryptoStringShape_2ae44cd65a.Raw, key);

        IQueuingStrategyContract<byte[]> byteContract = byteStrategy;
        IQueuingStrategyContract<object> countContract = countStrategy;
        ITypeScriptError[] errors =
            [domException, compileError, linkError, runtimeError];

        _ = byteContract;
        _ = countContract;
        _ = errors.Length;
        _ = valueTypes.V128;
    }

    public static void ConsumeCollisionOverloads(
        IDocument document,
        IElement element,
        IHTMLCanvasElement canvas,
        IOffscreenCanvas offscreenCanvas,
        IWebGLRenderingContextBase webGl)
    {
        _ = document.CreateElementNS(
            DocumentStringShape_3ba364cf6c.HttpWwwW3Org1999Xhtml,
            "main");
        _ = element.GetElementsByTagNameNS(
            ElementStringShape_ff809a7942.HttpWwwW3Org1999Xhtml,
            "main");
        _ = canvas.GetContext(HTMLCanvasElementStringShape_54c6689cf0._2D);
        _ = offscreenCanvas.GetContext(OffscreenCanvasStringShape_d9f5dc2874._2D);
        _ = webGl.GetExtension(
            WebGLRenderingContextBaseStringShape_9e416f179d.ANGLEInstancedArrays);
    }
}
