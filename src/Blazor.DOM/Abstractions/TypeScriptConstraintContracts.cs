// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>A generated value that is one of the property keys of <typeparamref name="TMap"/>.</summary>
public interface ITypeScriptKeyOf<TMap>;

/// <summary>A validated value in TypeScript's structural string domain.</summary>
public interface ITypeScriptStringValue
{
    string Value { get; }
}

/// <summary>
/// Preserves the dependency between a finite TypeScript property map and its key.
/// </summary>
public interface ITypeScriptIndexedAccess<TMap, TKey>;

/// <summary>
/// A live intersection reference. Both views are over the same JavaScript identity.
/// </summary>
public interface IBrowserIntersection<TLeft, TRight> : IDomProxy
{
    TLeft AsLeft();
    TRight AsRight();
}
