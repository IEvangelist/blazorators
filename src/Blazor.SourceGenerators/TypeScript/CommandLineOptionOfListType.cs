// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class CommandLineOptionOfListType : CommandLineOptionBase
{
    internal CommandLineOptionBase Element { get; set; } = default!;
}