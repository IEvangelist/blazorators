using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Profiles;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class ProfileClosureRoutingTests
{
    [Fact]
    public void Resolve_RoutingDependencies_AreExactAndDeterministic()
    {
        var webAssembly = WebAssemblyFixture();
        var webAssemblyIndex = Index(webAssembly);

        foreach (var root in new[] { "WebAssembly", "WebAssembly.instantiate" })
        {
            var closure = TransitiveDependencyResolver.Resolve(
                [root],
                webAssemblyIndex);

            Assert.Equal(
                [
                    "Promise",
                    "WebAssembly",
                    "WebAssembly.CompileError",
                    "WebAssembly.Imports",
                    "WebAssembly.Instance",
                    "WebAssembly.Module",
                    "WebAssembly.instantiate",
                ],
                closure.OrderBy(name => name, StringComparer.Ordinal));
            Assert.DoesNotContain("Imports", closure);
            Assert.DoesNotContain("Instance", closure);
            Assert.DoesNotContain("Module", closure);
        }

        var css = CssFixture();
        Assert.Equal(
            ["CSS", "CSS.escape", "CSS.supports"],
            TransitiveDependencyResolver.Resolve(
                    ["CSS.escape"],
                    Index(css))
                .OrderBy(name => name, StringComparer.Ordinal));

        var window = WindowFixture();
        Assert.Equal(
            ["Window", "innerWidth"],
            TransitiveDependencyResolver.Resolve(
                    ["innerWidth"],
                    Index(window))
                .OrderBy(name => name, StringComparer.Ordinal));

        var nested = NestedNamespaceFixture();
        var first = TransitiveDependencyResolver.Resolve(
            ["Outer.Inner.create"],
            Index(nested));
        var reversed = TransitiveDependencyResolver.Resolve(
            ["Outer.Inner.create"],
            nested.Reverse().ToDictionary(
                symbol => symbol.Name,
                StringComparer.Ordinal));
        Assert.Equal(
            [
                "Outer",
                "Outer.Inner",
                "Outer.Inner.Dependency",
                "Outer.Inner.Widget",
                "Outer.Inner.create",
            ],
            first.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            first.OrderBy(name => name, StringComparer.Ordinal),
            reversed.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Corpus_RepresentativeClosures_UseRoutedAndQualifiedIdentities()
    {
        var ir = IrLoader.Load(Path.Combine(
            FindRepositoryRoot(),
            "data",
            "Blazor.DOM"));
        var index = Index(ir.TypescriptSymbols);

        var webAssembly = TransitiveDependencyResolver.Resolve(
            ["WebAssembly"],
            index);
        var instantiate = TransitiveDependencyResolver.Resolve(
            ["WebAssembly.instantiate"],
            index);
        Assert.Equal(
            webAssembly.OrderBy(name => name, StringComparer.Ordinal),
            instantiate.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(1442, instantiate.Count);
        Assert.Contains("WebAssembly.CompileError", webAssembly);
        Assert.Contains("WebAssembly.Imports", instantiate);
        Assert.Contains("WebAssembly.Module", instantiate);
        Assert.Contains("WebAssembly.Instance", instantiate);
        Assert.DoesNotContain("Imports", instantiate);
        Assert.DoesNotContain("Module", instantiate);
        Assert.DoesNotContain("Instance", instantiate);
        Assert.Equal(30, CountExternal(instantiate, index));

        var cssEscape = TransitiveDependencyResolver.Resolve(
            ["CSS.escape"],
            index);
        Assert.Contains("CSS", cssEscape);
        Assert.Contains("CSS.supports", cssEscape);
        Assert.Equal(1485, cssEscape.Count);
        Assert.Equal(31, CountExternal(cssEscape, index));

        var innerWidth = TransitiveDependencyResolver.Resolve(
            ["innerWidth"],
            index);
        Assert.Contains("Window", innerWidth);
        Assert.Equal(1414, innerWidth.Count);
        Assert.Equal(30, CountExternal(innerWidth, index));
    }

    [Fact]
    public void QualifiedDependency_IsNotSuppressedBySameNamedTypeParameter()
    {
        var root = Symbol(
            "Root",
            0,
            "interface",
            [
                Declaration(
                    "interface",
                    "Root",
                    members:
                    [
                        Member(
                            0,
                            "property",
                            "qualified",
                            Reference("Root.X", "Root.X")),
                        Member(
                            1,
                            "property",
                            "lexical",
                            Reference("X", "Root.X")),
                    ],
                    typeParameters:
                    [
                        new TypeParameterModel(0, "X", null, null),
                    ])
            ]);
        var dependency = Symbol(
            "Root.X",
            1,
            "interface",
            [InterfaceDeclaration("X")]);
        var symbols = new[] { root, dependency };

        Assert.Equal(
            ["Root", "Root.X"],
            TransitiveDependencyResolver.Resolve(["Root"], Index(symbols))
                .OrderBy(name => name, StringComparer.Ordinal));

        var output = CreateOutputDirectory();
        try
        {
            var result = RunProfile(
                "QualifiedTypeParameterCollision",
                "Root",
                symbols,
                output);
            AssertProfileSuccess(result, included: 2, external: []);
            Assert.Contains(
                result.PipelineResult.WrittenFiles,
                file => file.RelativePath.EndsWith(
                    Path.Combine("Root", "IX.g.cs"),
                    StringComparison.Ordinal));
            var rootContract = File.ReadAllText(Path.Combine(
                ProfileRoot(output, "QualifiedTypeParameterCollision"),
                "Interfaces",
                "IRoot.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.Root.IX Qualified { get; set; }",
                rootContract);
            Assert.Contains("X Lexical { get; set; }", rootContract);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData("WebAssembly", "WebAssemblyRoot")]
    [InlineData("WebAssembly.instantiate", "WebAssemblyInstantiate")]
    public void ProfilePipeline_WebAssemblyRoots_GenerateCompleteNamespace(
        string rootSymbol,
        string profileName)
    {
        var output = CreateOutputDirectory();
        try
        {
            var result = RunProfile(
                profileName,
                rootSymbol,
                WebAssemblyFixture(),
                output);

            AssertProfileSuccess(result, included: 6, external: ["Promise"]);
            var profileRoot = ProfileRoot(output, profileName);
            Assert.True(File.Exists(Path.Combine(
                profileRoot,
                "Interfaces",
                "Namespaces",
                "WebAssembly",
                "ICompileError.g.cs")));
            var contract = File.ReadAllText(Path.Combine(
                profileRoot,
                "Namespaces",
                "WebAssembly",
                "IWebAssemblyNamespace.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.WebAssembly.IImports imports",
                contract);
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.WebAssembly.IModule module",
                contract);
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.WebAssembly.IInstance",
                contract);
            var globals = File.ReadAllText(Path.Combine(
                profileRoot,
                "Globals",
                "IWindow.Globals.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.WebAssembly.IWebAssemblyNamespace " +
                "WebAssembly { get; }",
                globals);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ProfilePipeline_CssMember_GeneratesParentAndWindowAccessor()
    {
        const string profileName = "CssEscape";
        var output = CreateOutputDirectory();
        try
        {
            var result = RunProfile(
                profileName,
                "CSS.escape",
                CssFixture(),
                output);

            AssertProfileSuccess(result, included: 3, external: []);
            var profileRoot = ProfileRoot(output, profileName);
            var contract = File.ReadAllText(Path.Combine(
                profileRoot,
                "Namespaces",
                "CSS",
                "ICSSNamespace.g.cs"));
            Assert.Contains("string Escape(string @value);", contract);
            Assert.Contains("bool Supports(string condition);", contract);
            var globals = File.ReadAllText(Path.Combine(
                profileRoot,
                "Globals",
                "IWindow.Globals.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.CSS.ICSSNamespace CSS { get; }",
                globals);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ProfilePipeline_GlobalMember_GeneratesSemanticWindowOwner()
    {
        const string profileName = "InnerWidth";
        var output = CreateOutputDirectory();
        try
        {
            var result = RunProfile(
                profileName,
                "innerWidth",
                WindowFixture(),
                output);

            AssertProfileSuccess(result, included: 2, external: []);
            var window = File.ReadAllText(Path.Combine(
                ProfileRoot(output, profileName),
                "Interfaces",
                "IWindow.g.cs"));
            Assert.Contains("double InnerWidth { get; }", window);
            Assert.DoesNotContain(
                result.PipelineResult.Errors,
                error => error.Message.Contains(
                    "missing Window owner",
                    StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ProfilePipeline_NestedNamespaceFactoryAndGlobal_AreReachable()
    {
        const string profileName = "NestedNamespace";
        var output = CreateOutputDirectory();
        try
        {
            var result = RunProfile(
                profileName,
                "Outer.Inner.create",
                NestedNamespaceFixture(),
                output);

            AssertProfileSuccess(result, included: 5, external: []);
            var profileRoot = ProfileRoot(output, profileName);
            var globals = File.ReadAllText(Path.Combine(
                profileRoot,
                "Globals",
                "IWindow.Globals.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.Outer.IOuterNamespace Outer { get; }",
                globals);
            var outer = File.ReadAllText(Path.Combine(
                profileRoot,
                "Namespaces",
                "Outer",
                "IOuterNamespace.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.Outer.Inner.IInnerNamespace " +
                "Inner { get; }",
                outer);
            var inner = File.ReadAllText(Path.Combine(
                profileRoot,
                "Namespaces",
                "Outer",
                "Inner",
                "IInnerNamespace.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.Outer.Inner.IWidget Create();",
                inner);
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.Outer.Inner.IWidgetFactory " +
                "WidgetConstructor { get; }",
                inner);
            var factory = File.ReadAllText(Path.Combine(
                profileRoot,
                "Factories",
                "Namespaces",
                "Outer",
                "Inner",
                "IWidgetFactory.g.cs"));
            Assert.Contains(
                "IWidget Create(" +
                "global::Blazor.DOM.Namespaces.Outer.Inner.IDependency dependency);",
                factory);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static IReadOnlyList<SymbolModel> WebAssemblyFixture()
        =>
        [
            Symbol(
                "WebAssembly",
                0,
                "namespace",
                [
                    NamespaceDeclaration(
                        "WebAssembly",
                        [
                            "WebAssembly.CompileError",
                            "WebAssembly.instantiate",
                        ]),
                ]),
            Symbol(
                "WebAssembly.CompileError",
                1,
                "interface",
                [InterfaceDeclaration("CompileError")]),
            Symbol(
                "WebAssembly.Imports",
                2,
                "interface",
                [InterfaceDeclaration("Imports")]),
            Symbol(
                "WebAssembly.Module",
                3,
                "interface",
                [InterfaceDeclaration("Module")]),
            Symbol(
                "WebAssembly.Instance",
                4,
                "interface",
                [InterfaceDeclaration("Instance")]),
            Symbol(
                "WebAssembly.instantiate",
                5,
                "namespace",
                [
                    GlobalFunctionDeclaration(
                        "instantiate",
                        [
                            Parameter(
                                0,
                                "imports",
                                Reference("Imports", "WebAssembly.Imports")),
                            Parameter(
                                1,
                                "module",
                                Reference("Module", "WebAssembly.Module")),
                        ],
                        Reference(
                            "Promise",
                            "Promise",
                            [Reference("Instance", "WebAssembly.Instance")])),
                ],
                bindingKind: "namespaceMember",
                webIdlName: "WebAssembly",
                webIdlMemberName: "instantiate"),
        ];

    private static IReadOnlyList<SymbolModel> CssFixture()
        =>
        [
            Symbol(
                "CSS",
                0,
                "namespace",
                [
                    NamespaceDeclaration(
                        "CSS",
                        ["CSS.escape", "CSS.supports"]),
                ]),
            Symbol(
                "CSS.escape",
                1,
                "namespace",
                [
                    GlobalFunctionDeclaration(
                        "escape",
                        [Parameter(0, "value", new KeywordTypeNode("StringKeyword"))],
                        new KeywordTypeNode("StringKeyword")),
                ],
                bindingKind: "namespaceMember",
                webIdlName: "CSS",
                webIdlMemberName: "escape"),
            Symbol(
                "CSS.supports",
                2,
                "namespace",
                [
                    GlobalFunctionDeclaration(
                        "supports",
                        [Parameter(0, "condition", new KeywordTypeNode("StringKeyword"))],
                        new KeywordTypeNode("BooleanKeyword")),
                ],
                bindingKind: "namespaceMember",
                webIdlName: "CSS",
                webIdlMemberName: "supports"),
        ];

    private static IReadOnlyList<SymbolModel> WindowFixture()
        =>
        [
            Symbol(
                "Window",
                0,
                "interface",
                [
                    InterfaceDeclaration(
                        "Window",
                        [
                            Member(
                                0,
                                "property",
                                "innerWidth",
                                type: new KeywordTypeNode("NumberKeyword"),
                                readOnly: true),
                        ]),
                ]),
            Symbol(
                "innerWidth",
                1,
                "interface",
                [
                    GlobalVariableDeclaration(
                        "innerWidth",
                        new KeywordTypeNode("NumberKeyword")),
                ],
                bindingKind: "globalMember",
                webIdlName: "Window",
                webIdlMemberName: "innerWidth"),
        ];

    private static IReadOnlyList<SymbolModel> NestedNamespaceFixture()
        =>
        [
            Symbol(
                "Outer",
                0,
                "namespace",
                [NamespaceDeclaration("Outer", ["Outer.Inner"])]),
            Symbol(
                "Outer.Inner",
                1,
                "namespace",
                [
                    NamespaceDeclaration(
                        "Inner",
                        ["Outer.Inner.Widget", "Outer.Inner.create"]),
                ]),
            Symbol(
                "Outer.Inner.Widget",
                2,
                "interface",
                [
                    InterfaceDeclaration("Widget"),
                    GlobalVariableDeclaration(
                        "Widget",
                        new TypeLiteralTypeNode(
                        [
                            Member(
                                0,
                                "property",
                                "prototype",
                                type: Reference(
                                    "Widget",
                                    "Outer.Inner.Widget"),
                                readOnly: true),
                            Member(
                                1,
                                "constructSignature",
                                null,
                                parameters:
                                [
                                    Parameter(
                                        0,
                                        "dependency",
                                        Reference(
                                            "Dependency",
                                            "Outer.Inner.Dependency")),
                                ],
                                returnType: Reference(
                                    "Widget",
                                    "Outer.Inner.Widget")),
                        ]),
                        ordinal: 1,
                        constructorObject: true),
                ]),
            Symbol(
                "Outer.Inner.Dependency",
                3,
                "interface",
                [InterfaceDeclaration("Dependency")]),
            Symbol(
                "Outer.Inner.create",
                4,
                "namespace",
                [
                    GlobalFunctionDeclaration(
                        "create",
                        [],
                        Reference("Widget", "Outer.Inner.Widget")),
                ],
                bindingKind: "namespaceMember",
                webIdlName: "Outer.Inner",
                webIdlMemberName: "create"),
        ];

    private static ProfileGenerationResult RunProfile(
        string profileName,
        string rootSymbol,
        IReadOnlyList<SymbolModel> symbols,
        string output)
        => ProfilePipeline.Run(
            new ProfileDefinition(
                profileName,
                "routing closure regression",
                [rootSymbol],
                false,
                false,
                [],
                "Blazor.DOM",
                $"Profiles/{profileName}"),
            new IrBundle(CreateManifest(), symbols, []),
            output);

    private static void AssertProfileSuccess(
        ProfileGenerationResult result,
        int included,
        IReadOnlyList<string> external)
    {
        Assert.True(result.Coverage.ByteIdentityVerified);
        Assert.True(result.PipelineResult.Validation.IsValid);
        Assert.Empty(result.PipelineResult.Errors);
        Assert.Empty(result.PipelineResult.Manifest.Diagnostics);
        Assert.Equal(included, result.IncludedSymbolCount);
        Assert.Equal(external.Count, result.ExternalReferenceCount);
        Assert.Equal(included + external.Count, result.ClosureSize);
        Assert.Equal(external, result.Coverage.ExternalReferences);
        Assert.Equal(included, result.PipelineResult.Manifest.Accounting.TotalSymbols);
        Assert.Equal(0, result.PipelineResult.Manifest.Accounting.GenerationFailed);
    }

    private static int CountExternal(
        IReadOnlySet<string> closure,
        IReadOnlyDictionary<string, SymbolModel> index)
        => closure.Count(name => !index.ContainsKey(name));

    private static Dictionary<string, SymbolModel> Index(
        IEnumerable<SymbolModel> symbols)
        => symbols.ToDictionary(symbol => symbol.Name, StringComparer.Ordinal);

    private static SymbolModel Symbol(
        string name,
        int ordinal,
        string classification,
        IReadOnlyList<DeclarationModel> declarations,
        string bindingKind = "definition",
        string? webIdlName = null,
        string? webIdlMemberName = null)
        => new(
            ordinal,
            name,
            0,
            declarations,
            declarations.Count > 1,
            new SemanticModel(
                "matched",
                webIdlName ?? name,
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
                []));

    private static DeclarationModel InterfaceDeclaration(
        string name,
        IReadOnlyList<MemberModel>? members = null)
        => Declaration("interface", name, members: members);

    private static DeclarationModel NamespaceDeclaration(
        string name,
        IReadOnlyList<string> namespaceMembers)
        => Declaration(
            "namespace",
            name,
            namespaceMembers: namespaceMembers);

    private static DeclarationModel GlobalFunctionDeclaration(
        string name,
        IReadOnlyList<ParameterModel> parameters,
        TypeNode returnType)
        => Declaration(
            "globalFunction",
            name,
            parameters: parameters,
            returnType: returnType);

    private static DeclarationModel GlobalVariableDeclaration(
        string name,
        TypeNode type,
        int ordinal = 0,
        bool constructorObject = false)
        => Declaration(
            "globalVariable",
            name,
            ordinal,
            type: type,
            variableKind: "const",
            constructorObject: constructorObject);

    private static DeclarationModel Declaration(
        string kind,
        string name,
        int ordinal = 0,
        IReadOnlyList<MemberModel>? members = null,
        TypeNode? type = null,
        IReadOnlyList<ParameterModel>? parameters = null,
        TypeNode? returnType = null,
        string? variableKind = null,
        bool constructorObject = false,
        IReadOnlyList<string>? namespaceMembers = null,
        IReadOnlyList<TypeParameterModel>? typeParameters = null)
        => new(
            ordinal,
            kind,
            name,
            [],
            typeParameters ?? [],
            [],
            members ?? [],
            type,
            parameters ?? [],
            returnType,
            Documentation(),
            Location(ordinal + 1),
            variableKind,
            constructorObject,
            new EventMapModel(false, []),
            namespaceMembers ?? []);

    private static MemberModel Member(
        int ordinal,
        string kind,
        string? name,
        TypeNode? type = null,
        IReadOnlyList<ParameterModel>? parameters = null,
        TypeNode? returnType = null,
        bool readOnly = false)
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
            Documentation(),
            Location(ordinal + 20));

    private static ParameterModel Parameter(
        int ordinal,
        string name,
        TypeNode type)
        => new(
            ordinal,
            name,
            false,
            false,
            type,
            null,
            Documentation(),
            Location(ordinal + 40));

    private static ReferenceTypeNode Reference(
        string name,
        string resolvedSymbol,
        IReadOnlyList<TypeNode>? typeArguments = null)
        => new(name, resolvedSymbol, typeArguments ?? []);

    private static DocumentationModel Documentation()
        => new("", [], false);

    private static LocationModel Location(int line)
        => new(
            "profile-fixture.ts",
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
        var path = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "test-output",
            nameof(ProfileClosureRoutingTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ProfileRoot(string output, string profileName)
        => Path.Combine(output, "Profiles", profileName);

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "blazorators.sln")))
                return directory;
            directory = Path.GetDirectoryName(directory);
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
