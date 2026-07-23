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
            OnErrorEventHandlerNonNullCallEventEventOrStringUnion.FromString("failure"),
            error: error);

        _ = subtleCrypto.ExportKeyAsync(SubtleCryptoExportKeyFormatJwkString.Jwk, key);
        _ = subtleCrypto.ExportKeyAsync(
            SubtleCryptoExportKeyFormatExcludePkcs8OrRawOrSpkiString.Raw,
            key);

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
            DocumentCreateElementNSNamespaceURIXhtmlString.HttpWwwW3Org1999Xhtml,
            "main");
        _ = element.GetElementsByTagNameNS(
            ElementGetElementsByTagNameNSNamespaceURIXhtmlString.HttpWwwW3Org1999Xhtml,
            "main");
        _ = canvas.GetContext(HTMLCanvasElementGetContextContextIdTwoDString._2D);
        _ = offscreenCanvas.GetContext(
            OffscreenCanvasGetContextContextIdTwoDString._2D);
        _ = webGl.GetExtension(
            WebGLRenderingContextBaseGetExtensionExtensionNameANGLEInstancedArraysString
                .ANGLEInstancedArrays);
    }
}
