// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal readonly record struct Indentation(int Level)
{
    private readonly int _spaces = 4;

    internal Indentation Reset() => ResetTo(0);
    internal Indentation ResetTo(int level) => this with { Level = level };
    internal Indentation Increase(int extra = 0) => this with { Level = Level + 1 + extra };
    internal Indentation Decrease(int extra = 0) => this with { Level = Level - 1 - extra };

    public override string ToString() =>
        new(' ', _spaces * Level);
}
