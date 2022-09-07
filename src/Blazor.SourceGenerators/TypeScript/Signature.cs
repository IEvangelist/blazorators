// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class Signature
{
    internal SignatureDeclaration Declaration { get; set; }
    internal TypeParameter[] TypeParameters { get; set; }
    internal Symbol[] Parameters { get; set; }
    internal Symbol ThisParameter { get; set; }
    internal TsType ResolvedReturnType { get; set; }
    internal int MinArgumentCount { get; set; }
    internal bool HasRestParameter { get; set; }
    internal bool HasLiteralTypes { get; set; }
    internal Signature Target { get; set; }
    internal TypeMapper Mapper { get; set; }
    internal Signature[] UnionSignatures { get; set; }
    internal Signature ErasedSignatureCache { get; set; }
    internal ObjectType IsolatedSignatureType { get; set; }
    internal ITypePredicate TypePredicate { get; set; }
    internal Map<Signature> Instantiations { get; set; }
}