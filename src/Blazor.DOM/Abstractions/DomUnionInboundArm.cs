// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.JSInterop;

/// <summary>Describes one proven JavaScript discriminator for an inbound union arm.</summary>
public sealed class DomUnionInboundArm<TResult>
{
    private DomUnionInboundArm(
        string discriminator,
        string? brand,
        string? literal,
        Func<JsonElement, TResult>? jsonFactory,
        Func<IJSObjectReference, TResult>? referenceFactory)
    {
        Discriminator = discriminator;
        Brand = brand;
        Literal = literal;
        JsonFactory = jsonFactory;
        ReferenceFactory = referenceFactory;
    }

    internal string Discriminator { get; }
    internal string? Brand { get; }
    internal string? Literal { get; }
    internal Func<JsonElement, TResult>? JsonFactory { get; }
    internal Func<IJSObjectReference, TResult>? ReferenceFactory { get; }

    /// <summary>Creates a <c>typeof value === "string"</c> arm.</summary>
    public static DomUnionInboundArm<TResult> String(
        Func<string, TResult> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(
            "string",
            null,
            null,
            element => factory(element.GetString()
                ?? throw new DomTransportException(
                    "JavaScript selected a string union arm but supplied null.")),
            null);
    }

    /// <summary>Creates an exact string-literal arm.</summary>
    public static DomUnionInboundArm<TResult> StringLiteral(
        string literal,
        Func<TResult> factory)
    {
        ArgumentNullException.ThrowIfNull(literal);
        ArgumentNullException.ThrowIfNull(factory);
        return new("string-literal", null, literal, _ => factory(), null);
    }

    /// <summary>Creates a branded live-reference arm, such as <c>Blob</c>.</summary>
    public static DomUnionInboundArm<TResult> Reference(
        string brand,
        Func<IJSObjectReference, TResult> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brand);
        ArgumentNullException.ThrowIfNull(factory);
        return new("reference", brand, null, null, factory);
    }

    internal object ToJavaScriptDescriptor() => new
    {
        kind = Discriminator,
        brand = Brand,
        literal = Literal,
    };
}
