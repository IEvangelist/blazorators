using System.Security.Cryptography;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Profiles;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class GenericEmitterTests
{
    [Fact]
    public void Corpus_EmitsGenericDeclarationsMethodsFactoriesAliasesAndHeritage()
    {
        var (ir, resolver) = LoadCorpus();
        var output = CreateTempDirectory();
        try
        {
            var result = GenerationPipeline.Run(
                ir,
                output,
                EmitterOverridesLoader.Load(Path.Combine(
                    FindRepositoryRoot(),
                    "data",
                    "Blazor.DOM")));

            Assert.Contains(
                "public partial interface IHTMLCollectionOf<T> : IHTMLCollectionBase where T : IElement",
                Read(output, "Interfaces", "IHTMLCollectionOf.g.cs"));
            Assert.Contains(
                "T AppendChild<T>(T node) where T : INode;",
                Read(output, "Interfaces", "INode.g.cs"));
            Assert.Contains(
                "public delegate T LockGrantedCallback<T>(ILock? @lock);",
                Read(output, "Callbacks", "LockGrantedCallback.g.cs"));
            Assert.Contains(
                "public record QueuingStrategy<T>",
                Read(output, "Dictionaries", "QueuingStrategy.g.cs"));
            Assert.Contains(
                result.Manifest.Accounting.DeferredSymbols,
                entry => entry.Symbol == "ReadableStreamController"
                    && entry.Phase == "typed-union");
            Assert.Contains(
                "ICustomEvent<T> Create<T>(string type, CustomEventInit<T>? eventInitDict = default);",
                Read(output, "Factories", "ICustomEventFactory.g.cs"));
            Assert.Contains(
                "T StructuredClone<T>(T @value, StructuredSerializeOptions? options = default);",
                Read(output, "Globals", "IWindow.Globals.g.cs"));
            Assert.Contains(
                result.Manifest.Accounting.DeferredSymbols,
                entry => entry.Symbol == "FormDataIterator"
                    && entry.Phase == "iterator-transport");
            Assert.DoesNotContain(
                result.Errors,
                error => error.Message.Contains(
                    "generic C# emission is deferred",
                    StringComparison.Ordinal));
            var genericConstraintDeferral = Assert.Single(
                result.Manifest.Accounting.DeferredSymbols,
                entry => entry.Symbol == "WebAssembly.GlobalDescriptor");
            Assert.Equal(
                "advanced-generic-constraints",
                genericConstraintDeferral.Phase);
            Assert.All(
                new[] { "OptionalPrefixToken", "OptionalPostfixToken" },
                symbolName => Assert.Contains(
                    result.Manifest.Accounting.DeferredSymbols,
                    entry => entry.Symbol == symbolName
                        && entry.Phase == "advanced-generic-constraints"));
            Assert.Contains(
                result.Manifest.Accounting.DeferredMemberEntries,
                entry => entry.SymbolName == "ReadableStreamBYOBReader"
                    && entry.MemberName == "read"
                    && entry.Phase == "advanced-generic-constraints");
            Assert.Contains(
                result.Manifest.Accounting.DeferredMemberEntries,
                entry => entry.MemberName == "addEventListener"
                    && entry.Phase == "event-subscription");

            var blob = Assert.Single(
                ir.TypescriptSymbols,
                symbol => symbol.Name == "Blob");
            var stream = blob.Declarations.SelectMany(declaration => declaration.Members)
                .Single(member => member.Name?.Text == "stream").ReturnType!;
            var projection = resolver.Project(stream, "Blob/stream/return");
            Assert.Equal("transferable", projection.Transport?.Kind);
            Assert.Equal(1, projection.Identity.GenericArity);
            Assert.Equal(ClrTypeKind.Reference, projection.Identity.Kind);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void Resolver_ValidatesArityDefaultsNestedContainersAndQualifiedIdentity()
    {
        var (ir, resolver) = LoadCorpus();
        var customEvent = Assert.Single(
            ir.TypescriptSymbols,
            symbol => symbol.Name == "CustomEvent");
        Assert.Equal(1, resolver.GetGenericArity(customEvent.Name));

        var defaulted = resolver.Project(
            new ReferenceTypeNode("CustomEvent", "CustomEvent", []),
            "fixture/defaulted");
        Assert.Equal("ICustomEvent<object>", defaulted.RenderedType);

        var nested = resolver.Project(
            new ReferenceTypeNode(
                "Promise",
                "Promise",
                [
                    new ReferenceTypeNode(
                        "ReadonlyArray",
                        "ReadonlyArray",
                        [
                            new ReferenceTypeNode(
                                "CustomEvent",
                                "CustomEvent",
                                [new KeywordTypeNode("StringKeyword")])
                        ])
                ]),
            "fixture/nested");
        Assert.Equal(
            "ValueTask<ICustomEvent<string>[]>",
            nested.RenderedType);
        Assert.Equal(
            "ValueTask<ICustomEvent<string>[]>",
            nested.CanonicalType);

        var exception = Assert.Throws<TypeProjectionException>(() =>
            resolver.Project(
                new ReferenceTypeNode(
                    "CustomEvent",
                    "CustomEvent",
                    [
                        new KeywordTypeNode("StringKeyword"),
                        new KeywordTypeNode("NumberKeyword")
                    ]),
                "fixture/arity"));
        Assert.Contains("target arity is 1", exception.Message);
        Assert.Equal("fixture/arity", exception.Provenance);

        Assert.Equal(
            "IReadOnlyDictionary<string, double>",
            resolver.Project(
                new ReferenceTypeNode(
                    "ReadonlyMap",
                    null,
                    [
                        new KeywordTypeNode("StringKeyword"),
                        new KeywordTypeNode("NumberKeyword")
                    ]),
                "fixture/map").RenderedType);
        Assert.Equal(
            "IReadOnlySet<string>",
            resolver.Project(
                new ReferenceTypeNode(
                    "ReadonlySet",
                    null,
                    [new KeywordTypeNode("StringKeyword")]),
                "fixture/set").RenderedType);
        Assert.Equal(
            "ValueTask",
            resolver.Project(
                new ReferenceTypeNode(
                    "PromiseLike",
                    null,
                    [new KeywordTypeNode("VoidKeyword")]),
                "fixture/promise-like").RenderedType);
        Assert.Equal(
            "IAsyncEnumerable<string>",
            resolver.Project(
                new ReferenceTypeNode(
                    "AsyncIteratorObject",
                    null,
                    [
                        new KeywordTypeNode("StringKeyword"),
                        new ReferenceTypeNode(
                            "BuiltinIteratorReturn",
                            "BuiltinIteratorReturn",
                            []),
                        new KeywordTypeNode("UnknownKeyword")
                    ]),
                "fixture/async-iterator").RenderedType);
    }

    [Fact]
    public void GenericScopes_AreLexicalShadowingSafeAndRejectNormalizedCollisions()
    {
        var outer = GenericScope.Create(
            [new TypeParameterModel(0, "T", null, null)],
            "Outer");
        var inner = GenericScope.Create(
            [new TypeParameterModel(0, "T", null, null)],
            "Outer/decl[0]/Map",
            outer,
            "!!");
        var resolver = new TypeResolver([]);

        Assert.Equal(
            "!!0",
            resolver.Project(
                new ReferenceTypeNode("T", "Outer.Map.T", []),
                "fixture/inner",
                inner).CanonicalType);
        Assert.Equal("T_1", inner.Parameters[0].CSharpName);
        Assert.Equal(
            "!0",
            resolver.Project(
                new ReferenceTypeNode("T", "Outer.T", []),
                "fixture/outer",
                inner).CanonicalType);

        var collision = Assert.Throws<TypeProjectionException>(() =>
            GenericScope.Create(
                [
                    new TypeParameterModel(0, "T-U", null, null),
                    new TypeParameterModel(1, "T_U", null, null)
                ],
                "Collision"));
        Assert.Contains("duplicate C# name 'T_U'", collision.Message);

        var outOfScope = Assert.Throws<TypeProjectionException>(() =>
            resolver.Project(
                new ReferenceTypeNode("T", "Other.T", []),
                "fixture/out-of-scope",
                inner));
        Assert.Contains("outside the active lexical generic scope", outOfScope.Message);

        var normalizedOuter = GenericScope.Create(
            [new TypeParameterModel(0, "T$U", null, null)],
            "NormalizedOuter");
        var normalizedInner = GenericScope.Create(
            [new TypeParameterModel(0, "T_U", null, null)],
            "NormalizedOuter/Inner",
            normalizedOuter);
        Assert.Equal("T_U", normalizedOuter.Parameters[0].CSharpName);
        Assert.Equal("T_U_1", normalizedInner.Parameters[0].CSharpName);

        var sibling = GenericScope.Create(
            [new TypeParameterModel(0, "T", null, null)],
            "Outer/Sibling",
            outer);
        Assert.Equal("T_1", sibling.Parameters[0].CSharpName);
        Assert.Equal("T_1", inner.Parameters[0].CSharpName);
    }

    [Fact]
    public void Resolver_DefaultsUseTargetIdentity_AndQualifiedBuiltInsRemainNominal()
    {
        var foo = MakeGenericInterfaceSymbol(
            "Foo",
            [
                new TypeParameterModel(0, "T", null, null),
                new TypeParameterModel(
                    1,
                    "U",
                    null,
                    new ReferenceTypeNode("T", "Foo.T", [])),
                new TypeParameterModel(
                    2,
                    "V",
                    null,
                    new ReferenceTypeNode(
                        "Array",
                        "Array",
                        [new ReferenceTypeNode("U", "Foo.U", [])]))
            ]);
        var collisions = new[]
        {
            MakeGenericInterfaceSymbol(
                "Namespace.Map",
                [
                    new TypeParameterModel(0, "K", null, null),
                    new TypeParameterModel(1, "V", null, null),
                ]),
            MakeGenericInterfaceSymbol(
                "Namespace.Set",
                [new TypeParameterModel(0, "T", null, null)]),
            MakeGenericInterfaceSymbol(
                "Namespace.Readonly",
                [new TypeParameterModel(0, "T", null, null)]),
            MakeGenericInterfaceSymbol(
                "Namespace.Array",
                [new TypeParameterModel(0, "T", null, null)]),
        };
        var resolver = new TypeResolver([foo, .. collisions]);

        Assert.Equal(
            "IFoo<string, string, string[]>",
            resolver.Project(
                new ReferenceTypeNode(
                    "Foo",
                    "Foo",
                    [new KeywordTypeNode("StringKeyword")]),
                "Caller/Foo").RenderedType);

        Assert.Equal(
            [
                "global::Blazor.DOM.Namespaces.Namespace.IMap<string, double>",
                "global::Blazor.DOM.Namespaces.Namespace.ISet<string>",
                "global::Blazor.DOM.Namespaces.Namespace.IReadonly<string>",
                "global::Blazor.DOM.Namespaces.Namespace.IArray<string>",
            ],
            collisions.Select(symbol =>
            {
                var simpleName = symbol.Name[(symbol.Name.LastIndexOf('.') + 1)..];
                var arguments = simpleName == "Map"
                    ? new TypeNode[]
                    {
                        new KeywordTypeNode("StringKeyword"),
                        new KeywordTypeNode("NumberKeyword"),
                    }
                    : [new KeywordTypeNode("StringKeyword")];
                return resolver.Project(
                    new ReferenceTypeNode(simpleName, symbol.Name, arguments),
                    $"Caller/{symbol.Name}").RenderedType;
            }));

        var cycle = MakeGenericInterfaceSymbol(
            "Cycle",
            [
                new TypeParameterModel(
                    0,
                    "T",
                    null,
                    new ReferenceTypeNode("U", "Cycle.U", [])),
                new TypeParameterModel(
                    1,
                    "U",
                    null,
                    new ReferenceTypeNode("T", "Cycle.T", [])),
            ]);
        var cycleError = Assert.Throws<GenericDeferralException>(() =>
            new TypeResolver([cycle]).Project(
                new ReferenceTypeNode("Cycle", "Cycle", []),
                "Caller/Cycle"));
        Assert.Equal("generic-defaults", cycleError.Phase);
        Assert.Equal("Caller/Cycle/defaultTypeArgument[0]", cycleError.Provenance);
    }

    [Fact]
    public void Constraints_DefaultsAndOverloadIdentity_FailClosed()
    {
        var baseType = MakeInterfaceSymbol("BaseType", []);
        var otherType = MakeInterfaceSymbol("OtherType", []);
        var target = MakeInterfaceSymbol(
            "Target",
            [
                GenericMethod(0, "Map", "BaseType"),
                GenericMethod(1, "Map", "OtherType")
            ]);
        var resolver = new TypeResolver([baseType, otherType, target]);

        var collision = Assert.Throws<InterfaceEmitException>(() =>
            new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM").Emit(target));
        Assert.Contains("incompatible generic constraints", collision.Message);

        var unsupportedConstraint = Assert.Throws<GenericDeferralException>(() =>
            resolver.CreateGenericDeclaration(
                [
                    new TypeParameterModel(
                        0,
                        "T",
                        new OperatorTypeNode(
                            "keyof",
                            new ReferenceTypeNode("BaseType", "BaseType", [])),
                        null)
                ],
                "Target/Map"));
        Assert.Equal(
            "advanced-generic-constraints",
            unsupportedConstraint.Phase);

        var unsupportedDefault = Assert.Throws<GenericDeferralException>(() =>
            resolver.CreateGenericDeclaration(
                [
                    new TypeParameterModel(
                        0,
                        "T",
                        null,
                        new UnknownTypeNode("conditional"))
                ],
                "Target"));
        Assert.Equal("generic-defaults", unsupportedDefault.Phase);
    }

    [Fact]
    public void IllegalClrGenericArguments_AreDeferredExceptPromiseVoid()
    {
        var box = MakeGenericInterfaceSymbol(
            "Box",
            [new TypeParameterModel(0, "T", null, null)]);
        var resolver = new TypeResolver([box]);

        foreach (var keyword in new[]
                 {
                     "UndefinedKeyword",
                     "NullKeyword",
                     "VoidKeyword",
                 })
        {
            var error = Assert.Throws<GenericDeferralException>(() =>
                resolver.Project(
                    new ReferenceTypeNode(
                        "Box",
                        "Box",
                        [new KeywordTypeNode(keyword)]),
                    $"fixture/Box/{keyword}"));
            Assert.Equal("illegal-clr-generic-arguments", error.Phase);
            Assert.Equal(
                $"fixture/Box/{keyword}/typeArgument[0]",
                error.Provenance);
        }

        Assert.Equal(
            "ValueTask",
            resolver.Project(
                new ReferenceTypeNode(
                    "Promise",
                    "Promise",
                    [new KeywordTypeNode("VoidKeyword")]),
                "fixture/PromiseVoid").RenderedType);
    }

    [Fact]
    public void Constraints_RequireNominalGeneratedInterfaces()
    {
        var valid = MakeInterfaceSymbol("ValidContract", []);
        var structural = MakeInterfaceSymbol("StructuralAlias", []) with
        {
            Declarations =
            [
                MakeInterfaceSymbol("StructuralAlias", []).Declarations[0] with
                {
                    Kind = "typeAlias",
                    Type = new TypeLiteralTypeNode([]),
                }
            ],
            Semantic = MakeInterfaceSymbol("StructuralAlias", []).Semantic with
            {
                Classifications = ["typedef"],
            },
        };
        var resolver = new TypeResolver([valid, structural]);
        var constraints = new TypeNode[]
        {
            new FunctionTypeNode([], [], new KeywordTypeNode("VoidKeyword")),
            new ArrayTypeNode(new KeywordTypeNode("UnknownKeyword")),
            new ReferenceTypeNode(
                "StructuralAlias",
                "StructuralAlias",
                []),
        };

        foreach (var constraint in constraints)
        {
            var error = Assert.Throws<GenericDeferralException>(() =>
                resolver.CreateGenericDeclaration(
                    [new TypeParameterModel(0, "T", constraint, null)],
                    "ConstraintFixture"));
            Assert.Equal("advanced-generic-constraints", error.Phase);
        }

        var declaration = resolver.CreateGenericDeclaration(
            [
                new TypeParameterModel(
                    0,
                    "T",
                    new ReferenceTypeNode(
                        "ValidContract",
                        "ValidContract",
                        []),
                    null)
            ],
            "ConstraintFixture");
        Assert.Equal(["where T : IValidContract"], declaration.ConstraintClauses);
    }

    [Fact]
    public void UnsupportedIteratorTransport_ReadonlyMutation_AndGenericUnionDefer()
    {
        var unsupportedTransport = new TransportModel(
            "unsupported",
            false,
            "IteratorObject<string>",
            false,
            false,
            "Iterator proxy transport is not implemented.");
        var resolver = new TypeResolver([MakeInterfaceSymbol("Mutable", [])]);

        var iteratorError = Assert.Throws<GenericDeferralException>(() =>
            resolver.Project(
                new ReferenceTypeNode(
                    "IteratorObject",
                    "IteratorObject",
                    [new KeywordTypeNode("StringKeyword")])
                {
                    Transport = unsupportedTransport,
                },
                "fixture/iterator"));
        Assert.Equal("iterator-transport", iteratorError.Phase);
        Assert.Equal("fixture/iterator/transport", iteratorError.Provenance);

        var readonlyError = Assert.Throws<GenericDeferralException>(() =>
            resolver.Project(
                new ReferenceTypeNode(
                    "Readonly",
                    "Readonly",
                    [new ReferenceTypeNode("Mutable", "Mutable", [])]),
                "fixture/readonly"));
        Assert.Equal("readonly-mapped-types", readonlyError.Phase);
        Assert.Equal(
            "string",
            resolver.Project(
                new ReferenceTypeNode(
                    "Readonly",
                    "Readonly",
                    [new KeywordTypeNode("StringKeyword")]),
                "fixture/readonly-string").RenderedType);

        var either = MakeGenericAliasSymbol(
            "Either",
            [
                new TypeParameterModel(0, "T", null, null),
                new TypeParameterModel(1, "U", null, null),
            ],
            new UnionTypeNode(
            [
                new ReferenceTypeNode("T", "Either.T", []),
                new ReferenceTypeNode("U", "Either.U", []),
            ]));
        var unionError = Assert.Throws<GenericDeferralException>(() =>
            new AliasEmitter(
                new TypeResolver([either]),
                "1.0.0",
                "Blazor.DOM").Emit(either));
        Assert.Equal("typed-union", unionError.Phase);
        Assert.Equal("Either/typeAlias", unionError.Provenance);
    }

    [Fact]
    public void GenericArtifacts_AreByteIdenticalAcrossRecursiveTwoPassGeneration()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var ir = IrLoader.Load(data);
        var overrides = EmitterOverridesLoader.Load(data);
        var first = CreateTempDirectory();
        var second = CreateTempDirectory();
        try
        {
            var run1 = GenerationPipeline.Run(ir, first, overrides);
            var run2 = GenerationPipeline.Run(ir, second, overrides);
            Assert.True(run1.Validation.IsValid);
            Assert.True(run2.Validation.IsValid);

            var genericPaths = new[]
            {
                Path.Combine("Interfaces", "IHTMLCollectionOf.g.cs"),
                Path.Combine("Interfaces", "INode.g.cs"),
                Path.Combine("Callbacks", "LockGrantedCallback.g.cs"),
                Path.Combine("Dictionaries", "QueuingStrategy.g.cs"),
                Path.Combine("Factories", "ICustomEventFactory.g.cs"),
                Path.Combine("Globals", "IWindow.Globals.g.cs"),
            };
            foreach (var path in genericPaths)
            {
                var left = File.ReadAllBytes(Path.Combine(first, path));
                var right = File.ReadAllBytes(Path.Combine(second, path));
                Assert.Equal(left, right);
                Assert.Equal(
                    Convert.ToHexString(SHA256.HashData(left)),
                    Convert.ToHexString(SHA256.HashData(right)));
                Assert.DoesNotContain((byte)'\r', left);
            }

        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }

    [Fact]
    public void Corpus_GenericContractsProfile_IsFailureFreeAndByteIdentical()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var profile = ProfileLoader.Load(Path.Combine(
            root,
            "data",
            "Blazor.DOM.Profiles",
            "GenericContracts.profile.json"));
        var output = CreateTempDirectory();
        try
        {
            var result = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data));
            Assert.True(result.Coverage.ByteIdentityVerified);
            Assert.True(result.PipelineResult.Validation.IsValid);
            Assert.Empty(result.PipelineResult.Errors);
            Assert.Equal(0, result.PipelineResult.Manifest.Accounting.GenerationFailed);
            Assert.Equal((19, 18, 1), (
                result.ClosureSize,
                result.IncludedSymbolCount,
                result.ExternalReferenceCount));
            Assert.Equal(17, result.PipelineResult.Manifest.Accounting.Projected);
            Assert.Equal(1, result.PipelineResult.Manifest.Accounting.Deferred);
            Assert.DoesNotContain(
                result.Coverage.ExternalReferences,
                reference => reference is "T" or "K"
                    || reference.EndsWith(".T", StringComparison.Ordinal));
            Assert.Contains(
                result.PipelineResult.WrittenFiles,
                file => file.RelativePath == Path.Combine(
                    "Interfaces",
                    "ILockManager.g.cs"));
            Assert.Contains(
                result.PipelineResult.WrittenFiles,
                file => file.RelativePath == Path.Combine(
                    "Callbacks",
                    "LockGrantedCallback.g.cs"));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static MemberModel GenericMethod(
        int ordinal,
        string name,
        string constraint)
        => new(
            ordinal,
            "method",
            new NameNode("identifier", name),
            false,
            false,
            false,
            [
                new TypeParameterModel(
                    0,
                    "T",
                    new ReferenceTypeNode(constraint, constraint, []),
                    null)
            ],
            [
                new ParameterModel(
                    0,
                    "value",
                    false,
                    false,
                    new ReferenceTypeNode("T", $"Target.{name}.T", []),
                    null,
                    EmptyDocumentation,
                    EmptyLocation)
            ],
            null,
            new ReferenceTypeNode("T", $"Target.{name}.T", []),
            EmptyDocumentation,
            EmptyLocation);

    private static SymbolModel MakeInterfaceSymbol(
        string name,
        IReadOnlyList<MemberModel> members)
        => new(
            0,
            name,
            0,
            [
                new DeclarationModel(
                    0,
                    "interface",
                    name,
                    [],
                    [],
                    [],
                    members,
                    null,
                    [],
                    null,
                    EmptyDocumentation,
                    EmptyLocation,
                    null,
                    false,
                    new EventMapModel(false, []),
                    [])
            ],
            false,
            new SemanticModel(
                "matched",
                name,
                null,
                null,
                ["interface"],
                [],
                ["Window"],
                true,
                false,
                [],
                false,
                false,
                false,
                [],
                []));

    private static SymbolModel MakeGenericInterfaceSymbol(
        string name,
        IReadOnlyList<TypeParameterModel> typeParameters)
    {
        var symbol = MakeInterfaceSymbol(name, []);
        return symbol with
        {
                Declarations =
                [
                    symbol.Declarations[0] with
                    {
                        TypeParameters = typeParameters,
                    }
                ],
        };
    }

    private static SymbolModel MakeGenericAliasSymbol(
        string name,
        IReadOnlyList<TypeParameterModel> typeParameters,
        TypeNode type)
    {
        var symbol = MakeInterfaceSymbol(name, []);
        return symbol with
        {
            Declarations =
            [
                symbol.Declarations[0] with
                {
                    Kind = "typeAlias",
                    TypeParameters = typeParameters,
                    Type = type,
                }
            ],
            Semantic = symbol.Semantic with
            {
                Classifications = ["typedef"],
            },
        };
    }

    private static (IrBundle Ir, TypeResolver Resolver) LoadCorpus()
    {
        var data = Path.Combine(FindRepositoryRoot(), "data", "Blazor.DOM");
        var ir = IrLoader.Load(data);
        return (
            ir,
            new TypeResolver(
                ir.TypescriptSymbols,
                EmitterOverridesLoader.Load(data)));
    }

    private static string Read(string root, params string[] path)
        => File.ReadAllText(Path.Combine([root, .. path]));

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "blazorators.sln")))
                return directory;
            directory = Path.GetDirectoryName(directory);
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "generic-emitter-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static readonly DocumentationModel EmptyDocumentation =
        new("", [], false);
    private static readonly LocationModel EmptyLocation =
        new("fixture.d.ts", new PositionModel(1, 1, 0), new PositionModel(1, 1, 0));
}
