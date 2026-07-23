using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class DeclarationRoutingTests
{
    [Theory]
    [InlineData("AdvancedTypes/WidgetValueStringOrBlobUnion.g.cs")]
    [InlineData("Factories/IWidgetFactory.g.cs")]
    [InlineData("Globals/IWindow.Globals.g.cs")]
    [InlineData("Namespaces/WebAssembly/IWebAssemblyNamespace.g.cs")]
    public void SupplementalDirectories_AreTransactionallyOwned(string path)
        => Assert.True(OutputPromotion.IsExhaustiveOwnedPath(path));

    [Fact]
    public void Router_UsesTypeScriptShapeForEverySupportedDeclarationKind()
    {
        var symbols = new[]
        {
            MakeSymbol("InterfaceShape", 0, "interface", InterfaceDeclaration(
                "InterfaceShape"), "interface"),
            MakeSymbol("DictionaryShape", 1, "interface", InterfaceDeclaration(
                "DictionaryShape"), "dictionary"),
            MakeSymbol("AliasShape", 2, "typeAlias", TypeAliasDeclaration(
                "AliasShape",
                new KeywordTypeNode("StringKeyword")), "typedef"),
            MakeSymbol("EnumShape", 3, "typeAlias", TypeAliasDeclaration(
                "EnumShape",
                new UnionTypeNode(
                    [new LiteralTypeNode("StringLiteral", "\"value\"")])), "enum"),
            MakeSymbol("CallbackShape", 4, "typeAlias", TypeAliasDeclaration(
                "CallbackShape",
                new FunctionTypeNode(
                    [],
                    [],
                    new KeywordTypeNode("VoidKeyword"))), "callback"),
            MakeSymbol("globalValue", 5, "globalVariable", GlobalVariableDeclaration(
                "globalValue",
                new KeywordTypeNode("StringKeyword")), "interface"),
            MakeSymbol("globalCall", 6, "globalFunction", GlobalFunctionDeclaration(
                "globalCall",
                [],
                new KeywordTypeNode("VoidKeyword")), "interface"),
            MakeSymbol("Example", 7, "namespace", NamespaceDeclaration(
                "Example",
                []), "namespace"),
            MakeSymbol("ConstructorShape", 8, "globalVariable",
                GlobalVariableDeclaration(
                    "ConstructorShape",
                    new TypeLiteralTypeNode(
                    [
                        MakeMember(
                            0,
                            "constructSignature",
                            null,
                            returnType: new ReferenceTypeNode(
                                "ConstructorShape",
                                "ConstructorShape",
                                [])),
                    ]),
                    constructorObject: true),
                "interface"),
        };

        var plan = DeclarationRouter.Create(symbols);
        var routes = plan.Symbols.ToDictionary(
            symbol => symbol.Symbol.Name,
            symbol => Assert.Single(symbol.Declarations).Route,
            StringComparer.Ordinal);

        Assert.Equal(DeclarationRouteKind.Interface, routes["InterfaceShape"]);
        Assert.Equal(DeclarationRouteKind.Dictionary, routes["DictionaryShape"]);
        Assert.Equal(DeclarationRouteKind.Typedef, routes["AliasShape"]);
        Assert.Equal(DeclarationRouteKind.Enum, routes["EnumShape"]);
        Assert.Equal(DeclarationRouteKind.Callback, routes["CallbackShape"]);
        Assert.Equal(DeclarationRouteKind.GlobalVariable, routes["globalValue"]);
        Assert.Equal(DeclarationRouteKind.GlobalFunction, routes["globalCall"]);
        Assert.Equal(DeclarationRouteKind.Namespace, routes["Example"]);
        Assert.Equal(
            DeclarationRouteKind.FactoryConstructor,
            routes["ConstructorShape"]);
    }

    [Fact]
    public void Router_FailsClosedForGlobalVariableWithoutDeclaredType()
    {
        var symbol = MakeSymbol(
            "missingType",
            0,
            "globalVariable",
            Declaration(0, "globalVariable", "missingType"),
            "interface");

        var routing = DeclarationRouter.Create([symbol]).Get(symbol);

        Assert.Empty(routing.Declarations);
        Assert.Contains("does not declare a type", routing.FailureReason);
    }

    [Fact]
    public void WindowAliases_MergeOverloadsDocsDeprecationAndSetterSemantics()
    {
        var window = MakeSymbol(
            "Window",
            0,
            "interface",
            InterfaceDeclaration(
                "Window",
                [
                    MakeMember(
                        0,
                        "property",
                        "mutableValue",
                        type: new KeywordTypeNode("StringKeyword"),
                        readOnly: true,
                        documentation: Documentation("Window property.")),
                    MakeMember(
                        1,
                        "method",
                        "notify",
                        parameters:
                        [
                            Parameter(
                                0,
                                "message",
                                new KeywordTypeNode("StringKeyword")),
                        ],
                        returnType: new KeywordTypeNode("VoidKeyword"),
                        documentation: Documentation("Window method.")),
                    MakeMember(
                        2,
                        "method",
                        "notify",
                        parameters:
                        [
                            Parameter(
                                0,
                                "count",
                                new KeywordTypeNode("NumberKeyword")),
                        ],
                        returnType: new KeywordTypeNode("VoidKeyword"),
                        documentation: Documentation("Window method.")),
                ]),
            "interface");
        var globalProperty = MakeGlobalAlias(
            "mutableValue",
            1,
            GlobalVariableDeclaration(
                "mutableValue",
                new KeywordTypeNode("StringKeyword"),
                variableKind: "let",
                documentation: Documentation("Global property.", deprecated: true)),
            "Window");
        var globalFunctions = MakeGlobalAlias(
            "notify",
            2,
            [
                GlobalFunctionDeclaration(
                    "notify",
                    [
                        Parameter(
                            0,
                            "message",
                            new KeywordTypeNode("StringKeyword"),
                            optional: true),
                    ],
                    new KeywordTypeNode("VoidKeyword"),
                    ordinal: 0,
                    documentation: Documentation(
                        "Global method.",
                        deprecated: true)),
                GlobalFunctionDeclaration(
                    "notify",
                    [
                        Parameter(
                            0,
                            "count",
                            new KeywordTypeNode("NumberKeyword")),
                    ],
                    new KeywordTypeNode("VoidKeyword"),
                    ordinal: 1,
                    documentation: Documentation("Global method.")),
            ],
            "Window");
        var output = CreateOutputDirectory();

        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(
                    CreateManifest(),
                    [window, globalProperty, globalFunctions],
                    []),
                output);

            Assert.True(result.Validation.IsValid);
            Assert.Empty(result.Errors);
            Assert.False(File.Exists(Path.Combine(
                output,
                "Globals",
                "IWindow.Globals.g.cs")));

            var source = File.ReadAllText(Path.Combine(
                output,
                "Interfaces",
                "IWindow.g.cs"));
            Assert.Contains("Window property.", source);
            Assert.Contains("Global property.", source);
            Assert.Contains("string MutableValue { get; set; }", source);
            Assert.Contains("Window method.", source);
            Assert.Contains("Global method.", source);
            Assert.Contains("[Obsolete]", source);
            Assert.Contains(
                "void Notify(string? message = default);",
                source);
            Assert.Contains("void Notify(double count);", source);
            Assert.Equal(2, source.Split("void Notify(").Length - 1);

            var aliases = result.Manifest.Accounting.SourceDeclarationEntries!
                .Where(entry => entry.SymbolName is "mutableValue" or "notify")
                .ToList();
            Assert.Equal(3, aliases.Count);
            Assert.All(aliases, entry =>
            {
                Assert.Equal(
                    nameof(MemberOutcomeStatus.Projected),
                    entry.Status);
                Assert.Contains("Merged with canonical", entry.Reason);
            });
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void WindowAlias_IncompatibleCanonicalProperty_FailsClosed()
    {
        var window = MakeSymbol(
            "Window",
            0,
            "interface",
            InterfaceDeclaration(
                "Window",
                [
                    MakeMember(
                        0,
                        "property",
                        "collision",
                        type: new KeywordTypeNode("StringKeyword"),
                        readOnly: true),
                ]),
            "interface");
        var alias = MakeGlobalAlias(
            "collision",
            1,
            GlobalVariableDeclaration(
                "collision",
                new KeywordTypeNode("NumberKeyword"),
                variableKind: "var"),
            "Window");
        var output = CreateOutputDirectory();

        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateManifest(), [window, alias], []),
                output);

            Assert.True(result.Validation.IsValid);
            Assert.Equal(2, result.Manifest.Accounting.GenerationFailed);
            var declaration = Assert.Single(
                result.Manifest.Accounting.SourceDeclarationEntries!,
                entry => entry.SymbolName == "collision");
            Assert.Equal(
                nameof(MemberOutcomeStatus.Failed),
                declaration.Status);
            Assert.Contains(
                "no compatible canonical property",
                declaration.Reason);
            Assert.False(File.Exists(Path.Combine(
                output,
                "Globals",
                "IWindow.Globals.g.cs")));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void QualifiedNamespace_UsesResolvedSymbolsAndDeterministicPaths()
    {
        var namespaceSymbol = MakeSymbol(
            "WebAssembly",
            0,
            "namespace",
            NamespaceDeclaration(
                "WebAssembly",
                ["WebAssembly.Module", "WebAssembly.validate"]),
            "namespace");
        var module = MakeSymbol(
            "WebAssembly.Module",
            1,
            "interface",
            InterfaceDeclaration("Module"),
            "interface");
        var validate = MakeSymbol(
            "WebAssembly.validate",
            2,
            "globalFunction",
            GlobalFunctionDeclaration(
                "validate",
                [
                    Parameter(
                        0,
                        "module",
                        new ReferenceTypeNode(
                            "Module",
                            "WebAssembly.Module",
                            [])),
                ],
                new KeywordTypeNode("BooleanKeyword")),
            "namespace");
        var runtimeNamespace = MakeSymbol(
            "WebAssembly.Runtime",
            3,
            "namespace",
            NamespaceDeclaration(
                "Runtime",
                ["WebAssembly.Runtime.version"]),
            "namespace");
        var runtimeVersion = MakeSymbol(
            "WebAssembly.Runtime.version",
            4,
            "globalFunction",
            GlobalFunctionDeclaration(
                "version",
                [],
                new KeywordTypeNode("StringKeyword")),
            "namespace");
        var systemNamespace = MakeSymbol(
            "System",
            5,
            "namespace",
            NamespaceDeclaration("System", []),
            "namespace");
        var output = CreateOutputDirectory();

        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(
                    CreateManifest(),
                    [
                        namespaceSymbol,
                        module,
                        validate,
                        runtimeNamespace,
                        runtimeVersion,
                        systemNamespace,
                    ],
                    []),
                output);

            Assert.Empty(result.Errors);
            Assert.Contains(
                result.WrittenFiles,
                file => file.RelativePath == Path.Combine(
                    "Interfaces",
                    "Namespaces",
                    "WebAssembly",
                    "IModule.g.cs"));
            Assert.Contains(
                result.WrittenFiles,
                file => file.RelativePath == Path.Combine(
                    "Namespaces",
                    "WebAssembly",
                    "IWebAssemblyNamespace.g.cs"));
            var contract = File.ReadAllText(Path.Combine(
                output,
                "Namespaces",
                "WebAssembly",
                "IWebAssemblyNamespace.g.cs"));
            Assert.Contains(
                "bool Validate(" +
                "global::Blazor.DOM.Namespaces.WebAssembly.IModule module);",
                contract);
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.WebAssembly.Runtime." +
                "IRuntimeNamespace Runtime { get; }",
                contract);
            var runtimeContract = File.ReadAllText(Path.Combine(
                output,
                "Namespaces",
                "WebAssembly",
                "Runtime",
                "IRuntimeNamespace.g.cs"));
            Assert.Contains("string Version();", runtimeContract);
            Assert.Contains(
                result.WrittenFiles,
                file => file.RelativePath == Path.Combine(
                    "Namespaces",
                    "TypeScriptSystem",
                    "ISystemNamespace.g.cs"));
            var windowContract = File.ReadAllText(Path.Combine(
                output,
                "Globals",
                "IWindow.Globals.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.WebAssembly.IWebAssemblyNamespace " +
                "WebAssembly { get; }",
                windowContract);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ConstructorObject_EmitsRepresentableFactoryAndDefersUnsupportedMember()
    {
        var widget = MakeSymbol(
            "Widget",
            0,
            [
                InterfaceDeclaration("Widget"),
                GlobalVariableDeclaration(
                    "Widget",
                    new TypeLiteralTypeNode(
                    [
                        MakeMember(
                            0,
                            "property",
                            "prototype",
                            type: new ReferenceTypeNode(
                                "Widget",
                                "Widget",
                                []),
                            readOnly: true),
                        MakeMember(
                            1,
                            "constructSignature",
                            null,
                            parameters:
                            [
                                Parameter(
                                    0,
                                    "name",
                                    new KeywordTypeNode("StringKeyword"),
                                    optional: true),
                            ],
                            returnType: new ReferenceTypeNode(
                                "Widget",
                                "Widget",
                                [])),
                        MakeMember(
                            2,
                            "method",
                            "fromName",
                            parameters:
                            [
                                Parameter(
                                    0,
                                    "name",
                                    new KeywordTypeNode("StringKeyword")),
                            ],
                            returnType: new ReferenceTypeNode(
                                "Widget",
                                "Widget",
                                [])),
                        MakeMember(3, "indexSignature", null),
                    ]),
                    ordinal: 1,
                    constructorObject: true),
            ],
            "interface");
        var output = CreateOutputDirectory();

        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateManifest(), [widget], []),
                output);

            Assert.Empty(result.Errors);
            var factoryPath = Path.Combine(
                output,
                "Factories",
                "IWidgetFactory.g.cs");
            var factory = File.ReadAllText(factoryPath);
            Assert.Contains("IWidget Prototype { get; }", factory);
            Assert.Contains(
                "IWidget Create(string? name = default);",
                factory);
            Assert.Contains("IWidget FromName(string name);", factory);
            Assert.DoesNotContain("indexSignature", factory);

            var declaration = Assert.Single(
                result.Manifest.Accounting.SourceDeclarationEntries!,
                entry => entry.SymbolName == "Widget"
                    && entry.Kind == "globalVariable");
            Assert.Equal(
                nameof(MemberOutcomeStatus.Deferred),
                declaration.Status);
            Assert.Equal("factory-constructor", declaration.Phase);
            var members = result.Manifest.Accounting.SourceMemberEntries!
                .Where(entry => entry.SymbolName == "Widget")
                .ToList();
            Assert.Equal(4, members.Count);
            Assert.Equal(
                3,
                members.Count(entry =>
                    entry.Status == nameof(MemberOutcomeStatus.Projected)));
            var deferred = Assert.Single(
                members,
                entry => entry.Status == nameof(MemberOutcomeStatus.Deferred));
            Assert.Equal("factory-constructor", deferred.Phase);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ConstructorAlias_ReusesCanonicalFactoryWithoutDeferral()
    {
        var widget = MakeSymbol(
            "Widget",
            0,
            [
                InterfaceDeclaration("Widget"),
                GlobalVariableDeclaration(
                    "Widget",
                    new TypeLiteralTypeNode(
                    [
                        MakeMember(
                            0,
                            "property",
                            "prototype",
                            type: new ReferenceTypeNode("Widget", "Widget", []),
                            readOnly: true),
                        MakeMember(
                            1,
                            "constructSignature",
                            null,
                            returnType: new ReferenceTypeNode(
                                "Widget",
                                "Widget",
                                [])),
                    ]),
                    ordinal: 1,
                    constructorObject: true),
            ],
            "interface");
        var legacyWidget = MakeSymbol(
            "LegacyWidget",
            1,
            [
                TypeAliasDeclaration(
                    "LegacyWidget",
                    new ReferenceTypeNode("Widget", "Widget", [])),
                GlobalVariableDeclaration(
                    "LegacyWidget",
                    new QueryTypeNode(
                        null,
                        ExpressionName: "Widget",
                        ResolvedSymbol: "Widget",
                        TypeArguments: []),
                    ordinal: 1,
                    constructorObject: true),
            ],
            "interface");
        var output = CreateOutputDirectory();

        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateManifest(), [widget, legacyWidget], []),
                output);

            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Manifest.Accounting.Projected);
            Assert.Equal(2, result.Manifest.Accounting.ProjectedClean);
            Assert.Equal(0, result.Manifest.Accounting.ProjectedWithDeferredMembers);
            Assert.Equal(0, result.Manifest.Accounting.Deferred);
            Assert.Equal(0, result.Manifest.Accounting.GenerationFailed);
            var aliasDeclaration = Assert.Single(
                result.Manifest.Accounting.SourceDeclarationEntries!,
                entry => entry.SymbolName == "LegacyWidget"
                    && entry.Kind == "globalVariable");
            Assert.Equal(
                nameof(MemberOutcomeStatus.Projected),
                aliasDeclaration.Status);
            Assert.Null(aliasDeclaration.Phase);

            var window = File.ReadAllText(Path.Combine(
                output,
                "Globals",
                "IWindow.Globals.g.cs"));
            Assert.Contains(
                "[global::Microsoft.JSInterop.DomGlobalAlias(\"LegacyWidget\")]",
                window);
            Assert.Contains(
                "IWidgetFactory LegacyWidgetConstructor { get; }",
                window);
            Assert.False(File.Exists(Path.Combine(
                output,
                "Factories",
                "ILegacyWidgetFactory.g.cs")));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static SymbolModel MakeSymbol(
        string name,
        int ordinal,
        string declarationKind,
        DeclarationModel declaration,
        string classification)
        => MakeSymbol(
            name,
            ordinal,
            [declaration with { Kind = declarationKind }],
            classification);

    private static SymbolModel MakeSymbol(
        string name,
        int ordinal,
        IReadOnlyList<DeclarationModel> declarations,
        string classification,
        string bindingKind = "definition",
        string? webIdlName = null,
        string? webIdlMemberName = null)
        => new(
            ordinal,
            name,
            0,
            declarations,
            declarations.Count > 1,
            Semantic(
                classification,
                bindingKind,
                webIdlName ?? name,
                webIdlMemberName));

    private static SymbolModel MakeGlobalAlias(
        string name,
        int ordinal,
        DeclarationModel declaration,
        string owner)
        => MakeGlobalAlias(name, ordinal, [declaration], owner);

    private static SymbolModel MakeGlobalAlias(
        string name,
        int ordinal,
        IReadOnlyList<DeclarationModel> declarations,
        string owner)
        => MakeSymbol(
            name,
            ordinal,
            declarations,
            "interface",
            "globalMember",
            owner,
            name);

    private static SemanticModel Semantic(
        string classification,
        string bindingKind,
        string webIdlName,
        string? webIdlMemberName)
        => new(
            "matched",
            webIdlName,
            bindingKind,
            webIdlMemberName,
            [classification],
            [],
            ["Window"],
            true,
            false,
            ["Window"],
            false,
            false,
            false,
            [],
            []);

    private static DeclarationModel InterfaceDeclaration(
        string name,
        IReadOnlyList<MemberModel>? members = null,
        int ordinal = 0)
        => Declaration(
            ordinal,
            "interface",
            name,
            members: members ?? []);

    private static DeclarationModel TypeAliasDeclaration(
        string name,
        TypeNode type,
        int ordinal = 0)
        => Declaration(ordinal, "typeAlias", name, type: type);

    private static DeclarationModel GlobalVariableDeclaration(
        string name,
        TypeNode type,
        int ordinal = 0,
        string variableKind = "const",
        bool constructorObject = false,
        DocumentationModel? documentation = null)
        => Declaration(
            ordinal,
            "globalVariable",
            name,
            type: type,
            variableKind: variableKind,
            constructorObject: constructorObject,
            documentation: documentation);

    private static DeclarationModel GlobalFunctionDeclaration(
        string name,
        IReadOnlyList<ParameterModel> parameters,
        TypeNode returnType,
        int ordinal = 0,
        DocumentationModel? documentation = null)
        => Declaration(
            ordinal,
            "globalFunction",
            name,
            parameters: parameters,
            returnType: returnType,
            documentation: documentation);

    private static DeclarationModel NamespaceDeclaration(
        string name,
        IReadOnlyList<string> members,
        int ordinal = 0)
        => Declaration(
            ordinal,
            "namespace",
            name,
            namespaceMembers: members);

    private static DeclarationModel Declaration(
        int ordinal,
        string kind,
        string name,
        IReadOnlyList<MemberModel>? members = null,
        TypeNode? type = null,
        IReadOnlyList<ParameterModel>? parameters = null,
        TypeNode? returnType = null,
        string? variableKind = null,
        bool constructorObject = false,
        DocumentationModel? documentation = null,
        IReadOnlyList<string>? namespaceMembers = null)
        => new(
            ordinal,
            kind,
            name,
            [],
            [],
            [],
            members ?? [],
            type,
            parameters ?? [],
            returnType,
            documentation ?? Documentation(),
            Location(ordinal + 1),
            variableKind,
            constructorObject,
            new EventMapModel(false, []),
            namespaceMembers ?? []);

    private static MemberModel MakeMember(
        int ordinal,
        string kind,
        string? name,
        TypeNode? type = null,
        IReadOnlyList<ParameterModel>? parameters = null,
        TypeNode? returnType = null,
        bool readOnly = false,
        DocumentationModel? documentation = null)
        => new(
            ordinal,
            kind,
            name is null ? null : new NameNode("identifier", name),
            false,
            readOnly,
            false,
            [],
            parameters ?? [],
            type,
            returnType,
            documentation ?? Documentation(),
            Location(ordinal + 20));

    private static ParameterModel Parameter(
        int ordinal,
        string name,
        TypeNode type,
        bool optional = false)
        => new(
            ordinal,
            name,
            optional,
            false,
            type,
            null,
            Documentation(),
            Location(ordinal + 40));

    private static DocumentationModel Documentation(
        string text = "",
        bool deprecated = false)
        => new(text, [], deprecated);

    private static LocationModel Location(int line)
        => new(
            "fixture.ts",
            new PositionModel(line, 1, line),
            new PositionModel(line, 10, line + 9));

    private static ManifestModel CreateManifest()
        => new(
            1,
            new GenerationProfileModel("Window", ["Window"], true),
            new ManifestFilesModel(
                new(
                    "typescript-symbols.jsonl",
                    "jsonl",
                    "dummy",
                    0,
                    new string('a', 64)),
                new(
                    "webidl-symbols.jsonl",
                    "jsonl",
                    "dummy",
                    0,
                    new string('b', 64)),
                new(
                    "coverage.json",
                    "json",
                    "dummy",
                    1,
                    new string('c', 64))),
            new ManifestCountsModel(
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new ManifestProvenanceModel(
                new("test", "1.0.0", null),
                new(
                    "typescript",
                    "1.0.0",
                    "MIT",
                    new string('d', 64),
                    []),
                new("webref", "1.0.0", "MIT"),
                new("webidl2", "1.0.0", "MIT"),
                new("fixture", new string('e', 64), 0)));

    private static string CreateOutputDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }
}
