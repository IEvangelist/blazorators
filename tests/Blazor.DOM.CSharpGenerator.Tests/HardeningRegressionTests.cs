// Hardening regression tests: validates fail-closed behavior, exit codes, ambiguous overrides,
// type semantics correctness, no-object/void fallbacks, and manifest correctness.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Profiles;
using Blazor.DOM.CSharpGenerator.Projection;
using System.Text.Json;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class HardeningRegressionTests
{
    // ── Helper factories ────────────────────────────────────────────────────────

    private static TypeResolver EmptyResolver() => new([]);

    private static TypeResolver WithInterface(string name) =>
        new([new SymbolModel(0, name, 0, [MakeInterfaceDecl(name, [])], false,
            new SemanticModel("matched", name, "definition", null, ["interface"],
                [], [], false, false, [], false, false, false, [], []))]);

    private static TypeResolver WithInterfaces(params string[] names) =>
        new(names.Select((name, ordinal) => new SymbolModel(ordinal, name, 0, [MakeInterfaceDecl(name, [])], false,
            new SemanticModel("matched", name, "definition", null, ["interface"],
                [], [], false, false, [], false, false, false, [], []))).ToList());

    private static DeclarationModel MakeInterfaceDecl(
        string name,
        IReadOnlyList<MemberModel> members,
        int ordinal = 0,
        IReadOnlyList<HeritageClauseModel>? heritage = null)
        => new(ordinal, "interface", name, [], [], heritage ?? [], members,
            null, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)),
            null, false, new EventMapModel(false, []), []);

    private static DeclarationModel MakeTypeAliasDecl(string name, TypeNode type)
        => new(0, "typeAlias", name, [], [], [], [],
            type, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)),
            null, false, new EventMapModel(false, []), []);

    private static SymbolModel MakeSymbol(string name, string classification, IReadOnlyList<DeclarationModel> decls)
        => new(0, name, 0, decls, false, new SemanticModel(
            "matched", name, "definition", null, [classification],
            [], [], false, false, [], false, false, false, [], []));

    private static MemberModel MakeMethodMember(
        string name,
        TypeNode returnType,
        IReadOnlyList<ParameterModel> parameters,
        int ordinal = 0)
        => new(ordinal, "method", new NameNode("identifier", name),
            false, false, false, [], parameters,
            null, returnType,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)));

    private static MemberModel MakePropMember(
        string name,
        TypeNode type,
        bool optional = false,
        bool @readonly = false,
        int ordinal = 0)
        => new(ordinal, "property", new NameNode("identifier", name),
            optional, @readonly, false, [], [],
            type, null,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)));

    private static MemberModel MakeGetterMember(string name, TypeNode returnType, int ordinal = 0)
        => new(ordinal, "getter", new NameNode("identifier", name),
            false, true, false, [], [],
            null, returnType,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)));

    private static MemberModel MakeSetterMember(
        string name,
        IReadOnlyList<ParameterModel> parameters,
        int ordinal = 0)
        => new(ordinal, "setter", new NameNode("identifier", name),
            false, false, false, [], parameters,
            null, null,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)));

    private static ParameterModel MakeParam(string name, TypeNode type)
        => new(0, name, false, false, type, null,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)));

    // ── Defect 1: No object/void silent fallbacks ───────────────────────────────

    [Fact]
    public void InterfaceEmitter_UnsupportedMethodParam_IsNamedDeferral()
    {
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeSymbol("TestIface", "interface",
        [
            MakeInterfaceDecl("TestIface",
            [
                MakeMethodMember("badMethod",
                    new KeywordTypeNode("VoidKeyword"),
                    [MakeParam("p", new IntersectionTypeNode([
                        new KeywordTypeNode("StringKeyword"),
                        new KeywordTypeNode("BooleanKeyword"),
                    ]))])
            ])
        ]);

        var result = emitter.Emit(symbol);
        var outcome = Assert.Single(result.MemberOutcomes);

        Assert.Equal(MemberOutcomeStatus.Deferred, outcome.Status);
        Assert.Equal("intersection-composition", outcome.Phase);
        Assert.Contains("DEFERRED (intersection-composition)", result.Source);
        Assert.DoesNotContain("object", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_UnsupportedMethodReturn_IsNamedDeferral()
    {
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeSymbol("TestIface", "interface",
        [
            MakeInterfaceDecl("TestIface",
            [
                MakeMethodMember("badReturn",
                    new IntersectionTypeNode([
                        new KeywordTypeNode("StringKeyword"),
                        new KeywordTypeNode("NumberKeyword")
                    ]),
                    [])
            ])
        ]);

        var result = emitter.Emit(symbol);
        var outcome = Assert.Single(result.MemberOutcomes);

        Assert.Equal(MemberOutcomeStatus.Deferred, outcome.Status);
        Assert.Equal("intersection-composition", outcome.Phase);
        Assert.Contains("DEFERRED (intersection-composition)", result.Source);
        Assert.DoesNotContain("BadReturn(", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_SuccessfulEmit_DoesNotContainProjFailed()
    {
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeSymbol("SimpleIface", "interface",
        [
            MakeInterfaceDecl("SimpleIface",
            [
                MakePropMember("name", new KeywordTypeNode("StringKeyword"))
            ])
        ]);

        var source = emitter.Emit(symbol).Source;
        Assert.DoesNotContain("PROJECTION-FAILED", source);
        Assert.DoesNotContain("GENERATION-FAILED", source);
        Assert.Contains("#nullable enable", source);
    }

    [Fact]
    public void CallbackEmitter_FailedReturnType_Throws_NotVoidFallback()
    {
        // Unresolved return type → must throw, not fall back to void
        var resolver = EmptyResolver();
        var emitter = new CallbackEmitter(resolver, "1.0.0", "Blazor.DOM");

        // FunctionTypeNode: typeParameters, parameters, returnType
        var fnType1 = new FunctionTypeNode(
            [],
            [],
            new ReferenceTypeNode("NoSuchReturnType", null, []));

        var symbol1 = MakeSymbol("BadCallback", "callback",
        [
            MakeTypeAliasDecl("BadCallback", fnType1)
        ]);

        var ex = Assert.Throws<TypeProjectionException>(() => emitter.Emit(symbol1));
        Assert.Contains("NoSuchReturnType", ex.Message);
    }

    [Fact]
    public void CallbackEmitter_FailedParamType_Throws_NotObjectFallback()
    {
        var resolver = EmptyResolver();
        var emitter = new CallbackEmitter(resolver, "1.0.0", "Blazor.DOM");

        var fnType2 = new FunctionTypeNode(
            [],
            [MakeParam("x", new ReferenceTypeNode("NoSuchParamType", null, []))],
            new KeywordTypeNode("VoidKeyword"));

        var symbol2 = MakeSymbol("BadParamCallback", "callback",
        [
            MakeTypeAliasDecl("BadParamCallback", fnType2)
        ]);

        var ex2 = Assert.Throws<TypeProjectionException>(() => emitter.Emit(symbol2));
        Assert.Contains("NoSuchParamType", ex2.Message);
    }

    [Fact]
    public void DictionaryEmitter_FailedMember_Throws_NotComment()
    {
        var resolver = EmptyResolver();
        var emitter = new DictionaryEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeSymbol("BadDict", "dictionary",
        [
            MakeInterfaceDecl("BadDict",
            [
                MakePropMember("bad", new ReferenceTypeNode("UnknownType", null, []))
            ])
        ]);

        Assert.Throws<TypeProjectionException>(() => emitter.Emit(symbol));
    }

    [Fact]
    public void AliasEmitter_FailedUnionArm_Throws_NotObjectArm()
    {
        var resolver = EmptyResolver();
        var emitter = new AliasEmitter(resolver, "1.0.0", "Blazor.DOM");

        // Mixed union with one unresolvable arm → must throw
        var union = new UnionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new ReferenceTypeNode("UnresolvableType", null, []),
        ]);

        var symbol = MakeSymbol("BadAlias", "typedef",
        [
            MakeTypeAliasDecl("BadAlias", union)
        ]);

        Assert.Throws<TypeProjectionException>(() => emitter.Emit(symbol));
    }

    [Fact]
    public void AliasEmitter_FailedSimpleAlias_Throws_NotGenerationFailedComment()
    {
        var resolver = EmptyResolver();
        var emitter = new AliasEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeSymbol("BadSimple", "typedef",
        [
            MakeTypeAliasDecl("BadSimple",
                new ReferenceTypeNode("UnresolvableType", null, []))
        ]);

        Assert.Throws<TypeProjectionException>(() => emitter.Emit(symbol));
    }

    // ── Defect 3: Ambiguous overrides must be external ──────────────────────────

    [Fact]
    public void GenerationPipeline_AmbiguousSymbol_WithNoOverride_Fails()
    {
        var ambiguousSymbol = new SymbolModel(0, "AmbiguousType", 0, [], false,
            new SemanticModel("ambiguous", "AmbiguousType", "definition", null, [],
                [], [], false, false, [], false, false, false, [], []));

        var manifest = CreateDummyManifest();
        var ir = new IrBundle(manifest, [ambiguousSymbol], []);
        var emptyOverrides = new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal);
        var outputDir = CreateTempDir();
        try
        {
            var result = GenerationPipeline.Run(ir, outputDir, emptyOverrides);
            // Must have exactly 1 failure for the ambiguous symbol
            Assert.Single(result.Errors);
            Assert.Contains("AmbiguousType", result.Errors[0].SymbolName);
            Assert.Equal(1, result.Manifest.Accounting.GenerationFailed);
        }
        finally { Directory.Delete(outputDir, true); }
    }

    [Fact]
    public void GenerationPipeline_AmbiguousSymbol_WithOverride_Proceeds()
    {
        // Ambiguous symbol with a valid override (→ enum classification)
        var literals = (TypeNode)new LiteralTypeNode("StringLiteral", "\"value\"");
        var decl = new DeclarationModel(0, "typeAlias", "AmbiguousEnum", [], [], [], [],
            literals, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)),
            null, false, new EventMapModel(false, []), []);

        var ambiguousSymbol = new SymbolModel(0, "AmbiguousEnum", 0, [decl], false,
            new SemanticModel("ambiguous", "AmbiguousEnum", "definition", null, [],
                [], [], false, false, [], false, false, false, [], []));

        var manifest = CreateDummyManifest();
        var ir = new IrBundle(manifest, [ambiguousSymbol], []);
        var overrides = new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal)
        {
            ["AmbiguousEnum"] = new EmitterOverrideEntry(
                "AmbiguousEnum", "enum",
                "Reviewed: single-value string literal union; treated as an enum for type safety.")
        };
        var outputDir = CreateTempDir();
        try
        {
            var result = GenerationPipeline.Run(ir, outputDir, overrides);
            Assert.Empty(result.Errors);
            Assert.Equal(1, result.Manifest.Accounting.Projected);
        }
        finally { Directory.Delete(outputDir, true); }
    }

    // ── Defect 5: EmitterOverridesLoader validation ─────────────────────────────

    [Fact]
    public void EmitterOverridesLoader_EmptyOverridesFile_ReturnsEmpty()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "schemaVersion": 1,
                    "ambiguousSymbolOverrides": []
                }
                """;
            File.WriteAllText(Path.Combine(dir, "emitter-overrides.json"), json);
            var overrides = EmitterOverridesLoader.Load(dir);
            Assert.Empty(overrides);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmitterOverridesLoader_AbsentFile_ReturnsEmpty()
    {
        var dir = CreateTempDir();
        try
        {
            var overrides = EmitterOverridesLoader.Load(dir);
            Assert.Empty(overrides);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmitterOverridesLoader_EntryWithEmptyRationale_Throws()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "schemaVersion": 1,
                    "ambiguousSymbolOverrides": [
                        { "symbol": "Foo", "classification": "dictionary", "rationale": "short" }
                    ]
                }
                """;
            File.WriteAllText(Path.Combine(dir, "emitter-overrides.json"), json);
            Assert.Throws<EmitterOverridesException>(() => EmitterOverridesLoader.Load(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmitterOverridesLoader_ValidEntry_IsLoaded()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "schemaVersion": 1,
                    "ambiguousSymbolOverrides": [
                        { "symbol": "MyType", "classification": "dictionary",
                          "rationale": "Reviewed: WebIDL says dictionary; TS shape is an init-only options bag." }
                    ]
                }
                """;
            File.WriteAllText(Path.Combine(dir, "emitter-overrides.json"), json);
            var overrides = EmitterOverridesLoader.Load(dir);
            Assert.Single(overrides);
            Assert.True(overrides.ContainsKey("MyType"));
            Assert.Equal("dictionary", overrides["MyType"].Classification);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Defect 6: Type semantics ────────────────────────────────────────────────

    [Fact]
    public void TypeResolver_ReadableStream_IsNotMappedToSystemIOStream()
    {
        // ReadableStream must be resolved via the symbol index (I-prefixed proxy interface),
        // NOT silently mapped to System.IO.Stream.
        var resolver = EmptyResolver(); // No ReadableStream in index
        var node = new ReferenceTypeNode("ReadableStream", null, []);
        // Without ReadableStream in the symbol index, it must throw (unresolved), not return Stream
        var ex = Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test"));
        Assert.DoesNotContain("System.IO.Stream", ex.Message);
    }

    [Fact]
    public void TypeResolver_ReadableStream_WithSymbolInIndex_ReturnIReadableStream()
    {
        var resolver = WithInterface("ReadableStream");
        var node = new ReferenceTypeNode("ReadableStream", null, []);
        var result = resolver.Project(node, "test");
        Assert.Equal("IReadableStream", result.CSharpType);
    }

    [Fact]
    public void TypeResolver_UnparameterizedArray_ThrowsNotObjectArray()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("Array", null, []);  // no type args
        var ex = Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test/array"));
        Assert.DoesNotContain("object[]", ex.Message);
        Assert.Contains("Unparameterized", ex.Message);
    }

    [Fact]
    public void TypeResolver_ParameterizedArray_ReturnsCSharpArray()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("Array", null,
            [new KeywordTypeNode("StringKeyword")]);
        var result = resolver.Project(node, "test");
        Assert.Equal("string[]", result.CSharpType);
    }

    [Fact]
    public void TypeResolver_UnparameterizedIterable_ThrowsNotFallback()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("Iterable", null, []);
        var ex = Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test/iter"));
        Assert.Contains("Unparameterized", ex.Message);
    }

    // ── Defect 4: #nullable enable in all generated files ───────────────────────

    [Fact]
    public void DictionaryEmitter_Output_ContainsNullableEnable()
    {
        var resolver = EmptyResolver();
        var emitter = new DictionaryEmitter(resolver, "1.0.0", "Blazor.DOM");
        var symbol = MakeSymbol("EmptyDict", "dictionary",
        [
            MakeInterfaceDecl("EmptyDict", [])
        ]);
        var source = emitter.Emit(symbol);
        Assert.Contains("#nullable enable", source);
    }

    [Fact]
    public void CallbackEmitter_Output_ContainsNullableEnable()
    {
        var resolver = EmptyResolver();
        var emitter = new CallbackEmitter(resolver, "1.0.0", "Blazor.DOM");

        var fnType3 = new FunctionTypeNode([], [], new KeywordTypeNode("VoidKeyword"));
        var symbol3 = MakeSymbol("MyCallback", "callback",
        [
            MakeTypeAliasDecl("MyCallback", fnType3)
        ]);

        var source = emitter.Emit(symbol3);
        Assert.Contains("#nullable enable", source);
    }

    [Fact]
    public void InterfaceEmitter_Output_ContainsNullableEnable()
    {
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeSymbol("EmptyIface", "interface",
        [
            MakeInterfaceDecl("EmptyIface", [])
        ]);

        var source = emitter.Emit(symbol).Source;
        Assert.Contains("#nullable enable", source);
    }

    [Fact]
    public void AliasEmitter_Output_ContainsNullableEnable()
    {
        var resolver = EmptyResolver();
        var emitter = new AliasEmitter(resolver, "1.0.0", "Blazor.DOM");

        var union = new UnionTypeNode([
            new LiteralTypeNode("StringLiteral", "\"a\""),
            new LiteralTypeNode("StringLiteral", "\"b\""),
        ]);

        var symbol = MakeSymbol("MyEnum", "typedef",
        [
            MakeTypeAliasDecl("MyEnum", union)
        ]);

        var source = emitter.Emit(symbol);
        Assert.Contains("#nullable enable", source);
    }

    // ── Defect 8: Manifest must not contain checkout paths ─────────────────────

    [Fact]
    public void AccountingLedger_BuildManifest_DoesNotContainAbsolutePaths()
    {
        var ledger = new AccountingLedger();
        ledger.RecordProjected(MakeProjectedSymbol("Alpha"), "Alpha.g.cs");

        var manifest = ledger.BuildManifest("1.0.0", CreateDummyManifest());
        var json = System.Text.Json.JsonSerializer.Serialize(manifest,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        // Must not contain any absolute path separators that would reveal checkout dirs
        Assert.DoesNotContain(":\\", json);     // Windows absolute path
        Assert.DoesNotContain("/home/", json);  // Linux home dir
        Assert.DoesNotContain("/Users/", json); // macOS home dir
        Assert.DoesNotContain("run1", json);    // stale run1 reference
        Assert.DoesNotContain("run2", json);    // stale run2 reference
    }

    // ── Pipeline-level: generation failures make Errors non-empty ──────────────

    [Fact]
    public void GenerationPipeline_WithFailures_HasNonZeroErrorCount()
    {
        // A symbol whose emitter will throw (empty interface with no decl)
        var symbol = new SymbolModel(0, "BadInterface", 0, [], false,
            new SemanticModel("matched", "BadInterface", "definition", null, ["interface"],
                [], [], false, false, [], false, false, false, [], []));

        var manifest = CreateDummyManifest();
        var ir = new IrBundle(manifest, [symbol], []);
        var outputDir = CreateTempDir();
        try
        {
            var result = GenerationPipeline.Run(ir, outputDir);
            Assert.True(result.Errors.Count > 0,
                "Expected at least one error from symbol with no declarations");
            Assert.Equal(1, result.Manifest.Accounting.GenerationFailed);
        }
        finally { Directory.Delete(outputDir, true); }
    }

    // ── No PROJECTION-FAILED/GENERATION-FAILED in committed files ──────────────

    [Fact]
    public void GenerationPipeline_SuccessfulProjection_NoForbiddenComments()
    {
        var enumLiteral = new UnionTypeNode([
            new LiteralTypeNode("StringLiteral", "\"val-a\""),
            new LiteralTypeNode("StringLiteral", "\"val-b\""),
        ]);
        var decl = new DeclarationModel(0, "typeAlias", "SafeEnum", [], [], [], [],
            enumLiteral, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)),
            null, false, new EventMapModel(false, []), []);

        var symbol = new SymbolModel(0, "SafeEnum", 0, [decl], false,
            new SemanticModel("matched", "SafeEnum", "definition", null, ["enum"],
                [], [], false, false, [], false, false, false, [], []));

        var manifest = CreateDummyManifest();
        var ir = new IrBundle(manifest, [symbol], []);
        var outputDir = CreateTempDir();
        try
        {
            var result = GenerationPipeline.Run(ir, outputDir);
            Assert.Empty(result.Errors);
            foreach (var file in result.WrittenFiles)
            {
                var fullPath = Path.Combine(outputDir, file.RelativePath);
                var content = File.ReadAllText(fullPath);
                Assert.DoesNotContain("PROJECTION-FAILED", content);
                Assert.DoesNotContain("GENERATION-FAILED", content);
            }
        }
        finally { Directory.Delete(outputDir, true); }
    }

    // ── Byte identity ───────────────────────────────────────────────────────────

    [Fact]
    public void GenerationPipeline_TwoRuns_ProduceByteIdenticalOutput()
    {
        var enumLiteral = new UnionTypeNode([
            new LiteralTypeNode("StringLiteral", "\"x\""),
            new LiteralTypeNode("StringLiteral", "\"y\""),
        ]);
        var decl = new DeclarationModel(0, "typeAlias", "DetEnum", [], [], [], [],
            enumLiteral, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)),
            null, false, new EventMapModel(false, []), []);
        var symbol = new SymbolModel(0, "DetEnum", 0, [decl], false,
            new SemanticModel("matched", "DetEnum", "definition", null, ["enum"],
                [], [], false, false, [], false, false, false, [], []));

        var manifest = CreateDummyManifest();
        var ir = new IrBundle(manifest, [symbol], []);
        var run1 = CreateTempDir();
        var run2 = CreateTempDir();
        try
        {
            var r1 = GenerationPipeline.Run(ir, run1);
            var r2 = GenerationPipeline.Run(ir, run2);
            var verification = OutputVerifier.Verify(r1.WrittenFiles, r2.WrittenFiles);
            Assert.True(verification.Identical,
                "Two runs must produce byte-identical output (deterministic generation).");
        }
        finally
        {
            Directory.Delete(run1, true);
            Directory.Delete(run2, true);
        }
    }

    // ── Type fix regressions (parenthesized, null literal kind, bool|options, this-param) ──

    [Fact]
    public void TypeResolver_ParenthesizedTypeNode_Unwraps_ToInnerType()
    {
        // A parenthesized wrapper around a keyword type → unwrap and project the inner type
        var resolver = EmptyResolver();
        var inner = new KeywordTypeNode("StringKeyword");
        var parenthesized = new ParenthesizedTypeNode(inner);
        var result = resolver.Project(parenthesized, "test/parenthesized");
        Assert.Equal("string", result.CSharpType);
    }

    [Fact]
    public void TypeResolver_ParenthesizedFunctionType_IsProjectedAsDelegate()
    {
        // (ev: Event) => void wrapped in parentheses → unwrap to function → Action<IEvent>
        var resolver = WithInterface("Event");
        var fn = new FunctionTypeNode(
            [],
            [MakeParam("ev", new ReferenceTypeNode("Event", null, []))],
            new KeywordTypeNode("VoidKeyword"));
        var parenthesized = new ParenthesizedTypeNode(fn);
        var result = resolver.Project(parenthesized, "test/paren-fn");
        Assert.Equal("Action<IEvent>", result.CSharpType);
    }

    [Fact]
    public void TypeResolver_NullLiteralKind_NullKeyword_FilteredFromUnion()
    {
        // T | null where null has LiteralKind="NullKeyword" (as emitted by the TypeScript IR)
        // must be projected to T? — NOT fail as "Mixed union"
        var resolver = EmptyResolver();
        // string | null (null represented as LiteralTypeNode with LiteralKind="NullKeyword")
        var union = new UnionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new LiteralTypeNode("NullKeyword", "null"),
        ]);
        var result = resolver.Project(union, "test/null-literal-kind");
        Assert.Equal("string", result.CSharpType);
        Assert.True(result.IsNullable);
    }

    [Fact]
    public void TypeResolver_BoolOrEventListenerOptions_UsesTypedUnionWithoutCollapsing()
    {
        // A mixed bool/options value remains a discriminated union; it must not
        // collapse to either bool or a nullable options record.
        var eventListenerOptions = new SymbolModel(0, "EventListenerOptions", 0,
            [MakeInterfaceDecl("EventListenerOptions", [])], false,
            new SemanticModel("matched", "EventListenerOptions", "definition", null, ["dictionary"],
                [], [], false, false, [], false, false, false, [], []));
        var resolver = new TypeResolver([eventListenerOptions]);
        var union = new UnionTypeNode([
            new KeywordTypeNode("BooleanKeyword"),
            new ReferenceTypeNode("EventListenerOptions", null, []),
        ]);
        var result = resolver.Project(union, "test/bool-options");
        Assert.Equal("typed-union", result.ProviderNote);
        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes." +
            "TestBoolOptionsBooleanOrEventListenerOptionsUnion",
            result.RenderedType);
    }

    [Fact]
    public void InterfaceEmitter_BoolOptionsParam_ExpandsToTwoOverloads()
    {
        // A method with boolean | EventListenerOptions param must produce two overloads:
        // one with (bool capture = false) and one with (EventListenerOptions? options = null).
        var eventListenerOptions = new SymbolModel(0, "EventListenerOptions", 0,
            [MakeInterfaceDecl("EventListenerOptions", [])], false,
            new SemanticModel("matched", "EventListenerOptions", "definition", null, ["dictionary"],
                [], [], false, false, [], false, false, false, [], []));
        var resolver = new TypeResolver([eventListenerOptions]);
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var boolOptionsType = new UnionTypeNode([
            new KeywordTypeNode("BooleanKeyword"),
            new ReferenceTypeNode("EventListenerOptions", null, []),
        ]);
        var method = new MemberModel(0, "method",
            new NameNode("identifier", "doSomething"),
            false, false, false,
            [],
            [MakeParam("options", boolOptionsType)],
            null, new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)));

        var symbol = MakeSymbol("ITest", "interface", [MakeInterfaceDecl("ITest", [method])]);
        var result = emitter.Emit(symbol);

        // Must produce two method overloads in the source
        Assert.Contains("bool capture", result.Source);
        Assert.Contains("EventListenerOptions?", result.Source);
        // The bool arm must NOT be discarded
        Assert.DoesNotContain("object", result.Source.Replace("// object", ""));
    }

    [Fact]
    public void TypeResolver_TrueKeywordLiteral_MapsToBool()
    {
        // LiteralTypeNode with LiteralKind="TrueKeyword" must project to bool
        var resolver = EmptyResolver();
        var node = new LiteralTypeNode("TrueKeyword", "true");
        var result = resolver.Project(node, "test/true-keyword");
        Assert.Equal("bool", result.CSharpType);
    }

    [Fact]
    public void TypeResolver_FalseKeywordLiteral_MapsToBool()
    {
        // LiteralTypeNode with LiteralKind="FalseKeyword" must project to bool
        var resolver = EmptyResolver();
        var node = new LiteralTypeNode("FalseKeyword", "false");
        var result = resolver.Project(node, "test/false-keyword");
        Assert.Equal("bool", result.CSharpType);
    }

    [Fact]
    public void TypeResolver_FunctionType_SkipsThisParameter()
    {
        // (this: AbortSignal, ev: Event) => any → Action<IEvent> (NOT Action<IAbortSignal, IEvent>)
        // The synthetic `this` parameter must be excluded from C# delegate projections.
        var resolver = WithInterface("Event");
        var fn = new FunctionTypeNode(
            [],
            [
                MakeParam("this", new KeywordTypeNode("StringKeyword")), // 'this' param
                MakeParam("ev", new ReferenceTypeNode("Event", null, [])),
            ],
            new KeywordTypeNode("AnyKeyword"));
        var result = resolver.Project(fn, "test/this-param");
        // 'this' skipped; return 'any' → 'object'; Func<IEvent, object>
        Assert.Equal("Func<IEvent, object>", result.CSharpType);
    }

    [Fact]
    public void InterfaceEmitter_EventSubscriptionSuffixWithoutMap_FailsClosed()
    {
        // addEventListener with K extends keyof SomeEventMap → event-subscription deferral
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        // Use "KeyOfKeyword" as the operator — matches the TypeScript IR's SyntaxKind serialization
        var keyofConstraint = new OperatorTypeNode("KeyOfKeyword",
            new ReferenceTypeNode("HTMLElementEventMap", null, []));
        var typeParam = new TypeParameterModel(0, "K", keyofConstraint, null);
        var genericMethod = new MemberModel(0, "method",
            new NameNode("identifier", "addEventListener"),
            false, false, false,
            [typeParam],
            [MakeParam("type", new ReferenceTypeNode("K", null, []))],
            null, new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)));

        var symbol = MakeSymbol("ITestTarget", "interface",
        [
            MakeInterfaceDecl("ITestTarget", [genericMethod])
        ]);

        var exception = Assert.Throws<InterfaceEmitException>(() =>
            emitter.Emit(symbol));
        Assert.Contains(
            "does not resolve to an EventMap symbol",
            exception.Message);
    }

    [Fact]
    public void InterfaceEmitter_NonEventGenericMethod_Emits()
    {
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var typeParam = new TypeParameterModel(0, "T", null, null);
        var nonEventGeneric = new MemberModel(0, "method",
            new NameNode("identifier", "someGenericMethod"),
            false, false, false,
            [typeParam],
            [MakeParam("value", new ReferenceTypeNode("T", null, []))],
            null, new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)));

        var symbol = MakeSymbol("ITestTarget", "interface",
        [
            MakeInterfaceDecl("ITestTarget", [nonEventGeneric])
        ]);

        var result = emitter.Emit(symbol);
        Assert.Contains(
            "void SomeGenericMethod<T>(T @value);",
            result.Source);
        Assert.DoesNotContain(
            result.MemberOutcomes,
            outcome => outcome.Status != MemberOutcomeStatus.Projected);
    }

    // ── Finding 3: Member-level accounting ────────────────────────────────────

    [Fact]
    public void InterfaceEmitter_UnresolvedEventMapCannotCreateHiddenDeferral()
    {
        // A symbol with event-subscription deferred members must produce
        // ProjectedWithDeferredMembers outcome, not unqualified Projected.
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var keyofConstraint = new OperatorTypeNode("KeyOfKeyword",
            new ReferenceTypeNode("FooEventMap", null, []));
        var typeParam = new TypeParameterModel(0, "K", keyofConstraint, null);
        var eventMethod = new MemberModel(0, "method",
            new NameNode("identifier", "addEventListener"),
            false, false, false,
            [typeParam],
            [MakeParam("type", new ReferenceTypeNode("K", null, []))],
            null, new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)));

        var symbol = MakeSymbol("IFoo", "interface", [MakeInterfaceDecl("IFoo", [eventMethod])]);
        var exception = Assert.Throws<InterfaceEmitException>(() =>
            emitter.Emit(symbol));
        Assert.Contains(
            "does not resolve to an EventMap symbol",
            exception.Message);
    }

    [Fact]
    public void AccountingLedger_ProjectedWithDeferredMembers_AppearsInManifest()
    {
        var ledger = new AccountingLedger();
        var memberOutcomes = new List<MemberOutcome>
        {
            new(0, "addEventListener", "method", MemberOutcomeStatus.Projected),
            new(1, "addEventListener", "method", MemberOutcomeStatus.Deferred, "event-subscription", "generic"),
        };
        var symbol = MakeSymbol(
            "IFoo",
            "interface",
            [
                MakeInterfaceDecl(
                    "IFoo",
                    [
                        MakeMethodMember(
                            "addEventListener",
                            new KeywordTypeNode("VoidKeyword"),
                            [],
                            ordinal: 0),
                        MakeMethodMember(
                            "addEventListener",
                            new KeywordTypeNode("VoidKeyword"),
                            [],
                            ordinal: 1),
                    ])
            ]);
        ledger.RecordProjected(symbol, "IFoo.g.cs", memberOutcomes);

        var manifest = ledger.BuildManifest("1.0.0", CreateDummyManifest());

        Assert.Equal(1, manifest.Accounting.ProjectedWithDeferredMembers);
        Assert.Equal(0, manifest.Accounting.ProjectedClean);
        Assert.Equal(1, manifest.Accounting.Projected); // includes both buckets
        Assert.Single(manifest.Accounting.DeferredMemberEntries);
        Assert.Equal("IFoo", manifest.Accounting.DeferredMemberEntries[0].SymbolName);
        Assert.Equal("event-subscription", manifest.Accounting.DeferredMemberEntries[0].Phase);
    }

    // ── D1: CS0121 — bool|options must expand to THREE unambiguous overloads ──────

    [Fact]
    public void InterfaceEmitter_BoolOptionsParam_ExpandsToThreeOverloads_NotTwo()
    {
        // D1 regression: 2-optional-overload expansion is ambiguous (CS0121).
        // Must produce 3 unambiguous overloads:
        //   (type, cb) — no third param
        //   (type, cb, bool capture)
        //   (type, cb, EventListenerOptions? options)
        var eventListenerOptions = new SymbolModel(0, "EventListenerOptions", 0,
            [MakeInterfaceDecl("EventListenerOptions", [])], false,
            new SemanticModel("matched", "EventListenerOptions", "definition", null, ["dictionary"],
                [], [], false, false, [], false, false, false, [], []));
        var resolver = new TypeResolver([eventListenerOptions]);
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var boolOptionsType = new UnionTypeNode([
            new KeywordTypeNode("BooleanKeyword"),
            new ReferenceTypeNode("EventListenerOptions", null, []),
        ]);
        // The source param is optional (like addEventListener's third param)
        var optParam = new ParameterModel(2, "options", Optional: true, Rest: false,
            Type: boolOptionsType, Initializer: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(0,0,0), new(0,0,0)));
        var method = new MemberModel(0, "method",
            new NameNode("identifier", "addEventListener"),
            false, false, false,
            [],
            [MakeParam("type", new KeywordTypeNode("StringKeyword")),
             MakeParam("listener", new KeywordTypeNode("AnyKeyword")),
             optParam],
            null, new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)));

        var symbol = MakeSymbol("IEventTarget2", "interface", [MakeInterfaceDecl("IEventTarget2", [method])]);
        var result = emitter.Emit(symbol);

        // Must contain all three overload signatures
        // 1. No-third-param overload
        Assert.Contains("void AddEventListener(string type, object listener);", result.Source);
        // 2. bool required
        Assert.Contains("void AddEventListener(string type, object listener, bool capture);", result.Source);
        // 3. options required (no default = no '= null')
        Assert.Contains("EventListenerOptions? options", result.Source);
        Assert.DoesNotContain("EventListenerOptions? options =", result.Source);
        Assert.DoesNotContain("bool capture =", result.Source);
    }

    // ── D2: Mutable properties — Readonly=false PropertySignature → { get; set; } ──

    [Fact]
    public void InterfaceEmitter_MutableProperty_EmitsGetterAndSetter()
    {
        // D2 regression: cancelBubble (Readonly=false, kind=property) must emit { get; set; }
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var writableProp = new MemberModel(1, "property",
            new NameNode("identifier", "cancelBubble"),
            Optional: false, Readonly: false, Static: false,
            TypeParameters: [], Parameters: [],
            Type: new KeywordTypeNode("BooleanKeyword"), ReturnType: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(0,0,0), new(0,0,0)));

        var symbol = MakeSymbol("IEvent", "interface", [MakeInterfaceDecl("IEvent", [writableProp])]);
        var result = emitter.Emit(symbol);

        Assert.Contains("bool CancelBubble { get; set; }", result.Source);
        Assert.DoesNotContain("CancelBubble { get; }", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_ReadonlyProperty_EmitsGetterOnly()
    {
        // D2 regression: Readonly=true PropertySignature must remain { get; }
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var readonlyProp = new MemberModel(0, "property",
            new NameNode("identifier", "bubbles"),
            Optional: false, Readonly: true, Static: false,
            TypeParameters: [], Parameters: [],
            Type: new KeywordTypeNode("BooleanKeyword"), ReturnType: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(0,0,0), new(0,0,0)));

        var symbol = MakeSymbol("IEvent", "interface", [MakeInterfaceDecl("IEvent", [readonlyProp])]);
        var result = emitter.Emit(symbol);

        Assert.Contains("bool Bubbles { get; }", result.Source);
        Assert.DoesNotContain("get; set;", result.Source);
    }

    // ── D3: Merged declarations — all decls must be processed ─────────────────

    [Fact]
    public void InterfaceEmitter_MergedDeclarations_AllMembersProjected()
    {
        // D3 regression: an interface with 2 merged declarations must emit members from both.
        // Models the Headers pattern: first decl has append/delete, second decl has get/has.
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var decl0 = new DeclarationModel(
            Ordinal: 0, Kind: "interface", Name: "Headers",
            Modifiers: [], TypeParameters: [], Heritage: [],
            Members: [
                MakeMethodMember("append", new KeywordTypeNode("VoidKeyword"),
                    [MakeParam("name", new KeywordTypeNode("StringKeyword")),
                     MakeParam("value", new KeywordTypeNode("StringKeyword"))],
                    ordinal: 0),
                MakeMethodMember("delete", new KeywordTypeNode("VoidKeyword"),
                    [MakeParam("name", new KeywordTypeNode("StringKeyword"))],
                    ordinal: 1),
            ],
            Type: null, Parameters: [], ReturnType: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(0,0,0), new(0,0,0)),
            VariableKind: null, ConstructorObject: false,
            EventMap: new EventMapModel(false, []), NamespaceMembers: []);

        var decl2 = new DeclarationModel(
            Ordinal: 2, Kind: "interface", Name: "Headers",
            Modifiers: [], TypeParameters: [], Heritage: [],
            Members: [
                MakeMethodMember("get", new UnionTypeNode([
                    new KeywordTypeNode("StringKeyword"),
                    new LiteralTypeNode("NullKeyword", "null")]),
                    [MakeParam("name", new KeywordTypeNode("StringKeyword"))],
                    ordinal: 0),
                MakeMethodMember("has", new KeywordTypeNode("BooleanKeyword"),
                    [MakeParam("name", new KeywordTypeNode("StringKeyword"))],
                    ordinal: 1),
            ],
            Type: null, Parameters: [], ReturnType: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(1,0,0), new(1,0,0)),
            VariableKind: null, ConstructorObject: false,
            EventMap: new EventMapModel(false, []), NamespaceMembers: []);

        var symbol = MakeSymbol("Headers", "interface", [decl0, decl2]);
        var result = emitter.Emit(symbol);

        // Members from both declarations must be emitted
        Assert.Contains("Append(", result.Source);
        Assert.Contains("Delete(", result.Source);
        Assert.Contains("Get(", result.Source);
        Assert.Contains("Has(", result.Source);
        // All 4 members must be accounted for
        var projected = result.MemberOutcomes.Where(m => m.Status == MemberOutcomeStatus.Projected).ToList();
        Assert.Equal(4, projected.Count);
    }

    [Fact]
    public void InterfaceEmitter_MergedDeclarations_DuplicateMember_NotDoubleEmitted()
    {
        // D3: if both merged decls have the same member name, it must be emitted once.
        var resolver = EmptyResolver();
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var sharedMember = MakePropMember("value", new KeywordTypeNode("StringKeyword"));
        var decl0 = new DeclarationModel(0, "interface", "Dup", [], [], [],
            [sharedMember], null, [], null,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)),
            null, false, new EventMapModel(false, []), []);
        var decl1 = new DeclarationModel(1, "interface", "Dup", [], [], [],
            [sharedMember], null, [], null,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(1,0,0), new(1,0,0)),
            null, false, new EventMapModel(false, []), []);

        var symbol = MakeSymbol("Dup", "interface", [decl0, decl1]);
        var result = emitter.Emit(symbol);

        // Property 'Value' must appear exactly once
        var occurrences = result.Source.Split("string Value").Length - 1;
        Assert.Equal(1, occurrences);
    }

    // ── D4: Dictionary inheritance — record must inherit when base is a known dict ──

    [Fact]
    public void DictionaryEmitter_DictionaryInheritance_EmitsBaseClause()
    {
        // D4 regression: AddEventListenerOptions : EventListenerOptions must be emitted.
        var emitter = new DictionaryEmitter(
            new TypeResolver([new SymbolModel(0, "EventListenerOptions", 0, [], false,
                new SemanticModel("matched", "EventListenerOptions", "definition", null, ["dictionary"],
                    [], [], false, false, [], false, false, false, [], []))]),
            "1.0.0", "Blazor.DOM");

        var declWithHeritage = new DeclarationModel(0, "interface", "AddEventListenerOptions", [], [],
            [new HeritageClauseModel("extends", [new HeritageReferenceTypeNode("EventListenerOptions", null, [])])],
            [new MemberModel(0, "property", new NameNode("identifier", "once"), false, false, false, [], [],
                new KeywordTypeNode("BooleanKeyword"), null,
                new DocumentationModel("", [], false),
                new LocationModel("test", new(0,0,0), new(0,0,0)))],
            null, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)),
            null, false, new EventMapModel(false, []), []);

        var symWithHeritage = MakeSymbol("AddEventListenerOptions", "dictionary", [declWithHeritage]);
        var src = emitter.Emit(symWithHeritage);

        Assert.Contains("public record AddEventListenerOptions : EventListenerOptions", src);
        Assert.DoesNotContain("// Dictionary base", src);
    }

    [Fact]
    public void DictionaryEmitter_UnknownBaseType_Throws()
    {
        // D4 regression: if the base is not a known dictionary, must fail closed.
        var emitter = new DictionaryEmitter(new TypeResolver([]), "1.0.0", "Blazor.DOM");

        var decl = new DeclarationModel(0, "interface", "MyDict", [], [],
            [new HeritageClauseModel("extends", [new HeritageReferenceTypeNode("UnknownBase", null, [])])],
            [], null, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)),
            null, false, new EventMapModel(false, []), []);

        var sym = MakeSymbol("MyDict", "dictionary", [decl]);
        Assert.Throws<TypeProjectionException>(() => emitter.Emit(sym));
    }

    // ── D5: Profile nondeterminism exits nonzero ────────────────────────────────
    // (Tested via GenerationPipeline.Run PipelineResult.Errors classification.)
    // Full CLI integration tests for D5 are in the ProfilePipeline tests.

    // ── D6: OutputVerifier uses Ordinal path comparison ────────────────────────

    [Fact]
    public void OutputVerifier_CaseDifferentPaths_DetectedAsMismatch()
    {
        // D6 regression: case-different path must not match under Ordinal comparison.
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        try
        {
            // Write file 'Foo.g.cs' in dir1 and 'foo.g.cs' in dir2
            var content = System.Text.Encoding.UTF8.GetBytes("#nullable enable\n// test\n");
            File.WriteAllBytes(Path.Combine(dir1, "Foo.g.cs"), content);
            File.WriteAllBytes(Path.Combine(dir2, "foo.g.cs"), content);

            // Use GenerationPipeline to produce real GeneratedFile lists
            var file1 = new GeneratedFile("Foo.g.cs", "FooType", ComputeSha256(content), content.Length);
            var file2 = new GeneratedFile("foo.g.cs", "FooType", ComputeSha256(content), content.Length);

            var result = OutputVerifier.Verify([file1], [file2]);
            Assert.False(result.Identical,
                "Files with different path casing must be detected as a mismatch (Ordinal comparison).");
        }
        finally
        {
            Directory.Delete(dir1, true);
            Directory.Delete(dir2, true);
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    // ── D7: WriteManifest is included in WrittenFiles for --verify ─────────────

    [Fact]
    public void GenerationPipeline_WrittenFiles_IncludesManifestJson()
    {
        // D7 regression: emitter-manifest.json must be in WrittenFiles so --verify covers it.
        var enumLiteral = new UnionTypeNode([
            new LiteralTypeNode("StringLiteral", "\"x\""),
        ]);
        var decl = new DeclarationModel(0, "typeAlias", "TinyEnum", [], [], [], [],
            enumLiteral, [], null, new DocumentationModel("", [], false),
            new LocationModel("test", new(0,0,0), new(0,0,0)),
            null, false, new EventMapModel(false, []), []);
        var symbol = new SymbolModel(0, "TinyEnum", 0, [decl], false,
            new SemanticModel("matched", "TinyEnum", "definition", null, ["enum"],
                [], [], false, false, [], false, false, false, [], []));

        var manifest = CreateDummyManifest();
        var ir = new IrBundle(manifest, [symbol], []);
        var outputDir = CreateTempDir();
        try
        {
            var result = GenerationPipeline.Run(ir, outputDir);
            Assert.Empty(result.Errors);

            // emitter-manifest.json must appear in WrittenFiles
            var manifestFile = result.WrittenFiles.SingleOrDefault(
                f => f.RelativePath.Equals("emitter-manifest.json", StringComparison.Ordinal));
            Assert.NotNull(manifestFile);
            Assert.True(manifestFile.ByteLength > 0);
        }
        finally { Directory.Delete(outputDir, true); }
    }

    // ── Latest review regressions: D1-D5 ───────────────────────────────────────

    [Fact]
    public void InterfaceEmitter_GetterMember_UsesReturnType_NotType()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var symbol = MakeSymbol("GetterHost", "interface",
        [
            MakeInterfaceDecl("GetterHost", [MakeGetterMember("title", new KeywordTypeNode("StringKeyword"))])
        ]);

        var result = emitter.Emit(symbol);

        Assert.Contains("string Title { get; }", result.Source);
        Assert.DoesNotContain("void Title", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_GetterSetterPair_Compatible_EmitsGetSet()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var decl0 = MakeInterfaceDecl("AccessorHost",
            [MakeGetterMember("value", new KeywordTypeNode("StringKeyword"), ordinal: 0)],
            ordinal: 0);
        var decl1 = MakeInterfaceDecl("AccessorHost",
            [MakeSetterMember("value", [MakeParam("value", new KeywordTypeNode("StringKeyword"))], ordinal: 0)],
            ordinal: 1);

        var result = emitter.Emit(MakeSymbol("AccessorHost", "interface", [decl0, decl1]));

        Assert.Contains("string Value { get; set; }", result.Source);
        Assert.Contains(result.MemberOutcomes, m => m.Kind == "setter" && m.DeclarationOrdinal == 1);
    }

    [Fact]
    public void InterfaceEmitter_GetterSetterPair_Asymmetric_LowersWithoutWidening()
    {
        var emitter = new InterfaceEmitter(WithInterfaces("DOMTokenList"), "1.0.0", "Blazor.DOM");
        var decl0 = MakeInterfaceDecl("AccessorHost",
            [MakeGetterMember("relList", new ReferenceTypeNode("DOMTokenList", null, []), ordinal: 0)],
            ordinal: 0);
        var decl1 = MakeInterfaceDecl("AccessorHost",
            [MakeSetterMember("relList", [MakeParam("value", new KeywordTypeNode("StringKeyword"))], ordinal: 0)],
            ordinal: 2);

        var result = emitter.Emit(
            MakeSymbol("AccessorHost", "interface", [decl0, decl1]));

        Assert.Contains("IDOMTokenList RelList { get; }", result.Source);
        Assert.Contains("void SetRelList(string value);", result.Source);
        Assert.DoesNotContain("IDOMTokenList RelList { get; set; }", result.Source);
        Assert.Contains("DomAccessorOperation.Get", result.Source);
        Assert.Contains("DomAccessorOperation.Set", result.Source);
        Assert.Equal(
            2,
            result.MemberOutcomes.Count(outcome =>
                outcome.Status == MemberOutcomeStatus.Projected));
    }

    [Fact]
    public void InterfaceEmitter_GetterOnly_EmitsGetterOnly()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var result = emitter.Emit(MakeSymbol("GetterOnly", "interface",
        [
            MakeInterfaceDecl("GetterOnly", [MakeGetterMember("name", new KeywordTypeNode("StringKeyword"))])
        ]));

        Assert.Contains("string Name { get; }", result.Source);
        Assert.DoesNotContain("get; set;", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_SetterWithNoParameters_FailsClosed()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var symbol = MakeSymbol("SetterHost", "interface",
        [
            MakeInterfaceDecl("SetterHost",
            [
                MakeGetterMember("name", new KeywordTypeNode("StringKeyword"), ordinal: 0),
                MakeSetterMember("name", [], ordinal: 1)
            ])
        ]);

        var ex = Assert.Throws<InterfaceEmitException>(() => emitter.Emit(symbol));
        Assert.Contains("exactly one value parameter", ex.Message);
        Assert.Equal(
            "SetterHost/decl[0]/member[1]/setter/name",
            ex.Provenance);
    }

    [Fact]
    public void InterfaceEmitter_MergedDecl_DuplicateMethodSameTypes_EmitOnceAccountBoth()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var decl0 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("a", new KeywordTypeNode("StringKeyword"))], ordinal: 0)],
            ordinal: 0);
        var decl2 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("a", new KeywordTypeNode("StringKeyword"))], ordinal: 0)],
            ordinal: 2);

        var result = emitter.Emit(MakeSymbol("MethodHost", "interface", [decl0, decl2]));

        Assert.Equal(1, result.Source.Split("void Foo(string a);").Length - 1);
        Assert.Equal(2, result.MemberOutcomes.Count(m => m.Name == "foo" && m.Kind == "method"));
    }

    [Fact]
    public void InterfaceEmitter_MergedDecl_DuplicateMethodDifferentNames_EmitOnce()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var decl0 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("a", new KeywordTypeNode("StringKeyword"))], ordinal: 0)],
            ordinal: 0);
        var decl1 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("b", new KeywordTypeNode("StringKeyword"))], ordinal: 0)],
            ordinal: 1);

        var result = emitter.Emit(MakeSymbol("MethodHost", "interface", [decl0, decl1]));

        Assert.Equal(1, result.Source.Split("void Foo(").Length - 1);
        Assert.Contains(result.MemberOutcomes, m => m.DeclarationOrdinal == 1 && (m.Reason ?? "").Contains("Deduplicated", StringComparison.Ordinal));
    }

    [Fact]
    public void InterfaceEmitter_ExpandedNoOptionsOverload_CollidesWithExplicit_EmitOnce()
    {
        var eventListenerOptions = new SymbolModel(0, "EventListenerOptions", 0,
            [MakeInterfaceDecl("EventListenerOptions", [])], false,
            new SemanticModel("matched", "EventListenerOptions", "definition", null, ["dictionary"],
                [], [], false, false, [], false, false, false, [], []));
        var emitter = new InterfaceEmitter(new TypeResolver([eventListenerOptions]), "1.0.0", "Blazor.DOM");

        var boolOptionsType = new UnionTypeNode([
            new KeywordTypeNode("BooleanKeyword"),
            new ReferenceTypeNode("EventListenerOptions", null, []),
        ]);
        var explicitDecl = MakeInterfaceDecl("EventTargetLike",
            [MakeMethodMember("addEventListener", new KeywordTypeNode("VoidKeyword"),
                [MakeParam("type", new KeywordTypeNode("StringKeyword")), MakeParam("listener", new KeywordTypeNode("AnyKeyword"))], ordinal: 0)],
            ordinal: 0);
        var expandedDecl = MakeInterfaceDecl("EventTargetLike",
            [new MemberModel(0, "method",
                new NameNode("identifier", "addEventListener"),
                false, false, false,
                [],
                [
                    MakeParam("type", new KeywordTypeNode("StringKeyword")),
                    MakeParam("listener", new KeywordTypeNode("AnyKeyword")),
                    new ParameterModel(2, "options", true, false, boolOptionsType, null,
                        new DocumentationModel("", [], false),
                        new LocationModel("test", new(0, 0, 0), new(0, 0, 0)))
                ],
                null, new KeywordTypeNode("VoidKeyword"),
                new DocumentationModel("", [], false),
                new LocationModel("test", new(0, 0, 0), new(0, 0, 0)))]
            , ordinal: 1);

        var result = emitter.Emit(MakeSymbol("EventTargetLike", "interface", [explicitDecl, expandedDecl]));

        Assert.Equal(1, result.Source.Split("void AddEventListener(string type, object listener);").Length - 1);
        Assert.Contains("bool capture", result.Source);
        Assert.Contains("EventListenerOptions? options", result.Source);
    }

    [Fact]
    public void MemberOutcome_IncludesDeclarationOrdinal()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var decl = MakeInterfaceDecl("OutcomeHost",
            [MakePropMember("name", new KeywordTypeNode("StringKeyword"), ordinal: 0)],
            ordinal: 7);

        var result = emitter.Emit(MakeSymbol("OutcomeHost", "interface", [decl]));

        Assert.Equal(7, Assert.Single(result.MemberOutcomes).DeclarationOrdinal);
    }

    [Fact]
    public void InterfaceEmitter_MergedMethods_SameOrdinal_DifferentDeclOrdinal_DistinctOutcomes()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var decl0 = MakeInterfaceDecl("OutcomeHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [], ordinal: 0)],
            ordinal: 0);
        var decl2 = MakeInterfaceDecl("OutcomeHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [], ordinal: 0)],
            ordinal: 2);

        var result = emitter.Emit(MakeSymbol("OutcomeHost", "interface", [decl0, decl2]));
        var outcomes = result.MemberOutcomes.Where(m => m.Name == "foo").OrderBy(m => m.DeclarationOrdinal).ToList();

        Assert.Equal(2, outcomes.Count);
        Assert.Equal([0, 2], outcomes.Select(m => m.DeclarationOrdinal).ToArray());
        Assert.All(outcomes, m => Assert.Equal(0, m.Ordinal));
    }

    [Fact]
    public void InterfaceEmitter_UnknownHeritage_FailsClosed()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var symbol = MakeSymbol("Derived", "interface",
        [
            MakeInterfaceDecl("Derived", [], heritage:
            [
                new HeritageClauseModel("extends", [new HeritageReferenceTypeNode("UnknownBase", null, [])])
            ])
        ]);

        var ex = Assert.Throws<InterfaceEmitException>(() => emitter.Emit(symbol));
        Assert.Contains("UnknownBase", ex.Message);
        Assert.Contains("Derived/extends/UnknownBase", ex.Provenance);
    }

    [Fact]
    public void InterfaceEmitter_GenericHeritage_FailsClosed()
    {
        var emitter = new InterfaceEmitter(WithInterfaces("KnownBase"), "1.0.0", "Blazor.DOM");
        var symbol = MakeSymbol("Derived", "interface",
        [
            MakeInterfaceDecl("Derived", [], heritage:
            [
                new HeritageClauseModel("extends",
                [
                    new HeritageReferenceTypeNode("KnownBase", null, [new KeywordTypeNode("StringKeyword")])
                ])
            ])
        ]);

        var ex = Assert.Throws<InterfaceEmitException>(() => emitter.Emit(symbol));
        Assert.Contains("target arity is 0", ex.Message);
        Assert.Contains("Derived/decl[0]/extends/KnownBase", ex.Provenance);
    }

    [Fact]
    public void InterfaceEmitter_LaterDeclHeritage_Included()
    {
        var emitter = new InterfaceEmitter(WithInterfaces("BaseType"), "1.0.0", "Blazor.DOM");
        var decl0 = MakeInterfaceDecl("Derived", [], ordinal: 0);
        var decl1 = MakeInterfaceDecl("Derived", [], ordinal: 1, heritage:
        [
            new HeritageClauseModel("extends", [new HeritageReferenceTypeNode("BaseType", null, [])])
        ]);

        var result = emitter.Emit(MakeSymbol("Derived", "interface", [decl0, decl1]));

        Assert.Contains("public partial interface IDerived : IBaseType", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_DuplicateHeritage_DeduplicatedInBaseClause()
    {
        var emitter = new InterfaceEmitter(WithInterfaces("BaseType"), "1.0.0", "Blazor.DOM");
        var decl0 = MakeInterfaceDecl("Derived", [], ordinal: 0, heritage:
        [
            new HeritageClauseModel("extends", [new HeritageReferenceTypeNode("BaseType", null, [])])
        ]);
        var decl1 = MakeInterfaceDecl("Derived", [], ordinal: 1, heritage:
        [
            new HeritageClauseModel("extends", [new HeritageReferenceTypeNode("BaseType", null, [])])
        ]);

        var result = emitter.Emit(MakeSymbol("Derived", "interface", [decl0, decl1]));

        Assert.Equal(1, result.Source.Split("IBaseType").Length - 1);
    }

    [Fact]
    public void ProfilePipeline_CoverageOnlyDrift_ReportsFailure()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir1, "Alpha.g.cs"), "#nullable enable\npublic interface IAlpha {}\n");
            File.WriteAllText(Path.Combine(dir2, "Alpha.g.cs"), "#nullable enable\npublic interface IAlpha {}\n");
            File.WriteAllText(Path.Combine(dir1, "profile-coverage.json"), "{\"byteIdentityVerified\":false}\n");
            File.WriteAllText(Path.Combine(dir2, "profile-coverage.json"), "{\"byteIdentityVerified\":true}\n");

            var scanAllFiles = typeof(ProfilePipeline).GetMethod("ScanAllFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(scanAllFiles);

            var files1 = Assert.IsAssignableFrom<IReadOnlyList<GeneratedFile>>(scanAllFiles!.Invoke(null, [dir1]));
            var files2 = Assert.IsAssignableFrom<IReadOnlyList<GeneratedFile>>(scanAllFiles.Invoke(null, [dir2]));
            var verification = OutputVerifier.Verify(files1, files2);

            Assert.False(verification.Identical);
            Assert.Contains(verification.Mismatches, m => m.RelativePath == "profile-coverage.json");
        }
        finally
        {
            Directory.Delete(dir1, true);
            Directory.Delete(dir2, true);
        }
    }

    [Fact]
    public void OutputVerifier_ScanDirectory_IncludesAllFiles()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Alpha.g.cs"), "#nullable enable\npublic interface IAlpha {}\n");
            File.WriteAllText(Path.Combine(dir, "emitter-manifest.json"), "{}\n");
            File.WriteAllText(Path.Combine(dir, "profile-coverage.json"), "{}\n");

            var files = OutputVerifier.ScanDirectory(dir);

            Assert.Contains(files, f => f.RelativePath == "Alpha.g.cs");
            Assert.Contains(files, f => f.RelativePath == "emitter-manifest.json");
            Assert.Contains(files, f => f.RelativePath == "profile-coverage.json");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static SymbolModel MakeProjectedSymbol(string name)
        => new(0, name, 0, [], false, new SemanticModel(
            "matched", null, null, null, [], [], [], false, false, [], false, false, false, [], []));

    private static ManifestModel CreateDummyManifest() => new(
        SchemaVersion: 1,
        GenerationProfile: new("Window", ["Window"], true),
        Files: new(
            new("typescript-symbols.jsonl", "jsonl", "dummy", 0, new string('a', 64)),
            new("webidl-symbols.jsonl", "jsonl", "dummy", 0, new string('b', 64)),
            new("coverage.json", "json", "dummy", 1, new string('c', 64))),
        Counts: new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        Provenance: new(
            new("test", "0.0.0", null),
            new("typescript", "5.0.0", "MIT", new string('0', 64), []),
            new("@webref/idl", "0.0.0", "MIT"),
            new("webidl2", "0.0.0", "W3C"),
            new("/dev/null", new string('0', 64), 0)));
}
