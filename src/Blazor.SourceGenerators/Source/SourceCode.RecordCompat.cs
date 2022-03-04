// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.


namespace Blazor.SourceGenerators.Source;
static partial class SourceCode
{
    internal const string RecordCompat = @"using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}";
}
