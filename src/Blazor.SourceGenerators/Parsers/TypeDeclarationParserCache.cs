// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers;

/// <summary>
/// Caches the state of type processing to avoid infinite loops and redundant parsing
/// during type declaration parsing. It maintains a list of currently processing types
/// and already processed types.
/// </summary>
internal class TypeDeclarationParserCache
{
    /// <summary>
    /// Gets the set of types that have already been processed.
    /// </summary>
    public ConcurrentDictionary<string, CSharpObject> ProcessedTypes { get; } = [];

    /// <summary>
    /// Gets the set of types that are currently being processed.
    /// </summary>
    public HashSet<string> ProcessingTypes { get; } = [];

    /// <summary>
    /// Determines if the specified type has already been processed.
    /// </summary>
    /// <param name="typeName">The name of the type to check.</param>
    /// <returns>True if the type has been processed; otherwise, false.</returns>
    public bool IsProcessed(string typeName) => ProcessedTypes.ContainsKey(typeName);

    /// <summary>
    /// Determines if the specified type is currently being processed.
    /// </summary>
    /// <param name="typeName">The name of the type to check.</param>
    /// <returns>True if the type is currently being processed; otherwise, false.</returns>
    public bool IsProcessing(string typeName) => ProcessingTypes.Contains(typeName);

    /// <summary>
    /// Marks the specified type as processed by adding it to the set of processed types.
    /// </summary>
    /// <param name="typeName">The name of the type to mark as processed.</param>
    public void MarkProcessed(string typeName, CSharpObject obj) => ProcessedTypes.TryAdd(typeName, obj);

    /// <summary>
    /// Marks a type as being processed and returns an <see cref="IDisposable"/> that will
    /// automatically remove the type from the processing set when disposed.
    /// </summary>
    /// <param name="typeName">The name of the type to mark as being processed.</param>
    /// <returns>An <see cref="IDisposable"/> that will remove the type from the processing set when disposed.</returns>
    public IDisposable Process(string typeName) => new ProcessingType(this, typeName);

    /// <summary>
    /// Resets the cache by clearing both the processed and processing type sets.
    /// </summary>
    public void Reset()
    {
        ProcessedTypes.Clear();
        ProcessingTypes.Clear();
    }

    /// <summary>
    /// Represents a type that is currently being processed. When disposed, it removes
    /// the type from the processing set.
    /// </summary>
    private sealed class ProcessingType : IDisposable
    {
        private readonly TypeDeclarationParserCache _cache;
        private readonly string _typeName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessingType"/> class,
        /// marking the specified type as being processed.
        /// </summary>
        /// <param name="cache">The <see cref="TypeDeclarationParserCache"/> instance.</param>
        /// <param name="typeName">The name of the type being processed.</param>
        public ProcessingType(TypeDeclarationParserCache cache, string typeName)
        {
            _cache = cache;
            _cache.ProcessingTypes.Add(typeName);
            _typeName = typeName;
        }

        /// <summary>
        /// Disposes the instance, removing the type from the processing set.
        /// </summary>
        public void Dispose()
        {
            _cache.ProcessingTypes.Remove(_typeName);
        }
    }
}