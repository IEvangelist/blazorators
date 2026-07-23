using System.Security.Cryptography;
using Blazor.DOM.CSharpGenerator.Accounting;
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
                "public partial interface IHTMLCollectionOf<T> : IHTMLCollectionBase, global::Microsoft.JSInterop.IDomProxy where T : IElement",
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
            Assert.DoesNotContain(
                result.Manifest.Accounting.DeferredSymbols,
                entry => entry.Symbol == "ReadableStreamController"
                    && entry.Phase == "typed-union");
            Assert.Contains(
                "ICustomEvent<T> Create<T>(string type, CustomEventInit<T>? eventInitDict = default);",
                Read(output, "Factories", "ICustomEventFactory.g.cs"));
            Assert.Contains(
                "T StructuredClone<T>(T @value, StructuredSerializeOptions? options = default);",
                Read(output, "Interfaces", "IWindowOrWorkerGlobalScope.g.cs"));
            Assert.Contains(
                "DomWellKnownSymbol.Iterator",
                Read(output, "Interfaces", "IHeaders.g.cs"));
            var indexedCss = Read(
                output,
                "Interfaces",
                "ICSSStyleDeclaration.g.cs");
            Assert.Contains("DomIndexKeyKind.Number", indexedCss);
            Assert.Contains(
                "string GetIndexedValueByNumber(double index);",
                indexedCss);
            Assert.Contains(
                "void SetIndexedValueByNumber(double index, string value);",
                indexedCss);
            Assert.DoesNotContain(
                result.Manifest.Accounting.DeferredSymbols,
                entry => entry.Symbol == "FormDataIterator"
                    && entry.Phase == "iterator-transport");
            Assert.DoesNotContain(
                result.Errors,
                error => error.Message.Contains(
                    "generic C# emission is deferred",
                    StringComparison.Ordinal));
            Assert.All(
                new[]
                {
                    "WebAssembly.GlobalDescriptor",
                    "OptionalPrefixToken",
                    "OptionalPostfixToken",
                    "WebAssembly.Global",
                },
                symbolName => Assert.DoesNotContain(
                    result.Manifest.Accounting.DeferredSymbols,
                    entry => entry.Symbol == symbolName));
            Assert.Contains(
                result.WrittenFiles,
                file => file.RelativePath.EndsWith(
                    "IGlobalFactory.g.cs",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                result.Manifest.Accounting.DeferredMemberEntries,
                entry => entry.SymbolName == "ReadableStreamBYOBReader"
                    && entry.MemberName == "read");
            var globals = Read(output, "Globals", "IWindow.Globals.g.cs");
            Assert.Contains("DomGlobalAlias(\"toString\")", globals);
            Assert.Contains("GlobalToString", globals);
            Assert.Contains("DomGlobalAlias(\"name\")", globals);
            Assert.Empty(result.Manifest.Accounting.DeferredMemberEntries);
            Assert.DoesNotContain(
                result.Manifest.Accounting.SourceOverloadEntries ?? [],
                entry => entry.Status != nameof(MemberOutcomeStatus.Projected));

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
            "global::Microsoft.JSInterop.IBrowserPromise<ICustomEvent<string>[]>",
            nested.RenderedType);
        Assert.Equal(
            "global::Microsoft.JSInterop.IBrowserPromise<ICustomEvent<string>[]>",
            nested.CanonicalType);
        Assert.Equal(
            "ValueTask<ICustomEvent<string>[]>",
            resolver.Project(
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
                "fixture/method/return").RenderedType);

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
            "global::Microsoft.JSInterop.IReadOnlyBrowserMap<string, double>",
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
            "global::Microsoft.JSInterop.IReadOnlyBrowserSet<string>",
            resolver.Project(
                new ReferenceTypeNode(
                    "ReadonlySet",
                    null,
                    [new KeywordTypeNode("StringKeyword")]),
                "fixture/set").RenderedType);
        var liveArray = resolver.Project(
            new ArrayTypeNode(
                new ReferenceTypeNode(
                    "CustomEvent",
                    "CustomEvent",
                    [new KeywordTypeNode("StringKeyword")]))
            {
                Transport = new TransportModel(
                    "unsupported",
                    false,
                    "CustomEvent<string>[]",
                    false,
                    false,
                    "Collection contains a non-JSON transport."),
            },
            "fixture/live-array");
        Assert.Equal(
            "global::Microsoft.JSInterop.IBrowserArray<ICustomEvent<string>>",
            liveArray.RenderedType);
        Assert.Equal("js-reference", liveArray.Transport?.Kind);
        Assert.Equal(
            "global::Microsoft.JSInterop.IBrowserPromise",
            resolver.Project(
                new ReferenceTypeNode(
                    "PromiseLike",
                    null,
                    [new KeywordTypeNode("VoidKeyword")]),
                "fixture/promise-like").RenderedType);
        Assert.Equal(
            "global::Microsoft.JSInterop.IBrowserAsyncIterator<string, " +
            "global::Microsoft.JSInterop.BrowserUndefined, object>",
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

        var keywordOuter = GenericScope.Create(
            [new TypeParameterModel(0, "value", null, null)],
            "ShadowHost");
        var keywordInner = GenericScope.Create(
            [
                new TypeParameterModel(0, "value", null, null),
                new TypeParameterModel(1, "value_1", null, null),
                new TypeParameterModel(2, "Value", null, null),
                new TypeParameterModel(3, "value-2", null, null),
            ],
            "ShadowHost/Map",
            keywordOuter);
        Assert.Equal("@value", keywordOuter.Parameters[0].CSharpName);
        Assert.Equal(
            ["@value_3", "value_1", "Value", "value_2"],
            keywordInner.Parameters.Select(parameter => parameter.CSharpName));
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

        var keyConstraint = resolver.CreateGenericDeclaration(
                [
                    new TypeParameterModel(
                        0,
                        "T",
                        new OperatorTypeNode(
                            "keyof",
                            new ReferenceTypeNode("BaseType", "BaseType", [])),
                        null)
                ],
                "Target/Map");
        Assert.Equal(
            ["where T : global::Microsoft.JSInterop.ITypeScriptKeyOf<IBaseType>"],
            keyConstraint.ConstraintClauses);

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

        foreach (var (keyword, expected) in new[]
                 {
                     ("UndefinedKeyword", "global::Microsoft.JSInterop.BrowserUndefined"),
                     ("NullKeyword", "global::Microsoft.JSInterop.BrowserNull"),
                 })
        {
            Assert.Equal(
                $"IBox<{expected}>",
                resolver.Project(
                    new ReferenceTypeNode(
                        "Box",
                        "Box",
                        [new KeywordTypeNode(keyword)]),
                    $"fixture/Box/{keyword}").RenderedType);
        }
        var voidError = Assert.Throws<GenericDeferralException>(() =>
            resolver.Project(
                new ReferenceTypeNode(
                    "Box",
                    "Box",
                    [new KeywordTypeNode("VoidKeyword")]),
                "fixture/Box/VoidKeyword"));
        Assert.Equal("illegal-clr-generic-arguments", voidError.Phase);

        Assert.Equal(
            "global::Microsoft.JSInterop.IBrowserPromise",
            resolver.Project(
                new ReferenceTypeNode(
                    "Promise",
                    "Promise",
                    [new KeywordTypeNode("VoidKeyword")]),
                "fixture/PromiseVoid").RenderedType);
        Assert.Equal(
            "ValueTask",
            resolver.Project(
                new ReferenceTypeNode(
                    "Promise",
                    "Promise",
                    [new KeywordTypeNode("VoidKeyword")]),
                "fixture/PromiseVoid/return").RenderedType);
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
            var structuralDeclaration = resolver.CreateGenericDeclaration(
                    [new TypeParameterModel(0, "T", constraint, null)],
                    $"ConstraintFixture/{constraint.Kind}");
            Assert.Single(structuralDeclaration.ConstraintClauses);
            Assert.Contains(
                "AdvancedTypes.ConstraintFixture",
                structuralDeclaration.ConstraintClauses[0]);
            Assert.EndsWith(
                "Constraint",
                structuralDeclaration.ConstraintClauses[0]);
            Assert.DoesNotMatch(
                @"Shape_|_[0-9a-f]{10}",
                structuralDeclaration.ConstraintClauses[0]);
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

        var iterator = resolver.Project(
            new ReferenceTypeNode(
                "IteratorObject",
                "IteratorObject",
                [new KeywordTypeNode("StringKeyword")])
            {
                Transport = unsupportedTransport,
            },
            "fixture/iterator");
        Assert.Equal(
            "global::Microsoft.JSInterop.IBrowserIterator<string, " +
            "global::Microsoft.JSInterop.BrowserUndefined, object>",
            iterator.RenderedType);
        Assert.Equal("js-reference", iterator.Transport?.Kind);

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
        var unionSource = new AliasEmitter(
            new TypeResolver([either]),
            "1.0.0",
            "Blazor.DOM").Emit(either);
        Assert.Contains("public readonly struct Either<T, U>", unionSource);
        Assert.Contains("FromT(T value)", unionSource);
        Assert.Contains("FromU(U value)", unionSource);
        Assert.DoesNotContain("implicit operator", unionSource);
        Assert.DoesNotContain("AsObject", unionSource);
    }

    [Fact]
    public void Readonly_RequiresRecursiveSemanticImmutability()
    {
        var mutable = MakeInterfaceSymbol("Mutable", []);
        var primitiveAlias = MakeGenericAliasSymbol(
            "PrimitiveAlias",
            [],
            new KeywordTypeNode("NumberKeyword"));
        var nestedAlias = MakeGenericAliasSymbol(
            "NestedAlias",
            [],
            new ReferenceTypeNode(
                "PrimitiveAlias",
                "PrimitiveAlias",
                []));
        var qualifiedAlias = MakeGenericAliasSymbol(
            "Namespace.Token",
            [],
            new KeywordTypeNode("StringKeyword"));
        var immutableEnum = MakeGenericAliasSymbol(
            "ImmutableEnum",
            [],
            new UnionTypeNode(
            [
                new LiteralTypeNode("StringLiteral", "\"first\""),
                new LiteralTypeNode("StringLiteral", "\"second\""),
            ]));
        var mutableAlias = MakeGenericAliasSymbol(
            "MutableAlias",
            [],
            new ReferenceTypeNode("Mutable", "Mutable", []));
        var nestedMutableAlias = MakeGenericAliasSymbol(
            "NestedMutableAlias",
            [],
            new ReferenceTypeNode("MutableAlias", "MutableAlias", []));
        var arrayAlias = MakeGenericAliasSymbol(
            "ArrayAlias",
            [],
            new ArrayTypeNode(new KeywordTypeNode("StringKeyword")));
        var dictionaryAlias = MakeGenericAliasSymbol(
            "DictionaryAlias",
            [],
            new ReferenceTypeNode(
                "Record",
                "Record",
                [
                    new KeywordTypeNode("StringKeyword"),
                    new KeywordTypeNode("StringKeyword"),
                ]));
        var cycleA = MakeGenericAliasSymbol(
            "CycleA",
            [],
            new ReferenceTypeNode("CycleB", "CycleB", []));
        var cycleB = MakeGenericAliasSymbol(
            "CycleB",
            [],
            new ReferenceTypeNode("CycleA", "CycleA", []));
        var resolver = new TypeResolver(
        [
            mutable,
            primitiveAlias,
            nestedAlias,
            qualifiedAlias,
            immutableEnum,
            mutableAlias,
            nestedMutableAlias,
            arrayAlias,
            dictionaryAlias,
            cycleA,
            cycleB,
        ]);

        TypeProjection ProjectReadonly(TypeNode target, string name)
            => resolver.Project(
                new ReferenceTypeNode("Readonly", "Readonly", [target]),
                $"fixture/readonly/{name}");

        Assert.Equal(
            "double",
            ProjectReadonly(
                new KeywordTypeNode("NumberKeyword"),
                "number").RenderedType);
        Assert.Equal(
            "string",
            ProjectReadonly(
                new KeywordTypeNode("StringKeyword"),
                "string").RenderedType);
        Assert.Equal(
            "PrimitiveAlias",
            ProjectReadonly(
                new ReferenceTypeNode(
                    "PrimitiveAlias",
                    "PrimitiveAlias",
                    []),
                "primitive-alias").RenderedType);
        Assert.Equal(
            "NestedAlias",
            ProjectReadonly(
                new ReferenceTypeNode("NestedAlias", "NestedAlias", []),
                "nested-alias").RenderedType);
        Assert.Equal(
            "global::Blazor.DOM.Namespaces.Namespace.Token",
            ProjectReadonly(
                new ReferenceTypeNode(
                    "Token",
                    "Namespace.Token",
                    []),
                "qualified-alias").RenderedType);
        Assert.Equal(
            "ImmutableEnum",
            ProjectReadonly(
                new ReferenceTypeNode(
                    "ImmutableEnum",
                    "ImmutableEnum",
                    []),
                "enum").RenderedType);

        var mutableTargets = new (string Name, TypeNode Type)[]
        {
            ("interface", new ReferenceTypeNode("Mutable", "Mutable", [])),
            ("mutable-alias", new ReferenceTypeNode(
                "MutableAlias",
                "MutableAlias",
                [])),
            ("nested-mutable-alias", new ReferenceTypeNode(
                "NestedMutableAlias",
                "NestedMutableAlias",
                [])),
            ("array", new ArrayTypeNode(new KeywordTypeNode("StringKeyword"))),
            ("array-alias", new ReferenceTypeNode("ArrayAlias", "ArrayAlias", [])),
            ("dictionary-alias", new ReferenceTypeNode(
                "DictionaryAlias",
                "DictionaryAlias",
                [])),
            ("cycle", new ReferenceTypeNode("CycleA", "CycleA", [])),
            ("object", new KeywordTypeNode("ObjectKeyword")),
        };
        foreach (var (name, type) in mutableTargets)
        {
            var error = Assert.Throws<GenericDeferralException>(() =>
                ProjectReadonly(type, name));
            Assert.Equal("readonly-mapped-types", error.Phase);
            Assert.Equal($"fixture/readonly/{name}", error.Provenance);
        }
    }

    [Fact]
    public void GenericMethodDefaults_EmitDeterministicExpandedOverloads()
    {
        var method = MakeMethod(
            0,
            "Select",
            [
                new TypeParameterModel(
                    0,
                    "T",
                    null,
                    new KeywordTypeNode("StringKeyword")),
                new TypeParameterModel(
                    1,
                    "U",
                    null,
                    new ReferenceTypeNode("T", "Defaults.Select.T", [])),
            ],
            [],
            new ReferenceTypeNode("U", "Defaults.Select.U", []));
        var symbol = MakeInterfaceSymbol("Defaults", [method]);
        var emitted = new InterfaceEmitter(
            new TypeResolver([symbol]),
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Contains("U Select<T, U>();", emitted.Source);
        Assert.Contains("string Select();", emitted.Source);
        Assert.Contains("T Select<T>();", emitted.Source);
        Assert.Contains(
            "// TypeScript generic default: T = string.",
            emitted.Source);
        Assert.Contains(
            "// TypeScript generic default: U = T.",
            emitted.Source);
        Assert.Contains(
            emitted.MemberOutcomes,
            outcome => outcome.Status == MemberOutcomeStatus.Projected
                && outcome.Reason?.Contains(
                    "2 default-expanded overload(s)",
                    StringComparison.Ordinal) == true);
    }

    [Fact]
    public void GenericMethodDefaultCyclesAndCollisions_AreDeferredWithProvenance()
    {
        var cycle = MakeMethod(
            0,
            "Cycle",
            [
                new TypeParameterModel(
                    0,
                    "T",
                    null,
                    new ReferenceTypeNode("U", "CycleHost.Cycle.U", [])),
                new TypeParameterModel(
                    1,
                    "U",
                    null,
                    new ReferenceTypeNode("T", "CycleHost.Cycle.T", [])),
            ],
            [],
            new ReferenceTypeNode("T", "CycleHost.Cycle.T", []));
        var cycleHost = MakeInterfaceSymbol("CycleHost", [cycle]);
        var cycleResult = new InterfaceEmitter(
            new TypeResolver([cycleHost]),
            "1.0.0",
            "Blazor.DOM").Emit(cycleHost);
        var cycleOutcome = Assert.Single(cycleResult.MemberOutcomes);
        Assert.Equal(MemberOutcomeStatus.Deferred, cycleOutcome.Status);
        Assert.Equal("generic-method-defaults", cycleOutcome.Phase);
        Assert.Contains(
            "CycleHost/decl[0]/Cycle/defaultExpansion[0]/typeParameter[0]",
            cycleOutcome.Reason);

        var ordinary = MakeMethod(
            0,
            "Pick",
            [],
            [],
            new KeywordTypeNode("NumberKeyword"));
        var defaulted = MakeMethod(
            1,
            "Pick",
            [
                new TypeParameterModel(
                    0,
                    "T",
                    null,
                    new KeywordTypeNode("StringKeyword")),
            ],
            [],
            new ReferenceTypeNode("T", "CollisionHost.Pick.T", []));
        var collisionHost = MakeInterfaceSymbol(
            "CollisionHost",
            [ordinary, defaulted]);
        var collisionResolver = new TypeResolver([collisionHost]);
        var collisionResult = new InterfaceEmitter(
            collisionResolver,
            "1.0.0",
            "Blazor.DOM").Emit(collisionHost);
        var collisionUnion = Assert.Single(
            collisionResolver.SynthesizedTypes,
            type => type.Kind == "Union");
        Assert.Contains(collisionUnion.Name, collisionResult.Source);
        Assert.EndsWith("Union", collisionUnion.Name);
        Assert.Contains(" Pick();", collisionResult.Source);
        Assert.Contains("T Pick<T>();", collisionResult.Source);
        var collisionOutcome = Assert.Single(
            collisionResult.MemberOutcomes,
            outcome => outcome.Ordinal == 1);
        Assert.Equal(MemberOutcomeStatus.Projected, collisionOutcome.Status);
        Assert.Null(collisionOutcome.Phase);
    }

    [Fact]
    public void DefaultExpandedMethods_AreLegalAndTransactional()
    {
        var voidDefault = new TypeParameterModel(
            0,
            "T",
            null,
            new KeywordTypeNode("VoidKeyword"));
        var use = MakeMethod(
            0,
            "Use",
            [voidDefault],
            [
                new ParameterModel(
                    0,
                    "value",
                    false,
                    false,
                    new ReferenceTypeNode("T", "LegalityHost.Use.T", []),
                    null,
                    EmptyDocumentation,
                    EmptyLocation),
            ],
            new KeywordTypeNode("VoidKeyword"));
        var legalityHost = MakeInterfaceSymbol("LegalityHost", [use]);
        var legalityResult = new InterfaceEmitter(
            new TypeResolver([legalityHost]),
            "1.0.0",
            "Blazor.DOM").Emit(legalityHost);
        var legalityOutcome = Assert.Single(legalityResult.MemberOutcomes);
        Assert.Equal(MemberOutcomeStatus.Deferred, legalityOutcome.Status);
        Assert.Equal("generic-method-defaults", legalityOutcome.Phase);
        Assert.Contains(
            "LegalityHost/decl[0]/Use/parameter[0]/value/defaultExpansion",
            legalityOutcome.Reason);
        Assert.DoesNotContain("void Use<", legalityResult.Source);
        Assert.DoesNotContain("void Use(", legalityResult.Source);

        var nullDefault = new TypeParameterModel(
            0,
            "T",
            null,
            new KeywordTypeNode("NullKeyword"));
        var get = MakeMethod(
            0,
            "Get",
            [nullDefault],
            [],
            new ReferenceTypeNode("T", "NullHost.Get.T", []));
        var nullHost = MakeInterfaceSymbol("NullHost", [get]);
        var nullResult = new InterfaceEmitter(
            new TypeResolver([nullHost]),
            "1.0.0",
            "Blazor.DOM").Emit(nullHost);
        var nullOutcome = Assert.Single(nullResult.MemberOutcomes);
        Assert.Equal(MemberOutcomeStatus.Projected, nullOutcome.Status);
        Assert.Null(nullOutcome.Phase);
        Assert.Contains("T Get<T>();", nullResult.Source);
        Assert.Contains(
            "global::Microsoft.JSInterop.BrowserNull Get();",
            nullResult.Source);

        var ordinary = MakeMethod(
            0,
            "Choose",
            [new TypeParameterModel(0, "T", null, null)],
            [],
            new KeywordTypeNode("NumberKeyword"));
        var partiallyColliding = MakeMethod(
            1,
            "Choose",
            [
                new TypeParameterModel(
                    0,
                    "T",
                    null,
                    new KeywordTypeNode("StringKeyword")),
                new TypeParameterModel(
                    1,
                    "U",
                    null,
                    new ReferenceTypeNode("T", "OrderingHost.Choose.T", [])),
            ],
            [],
            new ReferenceTypeNode("U", "OrderingHost.Choose.U", []));
        var orderingHost = MakeInterfaceSymbol(
            "OrderingHost",
            [ordinary, partiallyColliding]);
        var orderingResolver = new TypeResolver([orderingHost]);
        var orderingResult = new InterfaceEmitter(
            orderingResolver,
            "1.0.0",
            "Blazor.DOM").Emit(orderingHost);
        var orderingUnion = Assert.Single(
            orderingResolver.SynthesizedTypes,
            type => type.Kind == "Union");
        Assert.Contains(orderingUnion.Name, orderingResult.Source);
        Assert.EndsWith("Union", orderingUnion.Name);
        Assert.Contains(" Choose<T>();", orderingResult.Source);
        Assert.Contains("U Choose<T, U>();", orderingResult.Source);
        Assert.Contains("string Choose();", orderingResult.Source);
        var orderingOutcome = Assert.Single(
            orderingResult.MemberOutcomes,
            outcome => outcome.Ordinal == 1);
        Assert.Equal(MemberOutcomeStatus.Projected, orderingOutcome.Status);
        Assert.Null(orderingOutcome.Phase);

        var legalVoid = MakeInterfaceSymbol(
            "VoidHost",
            [
                MakeMethod(
                    0,
                    "Complete",
                    [],
                    [],
                    new KeywordTypeNode("VoidKeyword")),
            ]);
        Assert.Contains(
            "void Complete();",
            new InterfaceEmitter(
                new TypeResolver([legalVoid]),
                "1.0.0",
                "Blazor.DOM").Emit(legalVoid).Source);
    }

    [Fact]
    public void RestArrayDedup_PrefersParamsRegardlessOfOrderAndOptionality()
    {
        foreach (var ordinaryOptional in new[] { false, true })
        {
            foreach (var restFirst in new[] { false, true })
            {
                var ordinary = MakeMethod(
                    restFirst ? 1 : 0,
                    "Merge",
                    [],
                    [
                        new ParameterModel(
                            0,
                            "values",
                            ordinaryOptional,
                            false,
                            new ArrayTypeNode(
                                new KeywordTypeNode("StringKeyword")),
                            null,
                            EmptyDocumentation,
                            EmptyLocation),
                    ],
                    new KeywordTypeNode("VoidKeyword"));
                var rest = MakeMethod(
                    restFirst ? 0 : 1,
                    "Merge",
                    [],
                    [
                        new ParameterModel(
                            0,
                            "values",
                            false,
                            true,
                            new ArrayTypeNode(
                                new KeywordTypeNode("StringKeyword")),
                            null,
                            EmptyDocumentation,
                            EmptyLocation),
                    ],
                    new KeywordTypeNode("VoidKeyword"));
                var symbol = MakeInterfaceSymbol(
                    $"Rest{ordinaryOptional}{restFirst}",
                    restFirst ? [rest, ordinary] : [ordinary, rest]);
                var result = new InterfaceEmitter(
                    new TypeResolver([symbol]),
                    "1.0.0",
                    "Blazor.DOM").Emit(symbol);

                Assert.Contains("void Merge(params string[] values);", result.Source);
                Assert.Equal(
                    1,
                    result.Source.Split(
                        " Merge(",
                        StringSplitOptions.None).Length - 1);
                Assert.All(
                    result.MemberOutcomes,
                    outcome => Assert.Equal(
                        MemberOutcomeStatus.Projected,
                        outcome.Status));
            }
        }
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
            Assert.Equal(18, result.PipelineResult.Manifest.Accounting.Projected);
            Assert.Equal(0, result.PipelineResult.Manifest.Accounting.Deferred);
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

    private static MemberModel MakeMethod(
        int ordinal,
        string name,
        IReadOnlyList<TypeParameterModel> typeParameters,
        IReadOnlyList<ParameterModel> parameters,
        TypeNode returnType)
        => new(
            ordinal,
            "method",
            new NameNode("identifier", name),
            false,
            false,
            false,
            typeParameters,
            parameters,
            null,
            returnType,
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
