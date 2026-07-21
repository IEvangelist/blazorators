// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Compile-time metadata for one strongly typed DOM event.
/// </summary>
/// <typeparam name="TEvent">The generated event payload contract.</typeparam>
public sealed class DomEventDescriptor<TEvent>
{
    private DomEventDescriptor(
        string name,
        string eventMap,
        DomTransportDescriptor transport,
        bool deprecated,
        IReadOnlyList<string> provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventMap);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(provenance);
        if (provenance.Count == 0 || provenance.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-empty source provenance entry is required.",
                nameof(provenance));
        }

        Name = name;
        EventMap = eventMap;
        Transport = transport;
        Deprecated = deprecated;
        Provenance = provenance;
    }

    /// <summary>The exact JavaScript event name.</summary>
    public string Name { get; }

    /// <summary>The authoritative TypeScript EventMap identity.</summary>
    public string EventMap { get; }

    /// <summary>The reviewed payload transport.</summary>
    public DomTransportDescriptor Transport { get; }

    /// <summary>Whether any contributing declaration is deprecated.</summary>
    public bool Deprecated { get; }

    /// <summary>Stable source declarations contributing this event.</summary>
    public IReadOnlyList<string> Provenance { get; }

    /// <summary>Creates a descriptor for a callback-scoped live event reference.</summary>
    public static DomEventDescriptor<TEvent> Reference(
        string name,
        string eventMap,
        string sourceType,
        bool deprecated,
        params string[] provenance) =>
        new(
            name,
            eventMap,
            DomTransportDescriptor.JsReference(sourceType),
            deprecated,
            provenance);

    /// <summary>Creates a descriptor for a proven JSON event payload.</summary>
    public static DomEventDescriptor<TEvent> Value(
        string name,
        string eventMap,
        string sourceType,
        bool nullable,
        bool deprecated,
        params string[] provenance) =>
        new(
            name,
            eventMap,
            DomTransportDescriptor.JsonValue(sourceType, nullable),
            deprecated,
            provenance);
}
