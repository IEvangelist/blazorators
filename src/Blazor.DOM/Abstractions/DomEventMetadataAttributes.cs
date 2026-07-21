// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Associates a generated target contract with authoritative EventMaps.</summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = true)]
public sealed class DomEventTargetAttribute : Attribute
{
    /// <summary>Creates target metadata without relying on CLR naming conventions.</summary>
    public DomEventTargetAttribute(string targetType, params string[] eventMaps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentNullException.ThrowIfNull(eventMaps);
        if (eventMaps.Length == 0 || eventMaps.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one authoritative EventMap identity is required.",
                nameof(eventMaps));
        }

        TargetType = targetType;
        EventMaps = eventMaps;
    }

    /// <summary>The exact TypeScript target identity.</summary>
    public string TargetType { get; }

    /// <summary>Authoritative EventMaps accepted by the target.</summary>
    public IReadOnlyList<string> EventMaps { get; }
}

/// <summary>Identifies the runtime operation represented by a typed event API.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class DomEventSubscriptionAttribute(
    DomEventSubscriptionOperation operation) : Attribute
{
    /// <summary>The listener-registry operation.</summary>
    public DomEventSubscriptionOperation Operation { get; } = operation;

    /// <summary>Whether callers may omit the options argument.</summary>
    public bool SupportsOmittedOptions { get; set; }

    /// <summary>Whether TypeScript's boolean capture form is retained.</summary>
    public bool SupportsBooleanCapture { get; set; }

    /// <summary>Whether an AddEventListenerOptions-shaped value is retained.</summary>
    public bool SupportsObjectOptions { get; set; }
}

/// <summary>Runtime operation for a generated event contract.</summary>
public enum DomEventSubscriptionOperation
{
    /// <summary>Adds a listener and returns its owned registration handle.</summary>
    Subscribe,
}
