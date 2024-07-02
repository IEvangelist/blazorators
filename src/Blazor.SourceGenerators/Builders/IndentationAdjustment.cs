// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Builders;

/// <summary>
/// Specifies the type of adjustment to be made to the indentation level.
/// </summary>
internal enum IndentationAdjustment
{
    /// <summary>
    /// No adjustment is made to the indentation level.
    /// </summary>
    Noop,

    /// <summary>
    /// The indentation level is increased.
    /// </summary>
    Increase,
    
    /// <summary>
    /// The indentation level is decreased.
    /// </summary>
    Decrease
};