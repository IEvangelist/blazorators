// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Builders;

/// <summary>
/// Represents the indentation level and spaces for generating code.
/// </summary>
/// <param name="Level">The current indentation level.</param>
/// <param name="Spaces">The number of spaces to use for indentation.</param>
internal readonly record struct Indentation(int Level, int Spaces = 4)
{
    /// <summary>
    /// Resets the indentation level to 0.
    /// </summary>
    /// <returns>A new <see cref="Indentation"/> instance with a level of <c>0</c>.</returns>
    internal Indentation Reset() => ResetTo(0);

    /// <summary>
    /// Resets the indentation level to the specified value.
    /// </summary>
    /// <param name="level">The new indentation level.</param>
    /// <returns>A new <see cref="Indentation"/> instance with the updated level.</returns>
    internal Indentation ResetTo(int level) => this with { Level = level };

    /// <summary>
    /// Increases the indentation level by one, with an optional extra increment.
    /// </summary>
    /// <param name="extra">An optional extra increment to add to the indentation level.</param>
    /// <returns>A new <see cref="Indentation"/> instance with the incremented indentation level.</returns>
    internal Indentation Increase(int extra = 0) => this with { Level = Level + 1 + extra };

    /// <summary>
    /// Decreases the indentation level by the specified amount.
    /// </summary>
    /// <param name="extra">The additional amount to decrease the indentation level by.</param>
    /// <returns>A new <see cref="Indentation"/> instance with the decremented indentation level.</returns>
    internal Indentation Decrease(int extra = 0) => this with { Level = Level - 1 - extra };

    /// <summary>
    /// Returns a <see langword="string"/> representation of the current indentation level.
    /// </summary>
    /// <remarks>
    /// This is used to generate the indentation for the code. For example, if the indentation
    /// level is <c>2</c> and the number of spaces is <c>4</c>, then the result would be <c>"        "</c>.
    /// </remarks>
    public override string ToString() => new(' ', Spaces * Level);
}