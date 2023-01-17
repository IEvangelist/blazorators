// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class Signature
{
    public SignatureDeclaration Declaration { get; set; }
    public TypeParameter[] TypeParameters { get; set; }
    public Symbol[] Parameters { get; set; }
    public Symbol ThisParameter { get; set; }
    public TypeScriptType ResolvedReturnType { get; set; }
    public int MinArgumentCount { get; set; }
    public bool HasRestParameter { get; set; }
    public bool HasLiteralTypes { get; set; }
    public Signature Target { get; set; }
    public TypeMapper Mapper { get; set; }
    public Signature[] UnionSignatures { get; set; }
    public Signature ErasedSignatureCache { get; set; }
    public ObjectType IsolatedSignatureType { get; set; }
    public ITypePredicate TypePredicate { get; set; }
    public Map<Signature> Instantiations { get; set; }
}