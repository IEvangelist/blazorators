using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Profiles;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class FinalEmitterAuditTests
{
    [Fact]
    public void EffectiveClassification_UnmatchedInterface_IsSharedByRoutingProjectionAndHeritage()
    {
        var baseSymbol = MakeInterfaceSymbol(
            "TsOnlyBase",
            [],
            status: "unmatched",
            classification: null);
        var derived = MakeInterfaceSymbol(
            "Derived",
            [],
            heritage:
            [
                new HeritageClauseModel(
                    "extends",
                    [new HeritageReferenceTypeNode("TsOnlyBase", null, [])])
            ]);
        var symbols = new[] { baseSymbol, derived };
        var resolver = new TypeResolver(symbols);

        Assert.Equal("interface", resolver.GetClassification("TsOnlyBase"));
        Assert.True(resolver.IsInterfaceOrMixin("TsOnlyBase"));
        Assert.Equal(
            "ITsOnlyBase",
            resolver.Project(
                new ReferenceTypeNode("TsOnlyBase", null, []),
                "fixture/reference").CSharpType);

        var source = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM")
            .Emit(derived)
            .Source;
        Assert.Contains("public partial interface IDerived : ITsOnlyBase", source);

        var output = CreateTempDirectory();
        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateDummyManifest(), symbols, []),
                output);
            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Manifest.Accounting.Projected);
            Assert.Contains(
                result.WrittenFiles,
                file => file.RelativePath == Path.Combine("Interfaces", "ITsOnlyBase.g.cs"));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void EffectiveClassification_UnmatchedJsonValueExtension_IsDictionary()
    {
        var jsonTransport = new TransportModel(
            "json-value",
            false,
            "JsonOptions",
            false,
            true,
            null);
        var baseSymbol = MakeInterfaceSymbol(
            "JsonOptions",
            [],
            classification: "dictionary");
        var absenceMember = MakeProperty(
            0,
            "legacy",
            new KeywordTypeNode("UndefinedKeyword")
            {
                CheckerType = "undefined",
                Transport = jsonTransport with { SourceType = "undefined", Nullable = true },
            }) with
        {
            Optional = true,
        };
        var derived = MakeInterfaceSymbol(
            "JsonOptionsStrict",
            [absenceMember],
            status: "unmatched",
            classification: null,
            heritage:
            [
                new HeritageClauseModel(
                    "extends",
                    [
                        new HeritageReferenceTypeNode("JsonOptions", "JsonOptions", [])
                        {
                            CheckerType = "JsonOptions",
                            Transport = jsonTransport,
                        },
                    ])
            ]) with
        {
            Ordinal = 1,
            Supplemental = true,
        };
        var symbols = new[] { baseSymbol, derived };
        var classification = EffectiveClassificationPolicy.Classify(derived);
        var resolver = new TypeResolver(symbols);

        Assert.Equal("dictionary", classification.Name);
        Assert.Equal(EffectiveClassificationSource.DeclarationShape, classification.Source);
        Assert.True(resolver.IsDictionarySymbol("JsonOptionsStrict"));
        var reference = resolver.Project(
            new ReferenceTypeNode("JsonOptionsStrict", "JsonOptionsStrict", [])
            {
                CheckerType = "JsonOptionsStrict",
                Transport = new TransportModel(
                    "unsupported",
                    false,
                    "JsonOptionsStrict",
                    false,
                    false,
                    "Unmatched TypeScript interface."),
            },
            "fixture/reference");
        Assert.Equal("JsonOptionsStrict", reference.CSharpType);
        Assert.Equal("json-value", reference.Transport?.Kind);

        var output = CreateTempDirectory();
        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateDummyManifest(), symbols, []),
                output);

            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Manifest.Accounting.ProjectedClean);
            Assert.Equal(0, result.Manifest.Accounting.DeferredMembers);
            var generated = File.ReadAllText(Path.Combine(
                output,
                "Dictionaries",
                "JsonOptionsStrict.g.cs"));
            Assert.Contains(
                "public record JsonOptionsStrict : JsonOptions",
                generated);
            Assert.DoesNotContain("Legacy", generated);
            Assert.False(File.Exists(Path.Combine(
                output,
                "Interfaces",
                "IJsonOptionsStrict.g.cs")));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void EffectiveClassification_ReviewedOverride_IsSharedAndDictionaryShapeDoesNotBecomeInterface()
    {
        var ambiguous = MakeInterfaceSymbol(
            "ReviewedOptions",
            [],
            status: "ambiguous",
            classification: null);
        var overrides = new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal)
        {
            ["ReviewedOptions"] = new(
                "ReviewedOptions",
                "dictionary",
                "Reviewed as an initialization dictionary rather than a live interface.")
        };
        var resolver = new TypeResolver([ambiguous], overrides);

        Assert.Equal("dictionary", resolver.GetClassification("ReviewedOptions"));
        Assert.True(resolver.IsDictionarySymbol("ReviewedOptions"));
        Assert.False(resolver.IsInterfaceOrMixin("ReviewedOptions"));
        Assert.Equal(
            "ReviewedOptions",
            resolver.Project(
                new ReferenceTypeNode("ReviewedOptions", null, []),
                "fixture/reference").CSharpType);
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("enum")]
    [InlineData("typedef")]
    public void EffectiveClassification_KnownNonInterfaceShape_IsRejectedFromHeritage(
        string classification)
    {
        var baseSymbol = MakeInterfaceSymbol(
            "NonInterfaceBase",
            [],
            classification: classification);
        var derived = MakeInterfaceSymbol(
            "Derived",
            [],
            heritage:
            [
                new HeritageClauseModel(
                    "extends",
                    [new HeritageReferenceTypeNode("NonInterfaceBase", null, [])])
            ]);
        var resolver = new TypeResolver([baseSymbol, derived]);

        Assert.Equal(classification, resolver.GetClassification("NonInterfaceBase"));
        Assert.False(resolver.IsInterfaceOrMixin("NonInterfaceBase"));
        var exception = Assert.Throws<InterfaceEmitException>(
            () => new InterfaceEmitter(
                resolver,
                "1.0.0",
                "Blazor.DOM").Emit(derived));
        Assert.Contains("not interface/mixin", exception.Message);
    }

    [Fact]
    public void Corpus_UnmatchedInterfaceHeritageTargets_UseEffectiveInterfaceClassification()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var ir = IrLoader.Load(data);
        var resolver = new TypeResolver(
            ir.TypescriptSymbols,
            EmitterOverridesLoader.Load(data));
        var cases = new[]
        {
            ("HTMLButtonElement", "PopoverInvokerElement"),
            ("HTMLCollection", "HTMLCollectionBase"),
            ("HTMLCollectionOf", "HTMLCollectionBase"),
            ("HTMLElement", "HTMLOrSVGElement"),
            ("HTMLFormControlsCollection", "HTMLCollectionBase"),
            ("HTMLInputElement", "PopoverInvokerElement"),
            ("HTMLOptionsCollection", "HTMLCollectionOf"),
            ("MathMLElement", "HTMLOrSVGElement"),
            ("RadioNodeList", "NodeListOf"),
            ("SVGElement", "HTMLOrSVGElement"),
        };

        foreach (var (derived, baseName) in cases)
        {
            var symbol = Assert.Single(
                ir.TypescriptSymbols,
                symbol => symbol.Name == derived);
            Assert.Contains(
                symbol.Declarations.SelectMany(declaration => declaration.Heritage)
                    .SelectMany(heritage => heritage.Types)
                    .OfType<HeritageReferenceTypeNode>(),
                heritage => heritage.Expression == baseName);
            Assert.Equal("interface", resolver.GetClassification(baseName));
            Assert.True(resolver.IsInterfaceOrMixin(baseName));
        }
    }

    [Fact]
    public void Corpus_StrictGeneration_HasExactAccountingDiagnosticsAndLfArtifacts()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDirectory();
        try
        {
            var ir = IrLoader.Load(data);
            var overrides = EmitterOverridesLoader.Load(data);
            var result = GenerationPipeline.Run(
                ir,
                output,
                overrides);
            var accounting = result.Manifest.Accounting;

            Assert.True(result.Validation.IsValid);
            Assert.Equal(2183, accounting.TotalSymbols);
            Assert.Equal(2183, accounting.Projected);
            Assert.Equal(2183, accounting.ProjectedClean);
            Assert.Equal(0, accounting.ProjectedWithDeferredMembers);
            Assert.Equal(0, accounting.Excluded);
            Assert.Equal(0, accounting.Deferred);
            Assert.Equal(0, accounting.GenerationFailed);
            Assert.Equal(3028, result.WrittenFiles.Count);
            Assert.Equal((3022, 3022), (
                accounting.AccountedSourceDeclarations,
                accounting.SourceDeclarations));
            Assert.All(
                accounting.SourceDeclarationEntries ?? [],
                entry => Assert.Equal(
                    nameof(MemberOutcomeStatus.Projected),
                    entry.Status));
            Assert.Equal((12217, 12217), (
                accounting.AccountedSourceMembers,
                accounting.SourceMembers));
            Assert.Equal(12217, accounting.TotalMembers);
            Assert.Equal(12217, accounting.ExpectedMembers);
            Assert.Equal(12217, accounting.ProjectedMembers);
            Assert.Equal(0, accounting.DeferredMembers);
            Assert.Equal(0, accounting.FailedMembers);
            Assert.Equal((3880, 3880), (
                accounting.AccountedSourceOverloads,
                accounting.SourceOverloads));
            Assert.Equal((6522, 6522), (
                accounting.AccountedSourceParameters,
                accounting.SourceParameters));
            Assert.True(accounting.ParameterReconciliationValid);
            Assert.True(result.Validation.ParameterReconciliationValid);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Manifest.Diagnostics);
            Assert.Equal(432, result.Manifest.SynthesizedTypes?.Count);
            Assert.Equal(
                (6, 3, 182, 208, 1, 1),
                (
                    result.Manifest.SynthesizedTypes?.Count(type =>
                        type.Kind == "Tuple"),
                    result.Manifest.SynthesizedTypes?.Count(type =>
                        type.Kind == "Record"),
                    result.Manifest.SynthesizedTypes?.Count(type =>
                        type.Kind == "String"),
                    result.Manifest.SynthesizedTypes?.Count(type =>
                        type.Kind == "Union"),
                    result.Manifest.SynthesizedTypes?.Count(type =>
                        type.Kind == "Standard"),
                    result.Manifest.SynthesizedTypes?.Count(type =>
                        type.Kind == "Never")));
            Assert.All(
                result.Manifest.SynthesizedTypes ?? [],
                type =>
                {
                    Assert.DoesNotMatch(@"_[0-9a-f]{10}$", type.Name);
                    Assert.DoesNotContain("Shape_", type.Name);
                });
            Assert.All(
                (result.Manifest.SynthesizedTypes ?? [])
                    .Where(type => type.Kind == "Union"),
                type => Assert.DoesNotMatch(
                    @"\bArm\d+\b",
                    File.ReadAllText(Path.Combine(output, type.RelativePath))));
            Assert.Empty(accounting.DeferredSymbols);
            Assert.Empty(accounting.DeferredMemberEntries ?? []);
            Assert.Empty(accounting.FailedSymbols);
            Assert.Equal(
                (3880, 0, 0),
                (
                    (accounting.SourceOverloadEntries ?? []).Count(entry =>
                        entry.Status == nameof(MemberOutcomeStatus.Projected)),
                    (accounting.SourceOverloadEntries ?? []).Count(entry =>
                        entry.Status == nameof(MemberOutcomeStatus.Deferred)),
                    (accounting.SourceOverloadEntries ?? []).Count(entry =>
                        entry.Status == nameof(MemberOutcomeStatus.Failed))));
            Assert.DoesNotContain(
                accounting.SourceOverloadEntries ?? [],
                entry => entry.Status != nameof(MemberOutcomeStatus.Projected));
            var allParameters = (accounting.SourceOverloadEntries ?? [])
                .SelectMany(entry => entry.ParameterOutcomes)
                .ToList();
            Assert.Equal(
                (6522, 0, 0),
                (
                    allParameters.Count(entry =>
                        entry.Status == MemberOutcomeStatus.Projected),
                    allParameters.Count(entry =>
                        entry.Status is MemberOutcomeStatus.Deferred
                            or MemberOutcomeStatus.NotAttemptedAfterFailure),
                    allParameters.Count(entry =>
                        entry.Status == MemberOutcomeStatus.Failed)));
            Assert.All(allParameters, entry =>
                Assert.Equal(MemberOutcomeStatus.Projected, entry.Status));
            var failureCategories = accounting.FailedSymbols
                .GroupBy(entry => StrictFailureCategory(entry.Reason))
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);
            Assert.Empty(failureCategories);
            Assert.Empty(accounting.FailedSymbols);
            var newlyProjected = new[]
            {
                "ByteLengthQueuingStrategy",
                "CountQueuingStrategy",
                "CSPViolationReportBody",
                "DOMException",
                "Document",
                "Element",
                "HTMLCanvasElement",
                "OffscreenCanvas",
                "OnErrorEventHandlerNonNull",
                "Report",
                "ReportBody",
                "SubtleCrypto",
                "WebAssembly.CompileError",
                "WebAssembly.LinkError",
                "WebAssembly.RuntimeError",
                "WebAssembly.ValueTypeMap",
                "WebGLRenderingContextBase",
            };
            Assert.All(
                newlyProjected,
                symbolName => Assert.Contains(
                    symbolName,
                    accounting.ProjectedSymbols));
            var resolver = new TypeResolver(ir.TypescriptSymbols, overrides);
            foreach (var symbolName in new[]
                {
                    "CSPViolationReportBody",
                    "Report",
                    "ReportBody",
                })
            {
                var symbol = Assert.Single(
                    ir.TypescriptSymbols,
                    candidate => candidate.Name == symbolName);
                var classification = EffectiveClassificationPolicy.Classify(
                    symbol,
                    overrides);
                Assert.Equal("interface", classification.Name);
                Assert.Equal(
                    EffectiveClassificationSource.ReviewedOverride,
                    classification.Source);
                Assert.True(resolver.IsInterfaceOrMixin(symbolName));
                Assert.True(overrides[symbolName].Rationale.Length >= 10);
                Assert.True(File.Exists(Path.Combine(
                    output,
                    "Interfaces",
                    $"I{symbolName}.g.cs")));
                Assert.True(File.Exists(Path.Combine(
                    output,
                    "Factories",
                    $"I{symbolName}Factory.g.cs")));
                Assert.False(File.Exists(Path.Combine(
                    output,
                    "Dictionaries",
                    $"{symbolName}.g.cs")));
            }
            Assert.Contains(
                "public partial interface ICSPViolationReportBody : IReportBody",
                File.ReadAllText(Path.Combine(
                    output,
                    "Interfaces",
                    "ICSPViolationReportBody.g.cs")));
            Assert.Contains(
                "IReportBody? Body { get; }",
                File.ReadAllText(Path.Combine(
                    output,
                    "Interfaces",
                    "IReport.g.cs")));
            var resolvedAccessorFailures = new[]
            {
                ("CSSFontFaceRule", "Style"),
                ("CSSImportRule", "Media"),
                ("CSSKeyframeRule", "Style"),
                ("CSSMediaRule", "Media"),
                ("CSSNestedDeclarations", "Style"),
                ("CSSPageRule", "Style"),
                ("CSSStyleRule", "Style"),
                ("ElementCSSInlineStyle", "Style"),
                ("HTMLAnchorElement", "RelList"),
                ("HTMLAreaElement", "RelList"),
                ("HTMLFormElement", "RelList"),
                ("HTMLIFrameElement", "Sandbox"),
                ("HTMLLinkElement", "Blocking"),
                ("HTMLOutputElement", "HtmlFor"),
                ("HTMLScriptElement", "Blocking"),
                ("HTMLStyleElement", "Blocking"),
                ("SVGAElement", "RelList"),
                ("StyleSheet", "Media"),
                ("Window", "Location"),
            };
            foreach (var (symbolName, memberName) in resolvedAccessorFailures)
            {
                Assert.Contains(symbolName, accounting.ProjectedSymbols);
                var source = File.ReadAllText(Path.Combine(
                    output,
                    "Interfaces",
                    $"I{symbolName}.g.cs"));
                Assert.Contains($"void Set{memberName}(", source);
                Assert.Contains("DomAccessorOperation.Get", source);
                Assert.Contains("DomAccessorOperation.Set", source);
            }
            Assert.Contains("location", accounting.ProjectedSymbols);
            Assert.Contains(
                "IQueuingStrategyContract",
                File.ReadAllText(Path.Combine(
                    output,
                    "Dictionaries",
                    "QueuingStrategy.g.cs")));
            Assert.Contains(
                "global::Blazor.DOM.StandardTypes.ITypeScriptError",
                File.ReadAllText(Path.Combine(
                    output,
                    "Interfaces",
                    "IDOMException.g.cs")));
            Assert.Contains(
                "TypeScriptNever V128",
                File.ReadAllText(Path.Combine(
                    output,
                    "Interfaces",
                    "Namespaces",
                    "WebAssembly",
                    "IValueTypeMap.g.cs")));
            Assert.All(result.Manifest.Diagnostics, diagnostic =>
            {
                Assert.Equal("GENERATION_FAILED", diagnostic.Code);
                Assert.Equal("error", diagnostic.Severity);
            });
            Assert.Equal(
                accounting.FailedSymbols.Select(entry => entry.Symbol),
                result.Manifest.Diagnostics.Select(diagnostic =>
                    diagnostic.Message[
                        ..diagnostic.Message.IndexOf(':')]));
            Assert.Contains(
                "PopoverInvokerElement",
                accounting.ProjectedSymbols);
            Assert.Contains(
                "CustomElementConstructor",
                accounting.ProjectedSymbols);
            Assert.Contains("NodeFilter", accounting.ProjectedSymbols);
            Assert.Contains("XPathNSResolver", accounting.ProjectedSymbols);

            var windowGlobals = File.ReadAllText(Path.Combine(
                output,
                "Globals",
                "IWindow.Globals.g.cs"));
            foreach (var name in new[] { "Bluetooth", "USB", "GPUAdapter" })
            {
                var statics = File.ReadAllText(Path.Combine(
                    output,
                    "Factories",
                    $"I{name}Statics.g.cs"));
                Assert.DoesNotContain(" Create(", statics);
                Assert.Contains($"I{name}Statics {name}Statics", windowGlobals);
                Assert.DoesNotContain($"{name}Constructor", windowGlobals);
            }
            var presentationRequestFactory = File.ReadAllText(Path.Combine(
                output,
                "Factories",
                "IPresentationRequestFactory.g.cs"));
            Assert.Contains("IPresentationRequest Create(", presentationRequestFactory);
            Assert.Contains(
                "IPresentationRequestFactory PresentationRequestConstructor",
                windowGlobals);

            var typeLiteralDeclarations = ir.TypescriptSymbols
                .SelectMany(symbol => symbol.Declarations
                    .Where(declaration =>
                        declaration.Kind == "globalVariable"
                        && declaration.Type is TypeLiteralTypeNode)
                    .Select(declaration => (symbol.Name, declaration.Ordinal)))
                .ToList();
            Assert.Equal(730, typeLiteralDeclarations.Count);
            var declarationEntries = accounting.SourceDeclarationEntries ?? [];
            var projectedTypeLiteralDeclarations = 0;
            var deferredTypeLiteralDeclarations = 0;
            foreach (var (symbolName, declarationOrdinal) in typeLiteralDeclarations)
            {
                var declaration = Assert.Single(
                    declarationEntries,
                    entry => entry.SymbolName == symbolName
                        && entry.DeclarationOrdinal == declarationOrdinal);
                if (declaration.Status == nameof(MemberOutcomeStatus.Projected))
                {
                    Assert.Null(declaration.Phase);
                    projectedTypeLiteralDeclarations++;
                }
                else
                {
                    Assert.Equal(
                        nameof(MemberOutcomeStatus.Deferred),
                        declaration.Status);
                    Assert.Contains(
                        declaration.Phase,
                        new[]
                        {
                            "factory-constructor",
                            "advanced-generic-constraints",
                            "primary-contract",
                            "promise-transport",
                            "standard-container-transport",
                            "typed-union-arm-discriminator",
                            "typed-union-interface-discriminator",
                        });
                    deferredTypeLiteralDeclarations++;
                }
            }
            Assert.Equal(730, projectedTypeLiteralDeclarations);
            Assert.Equal(0, deferredTypeLiteralDeclarations);

            var factoryMembers = (accounting.SourceMemberEntries ?? [])
                .Where(entry => entry.QualifiedKey.Contains(
                    "/typeLiteral/member[",
                    StringComparison.Ordinal)
                    && declarationEntries.Any(declaration =>
                        declaration.SymbolName == entry.SymbolName
                        && declaration.DeclarationOrdinal
                            == entry.DeclarationOrdinal
                        && declaration.Kind == "globalVariable"))
                .ToList();
            Assert.Equal(
                accounting.SourceMembers,
                (accounting.SourceMemberEntries ?? [])
                    .Select(entry => entry.QualifiedKey)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.Equal(
                accounting.SourceOverloads,
                (accounting.SourceOverloadEntries ?? [])
                    .Select(entry => entry.QualifiedKey)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            var parameterEntries = (accounting.SourceOverloadEntries ?? [])
                .SelectMany(entry => entry.ParameterOutcomes)
                .ToList();
            Assert.Equal(accounting.SourceParameters, parameterEntries.Count);
            Assert.Equal(
                accounting.SourceParameters,
                parameterEntries
                    .Select(entry => entry.Provenance)
                    .Distinct(StringComparer.Ordinal)
                    .Count());

            var globalFunctionDeclarations = ir.TypescriptSymbols
                .SelectMany(symbol => symbol.Declarations
                    .Where(declaration => declaration.Kind == "globalFunction")
                    .Select(declaration => (symbol.Name, declaration.Ordinal)))
                .ToList();
            Assert.Equal(120, globalFunctionDeclarations.Count);
            Assert.Equal(
                111,
                globalFunctionDeclarations
                    .Select(item => item.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            var globalFunctionOverloads = (accounting.SourceOverloadEntries ?? [])
                .Where(entry => entry.Kind == "globalFunction")
                .ToList();
            Assert.Equal(120, globalFunctionOverloads.Count);
            Assert.Equal(
                150,
                globalFunctionOverloads.Sum(entry => entry.ParameterOutcomes.Count));
            Assert.Equal(
                (120, 0, 0),
                (
                    globalFunctionOverloads.Count(entry =>
                        entry.Status == nameof(MemberOutcomeStatus.Projected)),
                    globalFunctionOverloads.Count(entry =>
                        entry.Status == nameof(MemberOutcomeStatus.Deferred)),
                    globalFunctionOverloads.Count(entry =>
                        entry.Status == nameof(MemberOutcomeStatus.Failed))));
            Assert.Equal(
                (150, 0, 0),
                (
                    globalFunctionOverloads
                        .SelectMany(entry => entry.ParameterOutcomes)
                        .Count(parameter =>
                            parameter.Status == MemberOutcomeStatus.Projected),
                    globalFunctionOverloads
                        .SelectMany(entry => entry.ParameterOutcomes)
                        .Count(parameter =>
                            parameter.Status is MemberOutcomeStatus.Deferred
                                or MemberOutcomeStatus.NotAttemptedAfterFailure),
                    globalFunctionOverloads
                        .SelectMany(entry => entry.ParameterOutcomes)
                        .Count(parameter =>
                            parameter.Status == MemberOutcomeStatus.Failed)));
            Assert.Equal(
                [2, 1],
                globalFunctionOverloads
                    .Where(entry => entry.SymbolName == "CSS.supports")
                    .OrderBy(entry => entry.DeclarationOrdinal)
                    .Select(entry => entry.ParameterOutcomes.Count)
                    .ToArray());
            Assert.Equal(
                [3, 3],
                globalFunctionOverloads
                    .Where(entry => entry.SymbolName == "addEventListener")
                    .OrderBy(entry => entry.DeclarationOrdinal)
                    .Select(entry => entry.ParameterOutcomes.Count)
                    .ToArray());
            foreach (var (symbolName, declarationOrdinal) in globalFunctionDeclarations)
            {
                var declaration = Assert.Single(
                    declarationEntries,
                    entry => entry.SymbolName == symbolName
                        && entry.DeclarationOrdinal == declarationOrdinal);
                Assert.Contains(
                    declaration.Status,
                    new[]
                    {
                        nameof(MemberOutcomeStatus.Projected),
                        nameof(MemberOutcomeStatus.Deferred),
                        nameof(MemberOutcomeStatus.Failed),
                    });
            }

            foreach (var (symbolName, memberName) in new[]
            {
                ("NodeFilter", "acceptNode"),
                ("XPathNSResolver", "lookupNamespaceURI"),
            })
            {
                var callbackDeclaration = Assert.Single(
                    declarationEntries,
                    entry => entry.SymbolName == symbolName
                        && entry.DeclarationOrdinal == 0);
                Assert.Equal(
                    nameof(MemberOutcomeStatus.Projected),
                    callbackDeclaration.Status);
                Assert.Null(callbackDeclaration.Phase);
                Assert.Contains(
                    $"{symbolName}/decl[0]/typeAlias",
                    callbackDeclaration.Provenance);

                var member = Assert.Single(
                    accounting.SourceMemberEntries ?? [],
                    entry => entry.SymbolName == symbolName
                        && entry.MemberName == memberName);
                Assert.Equal(nameof(MemberOutcomeStatus.Projected), member.Status);
                Assert.Null(member.Phase);
                Assert.Contains("/type/union[1]/typeLiteral/member[0]", member.QualifiedKey);

                var objectOverload = Assert.Single(
                    accounting.SourceOverloadEntries ?? [],
                    entry => entry.SymbolName == symbolName
                        && entry.Name == memberName
                        && entry.Kind == "method");
                Assert.Equal(nameof(MemberOutcomeStatus.Projected), objectOverload.Status);
                Assert.Null(objectOverload.Phase);
                var objectParameter = Assert.Single(objectOverload.ParameterOutcomes);
                Assert.Equal(MemberOutcomeStatus.Projected, objectParameter.Status);
                Assert.Null(objectParameter.Phase);
                Assert.Contains(
                    "/type/union[1]/typeLiteral/member[0]",
                    objectParameter.Provenance);

                var functionOverload = Assert.Single(
                    accounting.SourceOverloadEntries ?? [],
                    entry => entry.SymbolName == symbolName
                        && entry.Kind == "function");
                Assert.Equal(nameof(MemberOutcomeStatus.Projected), functionOverload.Status);
                Assert.Equal(
                    MemberOutcomeStatus.Projected,
                    Assert.Single(functionOverload.ParameterOutcomes).Status);
            }
            Assert.Contains(
                accounting.SourceMemberEntries ?? [],
                entry => entry.SymbolName == "WebAssembly.Global"
                    && entry.QualifiedKey.StartsWith(
                        "WebAssembly.Global/decl[",
                        StringComparison.Ordinal));
            Assert.Equal(2576, factoryMembers.Count);
            Assert.Equal(
                2576,
                factoryMembers.Count(entry =>
                    entry.Status == nameof(MemberOutcomeStatus.Projected)));
            Assert.Equal(
                0,
                factoryMembers.Count(entry =>
                    entry.Status == nameof(MemberOutcomeStatus.Deferred)
                    && entry.Phase == "factory-constructor"));
            Assert.Equal(
                0,
                factoryMembers.Count(
                entry => entry.Status == nameof(MemberOutcomeStatus.Deferred)
                    && entry.Phase == "advanced-generic-constraints"));
            Assert.Equal(
                0,
                factoryMembers.Count(entry =>
                entry.Status == nameof(MemberOutcomeStatus.Deferred)
                && entry.Phase == "primary-contract"));
            Assert.Equal(
                0,
                factoryMembers.Count(entry =>
                entry.Status == nameof(MemberOutcomeStatus.Deferred)
                && entry.Phase == "promise-transport"));
            Assert.Equal(
                0,
                factoryMembers.Count(entry =>
                entry.Status == nameof(MemberOutcomeStatus.Deferred)
                && entry.Phase == "standard-container-transport"));
            Assert.Equal(
                0,
                factoryMembers.Count(entry =>
                entry.Status == nameof(MemberOutcomeStatus.Deferred)
                && entry.Phase == "typed-union-interface-discriminator"));

            var wakeLockMembers = factoryMembers
                .Where(entry => entry.SymbolName == "WakeLock")
                .OrderBy(entry => entry.MemberOrdinal)
                .ToList();
            Assert.Equal(2, wakeLockMembers.Count);
            Assert.Equal(["prototype", "constructSignature"],
                wakeLockMembers.Select(entry => entry.MemberName).ToArray());
            var wakeLockConstructor = Assert.Single(
                accounting.SourceOverloadEntries ?? [],
                entry => entry.SymbolName == "WakeLock"
                    && entry.Kind == "constructSignature");
            Assert.Equal(
                nameof(MemberOutcomeStatus.Projected),
                wakeLockConstructor.Status);
            Assert.Null(wakeLockConstructor.Phase);

            var requestWindow = Assert.Single(
                accounting.SourceMemberEntries ?? [],
                entry => entry.SymbolName == "RequestInit"
                    && entry.MemberName == "window");
            Assert.Equal(nameof(MemberOutcomeStatus.Projected), requestWindow.Status);
            Assert.Null(requestWindow.Phase);

            var blobCallback = Assert.Single(
                accounting.SourceOverloadEntries ?? [],
                entry => entry.SymbolName == "BlobCallback");
            Assert.Equal(nameof(MemberOutcomeStatus.Projected), blobCallback.Status);
            var blobParameter = Assert.Single(blobCallback.ParameterOutcomes);
            Assert.Equal("blob", blobParameter.Name);
            Assert.Equal(nameof(MemberOutcomeStatus.Projected), blobParameter.Status.ToString());
            Assert.Contains(
                "BlobCallback/decl[0]/member[0]",
                blobParameter.Provenance);

            var globalContractPath = Path.Combine(
                output,
                "Globals",
                "IWindow.Globals.g.cs");
            var globalContract = File.ReadAllText(globalContractPath);
            Assert.DoesNotContain("SetTimeout(", globalContract);
            Assert.Contains("setTimeout", accounting.ProjectedSymbols);
            Assert.DoesNotContain("InnerWidth", globalContract);
            Assert.Contains(
                "double InnerWidth { get; set; }",
                File.ReadAllText(Path.Combine(
                    output,
                    "Interfaces",
                    "IWindow.g.cs")));
            Assert.Contains(
                "IAudioFactory AudioConstructor { get; }",
                globalContract);
            Assert.Contains(
                "IImageFactory ImageConstructor { get; }",
                globalContract);
            Assert.Contains(
                "IOptionFactory OptionConstructor { get; }",
                globalContract);
            Assert.Contains(
                "global::Blazor.DOM.Namespaces.WebAssembly.IWebAssemblyNamespace " +
                "WebAssembly { get; }",
                globalContract);

            var webAssemblyNamespacePath = Path.Combine(
                output,
                "Namespaces",
                "WebAssembly",
                "IWebAssemblyNamespace.g.cs");
            var webAssemblyNamespace = File.ReadAllText(
                webAssemblyNamespacePath);
            Assert.Contains(
                "ValueTask<global::Blazor.DOM.Namespaces.WebAssembly.IModule> " +
                "CompileAsync(BufferSource bytes);",
                webAssemblyNamespace);
            Assert.Contains(
                "ValueTask<global::Blazor.DOM.Namespaces.WebAssembly.IInstance> " +
                "InstantiateAsync(",
                webAssemblyNamespace);

            var moduleFactoryPath = Path.Combine(
                output,
                "Factories",
                "Namespaces",
                "WebAssembly",
                "IModuleFactory.g.cs");
            var moduleFactory = File.ReadAllText(moduleFactoryPath);
            Assert.Contains("IModule Create(BufferSource bytes);", moduleFactory);
            Assert.Contains("CustomSections(", moduleFactory);
            Assert.Contains(
                accounting.SourceMemberEntries ?? [],
                entry => entry.SymbolName == "WebAssembly.Module"
                    && entry.MemberName == "customSections"
                    && entry.Status == nameof(MemberOutcomeStatus.Projected));
            Assert.Contains(" Exports(", moduleFactory);
            Assert.Contains(
                accounting.SourceMemberEntries ?? [],
                entry => entry.SymbolName == "WebAssembly.Module"
                    && entry.MemberName == "exports"
                    && entry.Status == nameof(MemberOutcomeStatus.Projected));

            foreach (var relativePath in new[]
            {
                Path.Combine("Namespaces", "CSS", "ICSSNamespace.g.cs"),
            })
            {
                var fixturePath = Path.Combine(
                    root,
                    "tests",
                    "Blazor.DOM.GlobalNamespace.CompilationTests",
                    "Generated",
                    relativePath);
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(output, relativePath)),
                    File.ReadAllBytes(fixturePath));
            }

            var toStringDeclaration = Assert.Single(
                declarationEntries,
                entry => entry.SymbolName == "toString"
                    && entry.Kind == "globalFunction");
            Assert.Equal(
                nameof(MemberOutcomeStatus.Projected),
                toStringDeclaration.Status);
            Assert.Null(toStringDeclaration.Phase);

            Assert.All(
                declarationEntries.Where(entry =>
                    entry.Kind is "globalVariable" or "globalFunction"
                    && entry.Status == nameof(MemberOutcomeStatus.Failed)),
                entry => Assert.False(string.IsNullOrWhiteSpace(entry.Reason)));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories),
                path => File.ReadAllBytes(path).Contains((byte)'\r'));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void Corpus_WakeLockProfile_IndependentRunsMatchRecursively()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var profile = ProfileLoader.Load(Path.Combine(
            root,
            "data",
            "Blazor.DOM.Profiles",
            "WakeLock.profile.json"));
        var output1 = CreateTempDirectory();
        var output2 = CreateTempDirectory();
        try
        {
            var result1 = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output1,
                EmitterOverridesLoader.Load(data));
            var result2 = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output2,
                EmitterOverridesLoader.Load(data));
            var accounting = result1.PipelineResult.Manifest.Accounting;

            Assert.True(result1.Coverage.ByteIdentityVerified);
            Assert.True(result2.Coverage.ByteIdentityVerified);
            Assert.True(result1.PipelineResult.Validation.IsValid);
            Assert.True(result2.PipelineResult.Validation.IsValid);
            Assert.Empty(result1.PipelineResult.Errors);
            Assert.Empty(result2.PipelineResult.Errors);
            Assert.Empty(result1.PipelineResult.Manifest.Diagnostics);
            Assert.Empty(result2.PipelineResult.Manifest.Diagnostics);
            Assert.Equal((13, 12, 1), (
                result1.ClosureSize,
                result1.IncludedSymbolCount,
                result1.ExternalReferenceCount));
            Assert.Equal((12, 12, 0), (
                accounting.Projected,
                accounting.ProjectedClean,
                accounting.ProjectedWithDeferredMembers));
            Assert.Equal((16, 16), (
                accounting.AccountedSourceDeclarations,
                accounting.SourceDeclarations));
            Assert.Equal((23, 23), (
                accounting.AccountedSourceMembers,
                accounting.SourceMembers));
            Assert.Equal((12, 12), (
                accounting.AccountedSourceOverloads,
                accounting.SourceOverloads));
            Assert.Equal((17, 17), (
                accounting.AccountedSourceParameters,
                accounting.SourceParameters));

            var generated1 = SnapshotTree(Path.Combine(
                output1,
                "Profiles",
                "WakeLock"));
            var generated2 = SnapshotTree(Path.Combine(
                output2,
                "Profiles",
                "WakeLock"));
            Assert.Equal(42, generated1.Count);
            AssertTreesEqual(generated1, generated2);
            Assert.All(
                generated1.Values,
                bytes => Assert.DoesNotContain((byte)'\r', bytes));
        }
        finally
        {
            Directory.Delete(output1, recursive: true);
            Directory.Delete(output2, recursive: true);
        }
    }

    [Fact]
    public void TypeProjection_NullableValueTask_PreservesValueNullabilityIdentity()
    {
        var resolver = new TypeResolver([]);
        var promise = new ReferenceTypeNode(
            "Promise",
            null,
            [new KeywordTypeNode("NumberKeyword")]);
        var nullablePromise = new UnionTypeNode(
            [promise, new LiteralTypeNode("NullKeyword", "null")]);

        var required = resolver.Project(promise, "method/return");
        var nullable = resolver.Project(nullablePromise, "nullable-method/return");

        Assert.Equal(ClrTypeKind.Value, required.Identity.Kind);
        Assert.Equal("ValueTask<double>", required.RenderedType);
        Assert.Equal("ValueTask<double>", required.CanonicalType);
        Assert.Equal("ValueTask<double>?", nullable.RenderedType);
        Assert.Equal("ValueTask<double>?", nullable.CanonicalType);
    }

    [Fact]
    public void TypeProjection_NullableMemory_PreservesValueNullabilityIdentity()
    {
        var resolver = new TypeResolver([]);
        var dataView = new ReferenceTypeNode("DataView", null, []);
        var nullableDataView = new UnionTypeNode(
            [dataView, new LiteralTypeNode("NullKeyword", "null")]);

        var required = resolver.Project(dataView, "data-view");
        var nullable = resolver.Project(nullableDataView, "nullable-data-view");

        Assert.Equal(ClrTypeKind.Value, required.Identity.Kind);
        Assert.Equal("System.Memory<byte>", required.CanonicalType);
        Assert.Equal("System.Memory<byte>?", nullable.CanonicalType);
    }

    [Fact]
    public void InterfaceEmitter_NullableGenericStructParameters_RemainDistinctOverloads()
    {
        var promise = new ReferenceTypeNode(
            "Promise",
            null,
            [new KeywordTypeNode("NumberKeyword")]);
        var nullablePromise = new UnionTypeNode(
            [promise, new LiteralTypeNode("NullKeyword", "null")]);
        var dataView = new ReferenceTypeNode("DataView", null, []);
        var nullableDataView = new UnionTypeNode(
            [dataView, new LiteralTypeNode("NullKeyword", "null")]);
        var symbol = MakeInterfaceSymbol(
            "OverloadHost",
            [
                MakeMethod(0, "acceptTask", [MakeParameter("value", promise)]),
                MakeMethod(1, "acceptTask", [MakeParameter("value", nullablePromise)]),
                MakeMethod(2, "acceptMemory", [MakeParameter("value", dataView)]),
                MakeMethod(3, "acceptMemory", [MakeParameter("value", nullableDataView)]),
            ]);

        var result = new InterfaceEmitter(
            new TypeResolver([]),
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Contains(
            "global::Microsoft.JSInterop.IBrowserPromise<double>",
            result.Source);
        Assert.Equal(1, result.Source.Split("void AcceptTask(").Length - 1);
        Assert.Contains("System.Memory<byte> @value", result.Source);
        Assert.Contains("System.Memory<byte>? @value", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_NullableReferenceGeneric_StillCollapsesForOverloadIdentity()
    {
        var iterable = new ReferenceTypeNode(
            "Iterable",
            null,
            [new KeywordTypeNode("StringKeyword")]);
        var nullableIterable = new UnionTypeNode(
            [iterable, new LiteralTypeNode("NullKeyword", "null")]);
        var symbol = MakeInterfaceSymbol(
            "ReferenceHost",
            [
                MakeMethod(0, "accept", [MakeParameter("value", iterable)]),
                MakeMethod(1, "accept", [MakeParameter("value", nullableIterable)]),
            ]);

        var result = new InterfaceEmitter(
            new TypeResolver([]),
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Equal(1, result.Source.Split("void Accept(").Length - 1);
        Assert.Contains(
            result.MemberOutcomes,
            outcome => outcome.Ordinal == 1
                && (outcome.Reason ?? "").Contains("Deduplicated", StringComparison.Ordinal));
    }

    [Fact]
    public void InterfaceEmitter_NullableValueTaskReturnCollision_EmitsTypedUnion()
    {
        var promise = new ReferenceTypeNode(
            "Promise",
            null,
            [new KeywordTypeNode("NumberKeyword")]);
        var nullablePromise = new UnionTypeNode(
            [promise, new LiteralTypeNode("NullKeyword", "null")]);
        var symbol = MakeInterfaceSymbol(
            "ReturnHost",
            [
                MakeMethod(0, "read", [], promise),
                MakeMethod(1, "read", [], nullablePromise),
            ]);

        var resolver = new TypeResolver([]);
        var result = new InterfaceEmitter(
            resolver,
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Equal(1, result.Source.Split(" ReadAsync(").Length - 1);
        Assert.Contains(" Read", result.Source);
        Assert.All(
            result.MemberOutcomes.Where(outcome => outcome.Name == "read"),
            outcome => Assert.Equal(MemberOutcomeStatus.Projected, outcome.Status));
        Assert.Empty(resolver.SynthesizedTypes);
    }

    [Fact]
    public void InterfaceEmitter_LiteralDispatchedReturns_UseDistinctFiniteDomains()
    {
        var resolver = new TypeResolver([]);
        var symbol = MakeInterfaceSymbol(
            "LiteralDispatchHost",
            [
                MakeMethod(
                    0,
                    "read",
                    [MakeParameter(
                        "kind",
                        new LiteralTypeNode("StringLiteral", "\"text\""))],
                    new KeywordTypeNode("StringKeyword")),
                MakeMethod(
                    1,
                    "read",
                    [MakeParameter(
                        "kind",
                        new LiteralTypeNode("StringLiteral", "\"count\""))],
                    new KeywordTypeNode("NumberKeyword")),
            ]);

        var result = new InterfaceEmitter(
            resolver,
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Equal(2, result.Source.Split(" Read(").Length - 1);
        Assert.DoesNotContain("string kind", result.Source);
        var domains = resolver.SynthesizedTypes
            .Where(type => type.Kind == "String")
            .ToList();
        Assert.Equal(2, domains.Count);
        Assert.Contains(domains, domain =>
            domain.Source.Contains("Value = \"text\"", StringComparison.Ordinal));
        Assert.Contains(domains, domain =>
            domain.Source.Contains("Value = \"count\"", StringComparison.Ordinal));
        Assert.All(
            result.MemberOutcomes.Where(outcome => outcome.Name == "read"),
            outcome => Assert.Equal(MemberOutcomeStatus.Projected, outcome.Status));
    }

    [Fact]
    public void InterfaceEmitter_MergedReturnOnlyOverloads_EmitTypedUnion()
    {
        var resolver = new TypeResolver([]);
        var declarations = new[]
        {
            MakeInterfaceDeclaration(
                "ReturnUnionHost",
                0,
                [MakeMethod(0, "read", [], new KeywordTypeNode("StringKeyword"))]),
            MakeInterfaceDeclaration(
                "ReturnUnionHost",
                1,
                [MakeMethod(0, "read", [], new KeywordTypeNode("NumberKeyword"))]),
        };
        var symbol = MakeInterfaceSymbol(
            "ReturnUnionHost",
            [],
            declarations: declarations);

        var result = new InterfaceEmitter(
            resolver,
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Equal(1, result.Source.Split(" Read(").Length - 1);
        var union = Assert.Single(
            resolver.SynthesizedTypes,
            type => type.Kind == "Union");
        Assert.Contains(union.Name, result.Source);
        Assert.EndsWith("StringOrNumberUnion", union.Name);
        Assert.Equal(
            2,
            result.MemberOutcomes.Count(outcome =>
                outcome.Name == "read"
                && outcome.Status == MemberOutcomeStatus.Projected));
    }

    [Fact]
    public void InterfaceEmitter_NormalizedJavaScriptOperationCollision_FailsClosed()
    {
        var symbol = MakeInterfaceSymbol(
            "NormalizedHost",
            [
                MakeMethod(0, "readValue", []),
                MakeMethod(1, "ReadValue", []),
            ]);

        var exception = Assert.Throws<InterfaceEmitException>(() =>
            new InterfaceEmitter(
                new TypeResolver([]),
                "1.0.0",
                "Blazor.DOM").Emit(symbol));

        Assert.Contains("JavaScript operations", exception.Message);
        Assert.Contains("normalize to the same CLR signature", exception.Message);
    }

    [Fact]
    public void InterfaceEmitter_ClrIdenticalReturnTransportCollision_FailsClosed()
    {
        var jsonString = new KeywordTypeNode("StringKeyword")
        {
            Transport = new TransportModel(
                "json-value",
                false,
                "string",
                false,
                true,
                null),
        };
        var binaryString = new KeywordTypeNode("StringKeyword")
        {
            Transport = new TransportModel(
                "binary",
                false,
                "EncodedString",
                false,
                false,
                null),
        };
        var symbol = MakeInterfaceSymbol(
            "TransportHost",
            [
                MakeMethod(0, "read", [], jsonString),
                MakeMethod(1, "read", [], binaryString),
            ]);

        var exception = Assert.Throws<InterfaceEmitException>(() =>
            new InterfaceEmitter(
                new TypeResolver([]),
                "1.0.0",
                "Blazor.DOM").Emit(symbol));

        Assert.Contains("CLR-identical return type", exception.Message);
        Assert.Contains("incompatible return transports", exception.Message);
    }

    [Fact]
    public void InterfaceEmitter_LiteralDispatchOrdering_IsDeterministic()
    {
        var symbol = MakeInterfaceSymbol(
            "OrderedDispatchHost",
            [
                MakeMethod(
                    0,
                    "open",
                    [MakeParameter(
                        "kind",
                        new LiteralTypeNode("StringLiteral", "\"first\""))],
                    new KeywordTypeNode("StringKeyword")),
                MakeMethod(
                    1,
                    "open",
                    [MakeParameter(
                        "kind",
                        new LiteralTypeNode("StringLiteral", "\"second\""))],
                    new KeywordTypeNode("NumberKeyword")),
            ]);
        var firstResolver = new TypeResolver([]);
        var secondResolver = new TypeResolver([]);

        var first = new InterfaceEmitter(
            firstResolver,
            "1.0.0",
            "Blazor.DOM").Emit(symbol);
        var second = new InterfaceEmitter(
            secondResolver,
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Equal(first.Source, second.Source);
        Assert.Equal(
            firstResolver.SynthesizedTypes.Select(type => (type.Name, type.Source)),
            secondResolver.SynthesizedTypes.Select(type => (type.Name, type.Source)));
    }

    [Fact]
    public void InterfaceEmitter_HeritageFailure_AccountsEveryMemberAsNotAttempted()
    {
        var symbol = MakeInterfaceSymbol(
            "BrokenHeritage",
            [
                MakeMethod(0, "first", []),
                MakeMethod(1, "second", []),
                MakeMethod(2, "third", []),
            ],
            heritage:
            [
                new HeritageClauseModel(
                    "extends",
                    [new HeritageReferenceTypeNode("MissingBase", null, [])])
            ]);

        var exception = Assert.Throws<InterfaceEmitException>(
            () => new InterfaceEmitter(
                new TypeResolver([]),
                "1.0.0",
                "Blazor.DOM").Emit(symbol));

        Assert.Equal(3, exception.PartialOutcomes.Count);
        Assert.All(
            exception.PartialOutcomes,
            outcome =>
            {
                Assert.Equal(
                    MemberOutcomeStatus.NotAttemptedAfterFailure,
                    outcome.Status);
                Assert.Contains("Not attempted", outcome.Reason);
                Assert.Contains("BrokenHeritage/decl[0]/member[", outcome.Provenance);
                Assert.StartsWith("fixture.ts:", outcome.SourceLocation);
            });

        var output = CreateTempDirectory();
        try
        {
            var pipelineResult = GenerationPipeline.Run(
                new IrBundle(CreateDummyManifest(), [symbol], []),
                output);
            Assert.Equal(3, pipelineResult.Manifest.Accounting.FailedMembers);
            Assert.Equal(
                3,
                pipelineResult.Manifest.Accounting.NotAttemptedAfterFailureMembers);
            Assert.All(
                pipelineResult.Manifest.Accounting.FailedSymbolMemberOutcomes ?? [],
                outcome => Assert.Equal(
                    nameof(MemberOutcomeStatus.NotAttemptedAfterFailure),
                    outcome.Status));
            Assert.Empty(pipelineResult.Manifest.Accounting.FailedMemberEntries);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    public static TheoryData<int> FailurePositions => new()
    {
        0,
        1,
        2,
    };

    [Theory]
    [MemberData(nameof(FailurePositions))]
    public void InterfaceEmitter_EarlyMiddleLateFailure_ReconcilesEverySourceMember(
        int failureOrdinal)
    {
        var members = Enumerable.Range(0, 3)
            .Select(ordinal => ordinal == failureOrdinal
                ? MakeMethod(
                    ordinal,
                    $"method{ordinal}",
                    [MakeParameter("value", new ReferenceTypeNode("MissingType", null, []))])
                : MakeMethod(ordinal, $"method{ordinal}", []))
            .ToList();
        var symbol = MakeInterfaceSymbol("FailureHost", members);

        var exception = Assert.Throws<InterfaceEmitException>(
            () => new InterfaceEmitter(
                new TypeResolver([]),
                "1.0.0",
                "Blazor.DOM").Emit(symbol));

        Assert.Equal(3, exception.PartialOutcomes.Count);
        Assert.Equal(
            MemberOutcomeStatus.Failed,
            exception.PartialOutcomes.Single(
                outcome => outcome.Ordinal == failureOrdinal).Status);
        Assert.Equal(
            2,
            exception.PartialOutcomes.Count(
                outcome => outcome.Status == MemberOutcomeStatus.Projected));
        Assert.Equal(
            3,
            exception.PartialOutcomes
                .Select(outcome => (outcome.DeclarationOrdinal, outcome.Ordinal))
                .Distinct()
                .Count());
    }

    [Fact]
    public void InterfaceEmitter_StandaloneSetter_ProjectsIntentionalSetterMethod()
    {
        var setter = new MemberModel(
            0,
            "setter",
            new NameNode("identifier", "value"),
            false,
            false,
            false,
            [],
            [MakeParameter("value", new KeywordTypeNode("StringKeyword"))],
            null,
            null,
            new DocumentationModel("", [], false),
            MakeLocation(10));
        var symbol = MakeInterfaceSymbol(
            "SetterHost",
            [setter, MakeMethod(1, "afterSetter", [])]);

        var result = new InterfaceEmitter(
                new TypeResolver([]),
                "1.0.0",
                "Blazor.DOM").Emit(symbol);

        Assert.Contains("void SetValue(string value);", result.Source);
        Assert.Equal(2, result.MemberOutcomes.Count);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            result.MemberOutcomes.Single(outcome => outcome.Ordinal == 0).Status);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            result.MemberOutcomes.Single(outcome => outcome.Ordinal == 1).Status);
    }

    [Fact]
    public void FailedSymbolManifest_PreservesAllStatusesProvenanceAndMemberTotals()
    {
        var symbol = MakeInterfaceSymbol(
            "ManifestHost",
            [
                MakeProperty(0, "name", new KeywordTypeNode("StringKeyword")),
                MakeMethod(
                    1,
                    "broken",
                    [MakeParameter("value", new ReferenceTypeNode("MissingType", null, []))]),
                new MemberModel(
                    2,
                    "indexSignature",
                    null,
                    false,
                    false,
                    false,
                    [],
                    [],
                    null,
                    null,
                    new DocumentationModel("", [], false),
                    MakeLocation(12)),
            ]);
        var output = CreateTempDirectory();

        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateDummyManifest(), [symbol], []),
                output);
            var accounting = result.Manifest.Accounting;

            Assert.True(result.Validation.MemberReconciliationValid);
            Assert.Equal(3, result.Validation.ActualMemberCount);
            Assert.Equal(3, result.Validation.ExpectedMemberCount);
            Assert.Equal(3, accounting.TotalMembers);
            Assert.Equal(1, accounting.ProjectedMembers);
            Assert.Equal(0, accounting.DeferredMembers);
            Assert.Equal(2, accounting.FailedMembers);
            Assert.Equal(3, accounting.ExpectedMembers);
            Assert.True(accounting.MemberReconciliationValid);

            var entries = Assert.IsAssignableFrom<IReadOnlyList<FailedSymbolMemberOutcomeEntry>>(
                accounting.FailedSymbolMemberOutcomes);
            Assert.Equal(3, entries.Count);
            Assert.Contains(entries, entry => entry.Status == nameof(MemberOutcomeStatus.Projected));
            Assert.Contains(entries, entry => entry.Status == nameof(MemberOutcomeStatus.Failed));
            Assert.All(entries, entry =>
            {
                Assert.Contains("ManifestHost/decl[0]/member[", entry.Provenance);
                Assert.StartsWith("fixture.ts:", entry.SourceLocation);
            });
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void Accounting_ReconcilesEveryDeclarationMemberAndSourceOverload()
    {
        var projected = MakeInterfaceSymbol(
            "ProjectedHost",
            [
                MakeProperty(0, "name", new KeywordTypeNode("StringKeyword")),
                MakeMethod(1, "read", []),
            ]);
        var failed = MakeInterfaceSymbol(
            "FailedHost",
            [
                MakeMethod(
                    0,
                    "broken",
                    [MakeParameter("value", new ReferenceTypeNode("MissingType", null, []))]),
            ]);
        var output = CreateTempDirectory();

        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateDummyManifest(), [projected, failed], []),
                output);
            var validation = result.Validation;
            var accounting = result.Manifest.Accounting;

            Assert.True(validation.IsValid);
            Assert.True(validation.DeclarationReconciliationValid);
            Assert.True(validation.MemberReconciliationValid);
            Assert.True(validation.OverloadReconciliationValid);
            Assert.True(validation.ParameterReconciliationValid);
            Assert.Equal((2, 2), (
                validation.ActualDeclarationCount,
                validation.ExpectedDeclarationCount));
            Assert.Equal((3, 3), (
                validation.ActualMemberCount,
                validation.ExpectedMemberCount));
            Assert.Equal((2, 2), (
                validation.ActualOverloadCount,
                validation.ExpectedOverloadCount));
            Assert.Equal((1, 1), (
                validation.ActualParameterCount,
                validation.ExpectedParameterCount));
            Assert.Equal(accounting.SourceDeclarations, accounting.AccountedSourceDeclarations);
            Assert.Equal(accounting.SourceMembers, accounting.AccountedSourceMembers);
            Assert.Equal(accounting.SourceOverloads, accounting.AccountedSourceOverloads);
            Assert.Equal(accounting.SourceParameters, accounting.AccountedSourceParameters);
            Assert.Equal(2, accounting.SourceDeclarationEntries?.Count);
            Assert.Equal(3, accounting.SourceMemberEntries?.Count);
            Assert.All(accounting.SourceMemberEntries ?? [], entry =>
            {
                Assert.Contains($"/decl[{entry.DeclarationOrdinal}]/member[", entry.Provenance);
                Assert.StartsWith("fixture.ts:", entry.SourceLocation);
            });
            Assert.Contains(
                result.Manifest.Diagnostics,
                diagnostic => diagnostic.Code == "GENERATION_FAILED"
                    && diagnostic.Message.StartsWith("FailedHost:", StringComparison.Ordinal));

            var json = File.ReadAllText(Path.Combine(output, "emitter-manifest.json"));
            Assert.Contains("\"status\": \"Projected\"", json);
            Assert.Contains("\"status\": \"Failed\"", json);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void GlobalFunctionDeclarations_EmitEveryOverloadAndReconcileParameters()
    {
        var symbol = new SymbolModel(
            0,
            "overloadedGlobal",
            0,
            [
                MakeGlobalFunctionDeclaration(
                    "overloadedGlobal",
                    0,
                    [MakeParameter("first", new KeywordTypeNode("StringKeyword"))]),
                MakeGlobalFunctionDeclaration(
                    "overloadedGlobal",
                    3,
                    [
                        MakeParameter("first", new KeywordTypeNode("StringKeyword"))
                            with { Ordinal = 0 },
                        MakeParameter("second", new KeywordTypeNode("NumberKeyword"))
                            with { Ordinal = 1 },
                    ]),
            ],
            true,
            new SemanticModel(
                "matched",
                "overloadedGlobal",
                "definition",
                null,
                ["globalFunction"],
                [],
                [],
                false,
                false,
                [],
                false,
                false,
                false,
                [],
                []));
        var output = CreateTempDirectory();
        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateDummyManifest(), [symbol], []),
                output);
            var accounting = result.Manifest.Accounting;

            Assert.True(result.Validation.IsValid);
            Assert.True(result.Validation.ParameterReconciliationValid);
            Assert.Equal((2, 2), (
                accounting.AccountedSourceDeclarations,
                accounting.SourceDeclarations));
            Assert.Equal((0, 0), (
                accounting.AccountedSourceMembers,
                accounting.SourceMembers));
            Assert.Equal((2, 2), (
                accounting.AccountedSourceOverloads,
                accounting.SourceOverloads));
            Assert.Equal((3, 3), (
                accounting.AccountedSourceParameters,
                accounting.SourceParameters));

            Assert.All(accounting.SourceDeclarationEntries ?? [], declaration =>
            {
                Assert.Equal(nameof(MemberOutcomeStatus.Projected), declaration.Status);
                Assert.Null(declaration.Phase);
            });
            var globalContract = File.ReadAllText(Path.Combine(
                output,
                "Globals",
                "IWindow.Globals.g.cs"));
            Assert.Contains(
                "void OverloadedGlobal(string first);",
                globalContract);
            Assert.Contains(
                "void OverloadedGlobal(string first, double second);",
                globalContract);
            var overloads = accounting.SourceOverloadEntries ?? [];
            Assert.Equal(2, overloads.Count);
            Assert.Equal(
                [1, 2],
                overloads
                    .OrderBy(overload => overload.DeclarationOrdinal)
                    .Select(overload => overload.ParameterOutcomes.Count)
                    .ToArray());
            Assert.All(overloads, overload =>
            {
                Assert.Equal("globalFunction", overload.Kind);
                Assert.Equal(nameof(MemberOutcomeStatus.Projected), overload.Status);
                Assert.Null(overload.Phase);
                Assert.All(overload.ParameterOutcomes, parameter =>
                {
                    Assert.Equal(MemberOutcomeStatus.Projected, parameter.Status);
                    Assert.Null(parameter.Phase);
                    Assert.Contains(
                        $"/decl[{overload.DeclarationOrdinal}]/globalFunction/overload/" +
                        $"parameter[{parameter.Ordinal}]/{parameter.Name}",
                        parameter.Provenance);
                });
            });
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void DictionaryEmitter_AccountsProjectedDeferredFailedAndUntouchedMembersExactly()
    {
        var successful = MakeInterfaceSymbol(
            "DictionaryHost",
            [
                MakeProperty(0, "name", new KeywordTypeNode("StringKeyword")),
                MakeProperty(1, "omitted", new KeywordTypeNode("UndefinedKeyword")),
            ],
            classification: "dictionary");
        var emitter = new DictionaryEmitter(
            new TypeResolver([successful]),
            "1.0.0",
            "Blazor.DOM");

        var emitted = emitter.EmitWithOutcomes(successful);
        Assert.Equal(2, emitted.MemberOutcomes.Count);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            emitted.MemberOutcomes.Single(outcome => outcome.Ordinal == 0).Status);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            emitted.MemberOutcomes.Single(outcome => outcome.Ordinal == 1).Status);
        Assert.Null(
            emitted.MemberOutcomes.Single(outcome => outcome.Ordinal == 1).Phase);
        Assert.Single(emitted.DeclarationOutcomes ?? []);

        var failed = MakeInterfaceSymbol(
            "BrokenDictionary",
            [
                MakeProperty(0, "broken", new ReferenceTypeNode("MissingType", null, [])),
                MakeProperty(1, "later", new KeywordTypeNode("StringKeyword")),
            ],
            classification: "dictionary");
        var exception = Assert.Throws<DictionaryEmitException>(
            () => new DictionaryEmitter(
                new TypeResolver([failed]),
                "1.0.0",
                "Blazor.DOM").EmitWithOutcomes(failed));

        Assert.Equal(2, exception.PartialOutcomes.Count);
        Assert.Equal(
            MemberOutcomeStatus.Failed,
            exception.PartialOutcomes.Single(outcome => outcome.Ordinal == 0).Status);
        Assert.Equal(
            MemberOutcomeStatus.NotAttemptedAfterFailure,
            exception.PartialOutcomes.Single(outcome => outcome.Ordinal == 1).Status);
    }

    [Fact]
    public void DictionaryEmitter_UnsupportedKindFailsActualMemberAndPreservesPartialOutcomes()
    {
        var symbol = MakeInterfaceSymbol(
            "UnsupportedDictionary",
            [
                MakeProperty(0, "first", new KeywordTypeNode("StringKeyword")),
                MakeMethod(1, "notAProperty", []),
                MakeProperty(2, "later", new KeywordTypeNode("StringKeyword")),
            ],
            classification: "dictionary");

        var exception = Assert.Throws<DictionaryEmitException>(
            () => new DictionaryEmitter(
                new TypeResolver([symbol]),
                "1.0.0",
                "Blazor.DOM").EmitWithOutcomes(symbol));

        Assert.Equal(3, exception.PartialOutcomes.Count);
        Assert.Equal(
            [
                MemberOutcomeStatus.Projected,
                MemberOutcomeStatus.Failed,
                MemberOutcomeStatus.NotAttemptedAfterFailure,
            ],
            exception.PartialOutcomes
                .OrderBy(outcome => outcome.Ordinal)
                .Select(outcome => outcome.Status)
                .ToArray());
        Assert.Equal(
            MemberOutcomeStatus.Failed,
            Assert.Single(exception.PartialDeclarationOutcomes).Status);
        var overload = Assert.Single(exception.PartialOverloadOutcomes);
        Assert.Equal(MemberOutcomeStatus.Failed, overload.Status);
    }

    [Fact]
    public void CallbackEmitter_FailureMarksTheActualCallSignature()
    {
        var callSignature = new MemberModel(
            0,
            "callSignature",
            null,
            false,
            false,
            false,
            [],
            [],
            null,
            new ReferenceTypeNode("MissingReturn", null, []),
            new DocumentationModel("", [], false),
            MakeLocation(10));
        var symbol = MakeInterfaceSymbol(
            "BrokenCallback",
            [callSignature],
            classification: "callback");

        var exception = Assert.Throws<CallbackEmitException>(
            () => new CallbackEmitter(
                new TypeResolver([symbol]),
                "1.0.0",
                "Blazor.DOM").EmitWithOutcomes(symbol));

        var outcome = Assert.Single(exception.PartialOutcomes);
        Assert.Equal(MemberOutcomeStatus.Failed, outcome.Status);
        Assert.Equal("callSignature", outcome.Kind);
        Assert.Contains("BrokenCallback/decl[0]/member[0]", outcome.Provenance);
    }

    [Fact]
    public void CallbackPipeline_ParameterFailurePreservesQualifiedPartialOutcomes()
    {
        var callSignature = new MemberModel(
            0,
            "callSignature",
            null,
            false,
            false,
            false,
            [],
            [
                MakeParameter("first", new KeywordTypeNode("StringKeyword")) with { Ordinal = 0 },
                MakeParameter("broken", new ReferenceTypeNode("MissingType", null, [])) with { Ordinal = 1 },
                MakeParameter("later", new KeywordTypeNode("StringKeyword")) with { Ordinal = 2 },
            ],
            null,
            new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            MakeLocation(10));
        var symbol = MakeInterfaceSymbol(
            "BrokenCallbackParameters",
            [callSignature],
            classification: "callback");
        var output = CreateTempDirectory();
        try
        {
            var result = GenerationPipeline.Run(
                new IrBundle(CreateDummyManifest(), [symbol], []),
                output);

            var overload = Assert.Single(
                result.Manifest.Accounting.SourceOverloadEntries ?? []);
            Assert.Equal(MemberOutcomeStatus.Failed.ToString(), overload.Status);
            Assert.Equal(
                [
                    MemberOutcomeStatus.Projected,
                    MemberOutcomeStatus.Failed,
                    MemberOutcomeStatus.NotAttemptedAfterFailure,
                ],
                overload.ParameterOutcomes
                    .OrderBy(outcome => outcome.Ordinal)
                    .Select(outcome => outcome.Status)
                    .ToArray());
            Assert.All(
                overload.ParameterOutcomes,
                outcome => Assert.Contains(
                    $"BrokenCallbackParameters/decl[0]/member[0]/callSignature",
                    outcome.Provenance));

            var member = Assert.Single(
                result.Manifest.Accounting.SourceMemberEntries ?? []);
            Assert.Equal(MemberOutcomeStatus.Failed.ToString(), member.Status);
            var declaration = Assert.Single(
                result.Manifest.Accounting.SourceDeclarationEntries ?? []);
            Assert.Equal(MemberOutcomeStatus.Failed.ToString(), declaration.Status);
            Assert.Equal(1, result.Manifest.Accounting.GenerationFailed);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void InterfaceEmitter_MergedDeclarations_ReconcilesQualifiedMemberIdentity()
    {
        var declaration0 = MakeInterfaceDeclaration(
            "MergedHost",
            0,
            [MakeMethod(0, "first", [])]);
        var declaration4 = MakeInterfaceDeclaration(
            "MergedHost",
            4,
            [
                MakeMethod(
                    0,
                    "broken",
                    [MakeParameter("value", new ReferenceTypeNode("MissingType", null, []))])
            ]);
        var symbol = MakeInterfaceSymbol(
            "MergedHost",
            [],
            declarations: [declaration0, declaration4]);

        var exception = Assert.Throws<InterfaceEmitException>(
            () => new InterfaceEmitter(
                new TypeResolver([]),
                "1.0.0",
                "Blazor.DOM").Emit(symbol));

        Assert.Equal(2, exception.PartialOutcomes.Count);
        Assert.Equal(
            [(0, 0), (4, 0)],
            exception.PartialOutcomes
                .Select(outcome => (outcome.DeclarationOrdinal, outcome.Ordinal))
                .ToArray());
    }

    [Fact]
    public void ExhaustivePromotion_PreservesMetadataAndProfiles_AndRemovesStaleOwnedFiles()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteExhaustiveStaging(staging);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IStale.g.cs"),
                "stale\n");
            WriteFile(
                Path.Combine(canonical, "AdvancedTypes", "StaleShape.g.cs"),
                "stale\n");

            var attributes = File.ReadAllBytes(Path.Combine(canonical, ".gitattributes"));
            var wakeLock = File.ReadAllBytes(
                Path.Combine(canonical, "Profiles", "WakeLock", "keep.g.cs"));
            var reviewed = File.ReadAllBytes(
                Path.Combine(canonical, "reviewed", "metadata.json"));
            var nestedReviewed = File.ReadAllBytes(
                Path.Combine(canonical, "reviewed", "nested", "metadata.json"));

            OutputPromotion.PromoteExhaustive(staging, canonical);

            Assert.Equal(attributes, File.ReadAllBytes(Path.Combine(canonical, ".gitattributes")));
            Assert.Equal(
                wakeLock,
                File.ReadAllBytes(Path.Combine(canonical, "Profiles", "WakeLock", "keep.g.cs")));
            Assert.Equal(
                reviewed,
                File.ReadAllBytes(Path.Combine(canonical, "reviewed", "metadata.json")));
            Assert.Equal(
                nestedReviewed,
                File.ReadAllBytes(
                    Path.Combine(canonical, "reviewed", "nested", "metadata.json")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IStale.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "AdvancedTypes", "StaleShape.g.cs")));
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExhaustiveThenProfilePromotion_PreservesRootAndUnrelatedProfileMetadata()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var exhaustiveStaging = Path.Combine(root, "exhaustive-staging");
        var profileStaging = Path.Combine(root, "profile-staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Profiles", "Reviewed", "notes.txt"),
                "reviewed profile metadata\n");
            WriteExhaustiveStaging(exhaustiveStaging);
            WriteFile(
                Path.Combine(profileStaging, "Interfaces", "IWakeLock.g.cs"),
                "fresh wake lock\n");
            WriteFile(
                Path.Combine(profileStaging, "emitter-manifest.json"),
                "{\"profile\":true}\n");
            WriteFile(
                Path.Combine(profileStaging, "profile-coverage.json"),
                "{\"byteIdentityVerified\":true}\n");

            var attributes = File.ReadAllBytes(Path.Combine(canonical, ".gitattributes"));
            var reviewed = File.ReadAllBytes(
                Path.Combine(canonical, "reviewed", "metadata.json"));
            var reviewedProfile = File.ReadAllBytes(
                Path.Combine(canonical, "Profiles", "Reviewed", "notes.txt"));

            OutputPromotion.PromoteExhaustive(exhaustiveStaging, canonical);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Profiles", "WakeLock", "keep.g.cs")));

            OutputPromotion.PromoteProfile(
                profileStaging,
                Path.Combine(canonical, "Profiles", "WakeLock"));

            Assert.Equal(attributes, File.ReadAllBytes(Path.Combine(canonical, ".gitattributes")));
            Assert.Equal(
                reviewed,
                File.ReadAllBytes(Path.Combine(canonical, "reviewed", "metadata.json")));
            Assert.Equal(
                reviewedProfile,
                File.ReadAllBytes(
                    Path.Combine(canonical, "Profiles", "Reviewed", "notes.txt")));
            Assert.True(File.Exists(
                Path.Combine(canonical, "Profiles", "WakeLock", "Interfaces", "IWakeLock.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Profiles", "WakeLock", "keep.g.cs")));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExhaustivePromotion_RejectsUnownedStagingAndLeavesCanonicalUntouched()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteExhaustiveStaging(staging);
            WriteFile(Path.Combine(staging, "reviewed-metadata.json"), "{}\n");
            var before = SnapshotTree(canonical);

            Assert.Throws<InvalidOperationException>(
                () => OutputPromotion.PromoteExhaustive(staging, canonical));

            AssertTreesEqual(before, SnapshotTree(canonical));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public static TheoryData<OutputPromotionFailurePoint> ExhaustiveFailurePoints => new()
    {
        OutputPromotionFailurePoint.BeforePreservedTreeCopy,
        OutputPromotionFailurePoint.AfterPreservedTreeCopy,
        OutputPromotionFailurePoint.BeforeOwnedContentDeletion,
        OutputPromotionFailurePoint.AfterOwnedContentDeletion,
        OutputPromotionFailurePoint.BeforeStagingCopy,
        OutputPromotionFailurePoint.AfterStagingCopy,
        OutputPromotionFailurePoint.BeforeCanonicalSwap,
        OutputPromotionFailurePoint.AfterCanonicalBackupMove,
        OutputPromotionFailurePoint.AfterCandidatePromotion,
    };

    [Theory]
    [MemberData(nameof(ExhaustiveFailurePoints))]
    public void ExhaustivePromotion_InjectedFailure_RestoresByteIdenticalCanonical(
        OutputPromotionFailurePoint failurePoint)
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);
            var before = SnapshotTree(canonical);

            Assert.Throws<InjectedPromotionException>(
                () => OutputPromotion.PromoteExhaustive(
                    staging,
                    canonical,
                    point =>
                    {
                        if (point == failurePoint)
                            throw new InjectedPromotionException(point);
                    }));

            AssertTreesEqual(before, SnapshotTree(canonical));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public static TheoryData<OutputPromotionFailurePoint> PostCommitFailurePoints => new()
    {
        OutputPromotionFailurePoint.AfterPostPromotionVerification,
        OutputPromotionFailurePoint.AfterPromotionCommit,
        OutputPromotionFailurePoint.BeforeBackupDeletion,
    };

    [Theory]
    [MemberData(nameof(PostCommitFailurePoints))]
    public void ExhaustivePromotion_FailureAfterCommit_KeepsVerifiedCanonicalAndOriginalBackup(
        OutputPromotionFailurePoint failurePoint)
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);
            var before = SnapshotTree(canonical);

            var exception = Assert.Throws<OutputPromotionCleanupException>(
                () => OutputPromotion.PromoteExhaustive(
                    staging,
                    canonical,
                    point =>
                    {
                        if (point == failurePoint)
                            throw new InjectedPromotionException(point);
                    }));
            Assert.IsType<InjectedPromotionException>(exception.InnerException);

            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs")));
            Assert.True(File.Exists(Path.Combine(canonical, ".gitattributes")));
            Assert.True(File.Exists(
                Path.Combine(canonical, "Profiles", "WakeLock", "keep.g.cs")));

            var debris = Directory
                .EnumerateDirectories(
                    root,
                    $".{Path.GetFileName(canonical)}.*",
                    SearchOption.TopDirectoryOnly)
                .ToList();
            var backup = Assert.Single(debris);
            Assert.Contains(".backup-", Path.GetFileName(backup), StringComparison.Ordinal);
            AssertTreesEqual(before, SnapshotTree(backup));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExhaustivePromotion_RecursiveBackupCleanupFailure_PreservesVerifiedCanonicalAndDebris()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);
            var deletionCallbacks = 0;

            var exception = Assert.Throws<OutputPromotionCleanupException>(
                () => OutputPromotion.PromoteExhaustive(
                    staging,
                    canonical,
                    point =>
                    {
                        if (point == OutputPromotionFailurePoint.DuringBackupDeletion
                            && ++deletionCallbacks == 2)
                        {
                            throw new InjectedPromotionException(point);
                        }
                    }));

            Assert.IsType<InjectedPromotionException>(exception.InnerException);
            Assert.Equal(2, deletionCallbacks);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs")));
            Assert.True(File.Exists(Path.Combine(canonical, ".gitattributes")));
            Assert.True(File.Exists(
                Path.Combine(canonical, "Profiles", "WakeLock", "keep.g.cs")));

            var backup = Assert.Single(Directory.EnumerateDirectories(
                root,
                $".{Path.GetFileName(canonical)}.backup-*",
                SearchOption.TopDirectoryOnly));
            Assert.NotEmpty(Directory.EnumerateFileSystemEntries(
                backup,
                "*",
                SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustivePromotion_TransientWindowsCleanupFailures_AreRetried(
        bool unauthorized)
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);
            var failuresRemaining = 2;

            OutputPromotion.PromoteExhaustive(
                staging,
                canonical,
                point =>
                {
                    if (point != OutputPromotionFailurePoint.DuringBackupDeletion
                        || failuresRemaining-- <= 0)
                    {
                        return;
                    }

                    if (unauthorized)
                        throw new UnauthorizedAccessException("Injected Windows access failure.");
                    throw new IOException("Injected Windows sharing violation.");
                });

            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs")));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustivePromotion_TransientOwnedFileDeletionFailures_AreRetried(
        bool unauthorized)
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteExhaustiveStaging(staging);
            var failuresRemaining = 2;

            OutputPromotion.PromoteExhaustive(
                staging,
                canonical,
                failureInjector: point =>
                {
                    if (point != OutputPromotionFailurePoint.DuringOwnedFileDeletion
                        || failuresRemaining-- <= 0)
                    {
                        return;
                    }

                    if (unauthorized)
                        throw new UnauthorizedAccessException(
                            "Injected Windows access failure.");
                    throw new IOException("Injected Windows sharing violation.");
                },
                retryDelay: _ => { });

            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.Contains(
                "\"fresh\":true",
                File.ReadAllText(Path.Combine(
                    canonical,
                    "emitter-manifest.json")));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExhaustivePromotion_PersistentCanonicalMoveFailure_UsesVerifiedFileFallback(
        bool canonicalHasFiles,
        bool unauthorized)
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            Directory.CreateDirectory(canonical);
            if (canonicalHasFiles)
            {
                WriteCanonicalStaticTree(canonical);
                WriteFile(
                    Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                    "old\n");
            }
            WriteExhaustiveStaging(staging);
            var attempts = 0;

            OutputPromotion.PromoteExhaustive(
                staging,
                canonical,
                failureInjector: point =>
                {
                    if (point != OutputPromotionFailurePoint.DuringCanonicalDirectoryMove)
                        return;

                    attempts++;
                    if (unauthorized)
                    {
                        throw new UnauthorizedAccessException(
                            "Injected persistent Windows directory access failure.");
                    }
                    throw new IOException(
                        "Injected persistent Windows directory sharing violation.");
                },
                retryDelay: _ => { });

            Assert.Equal(3, attempts);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs")));
            if (canonicalHasFiles)
                Assert.True(File.Exists(Path.Combine(canonical, ".gitattributes")));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustivePromotion_PersistentCandidateMoveFailure_UsesVerifiedFileFallback(
        bool unauthorized)
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteExhaustiveStaging(staging);
            var attempts = 0;

            OutputPromotion.PromoteExhaustive(
                staging,
                canonical,
                failureInjector: point =>
                {
                    if (point != OutputPromotionFailurePoint.DuringCandidateDirectoryMove)
                        return;

                    attempts++;
                    if (unauthorized)
                    {
                        throw new UnauthorizedAccessException(
                            "Injected persistent Windows directory access failure.");
                    }
                    throw new IOException(
                        "Injected persistent Windows directory sharing violation.");
                },
                retryDelay: _ => { });

            Assert.Equal(3, attempts);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.Contains(
                "\"fresh\":true",
                File.ReadAllText(Path.Combine(
                    canonical,
                    "emitter-manifest.json")));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExhaustivePromotion_FileFallbackFailure_RestoresByteIdenticalCanonical()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);
            var before = SnapshotTree(canonical);

            Assert.Throws<InjectedPromotionException>(
                () => OutputPromotion.PromoteExhaustive(
                    staging,
                    canonical,
                    failureInjector: point =>
                    {
                        if (point == OutputPromotionFailurePoint.DuringCanonicalDirectoryMove)
                        {
                            throw new IOException(
                                "Injected persistent Windows directory sharing violation.");
                        }
                        if (point == OutputPromotionFailurePoint.AfterCandidatePromotion)
                            throw new InjectedPromotionException(point);
                    },
                    retryDelay: _ => { }));

            AssertTreesEqual(before, SnapshotTree(canonical));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExhaustivePromotion_IdenticalCanonical_SkipsDirectorySwap()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteExhaustiveStaging(staging);
            OutputPromotion.PromoteExhaustive(staging, canonical);
            WriteExhaustiveStaging(staging);
            var moveAttempts = 0;

            OutputPromotion.PromoteExhaustive(
                staging,
                canonical,
                failureInjector: point =>
                {
                    if (point == OutputPromotionFailurePoint.DuringCanonicalDirectoryMove)
                        moveAttempts++;
                });

            Assert.Equal(0, moveAttempts);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            AssertNoPromotionDebris(root, canonical);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustivePromotion_PersistentWindowsCleanupFailure_PreservesCanonical(
        bool unauthorized)
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);
            var attempts = 0;

            var exception = Assert.Throws<OutputPromotionCleanupException>(
                () => OutputPromotion.PromoteExhaustive(
                    staging,
                    canonical,
                    point =>
                    {
                        if (point != OutputPromotionFailurePoint.DuringBackupDeletion)
                            return;

                        attempts++;
                        if (unauthorized)
                            throw new UnauthorizedAccessException(
                                "Injected persistent Windows access failure.");
                        throw new IOException(
                            "Injected persistent Windows sharing violation.");
                    },
                    retryDelay: _ => { }));

            Assert.Equal(10, attempts);
            if (unauthorized)
                Assert.IsType<UnauthorizedAccessException>(exception.InnerException);
            else
                Assert.IsType<IOException>(exception.InnerException);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs")));
            Assert.Single(Directory.EnumerateDirectories(
                root,
                $".{Path.GetFileName(canonical)}.backup-*",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExhaustivePromotion_HandledPostCommitCleanupFailure_RemainsSuccessful()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);
            OutputPromotionCleanupException? cleanupFailure = null;

            OutputPromotion.PromoteExhaustive(
                staging,
                canonical,
                failureInjector: point =>
                {
                    if (point == OutputPromotionFailurePoint.BeforeBackupDeletion)
                        throw new InjectedPromotionException(point);
                },
                cleanupFailureHandler: exception => cleanupFailure = exception);

            Assert.NotNull(cleanupFailure);
            Assert.IsType<InjectedPromotionException>(cleanupFailure.InnerException);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs")));
            Assert.Single(Directory.EnumerateDirectories(
                root,
                $".{Path.GetFileName(canonical)}.backup-*",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExhaustivePromotion_DamagedRollbackBackup_DoesNotDiscardVerifiedCandidate()
    {
        var root = CreateTempDirectory();
        var canonical = Path.Combine(root, "Blazor.DOM.Generated");
        var staging = Path.Combine(root, "staging");
        try
        {
            WriteCanonicalStaticTree(canonical);
            WriteFile(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs"),
                "old\n");
            WriteExhaustiveStaging(staging);

            var exception = Assert.Throws<AggregateException>(
                () => OutputPromotion.PromoteExhaustive(
                    staging,
                    canonical,
                    point =>
                    {
                        if (point != OutputPromotionFailurePoint.AfterCandidatePromotion)
                            return;

                        var backup = Assert.Single(Directory.EnumerateDirectories(
                            root,
                            $".{Path.GetFileName(canonical)}.backup-*",
                            SearchOption.TopDirectoryOnly));
                        File.Delete(Path.Combine(backup, "Interfaces", "IOld.g.cs"));
                        throw new InjectedPromotionException(point);
                    }));

            Assert.Contains(
                exception.InnerExceptions,
                inner => inner is IOException
                    && inner.Message.Contains("not byte-identical", StringComparison.Ordinal));
            Assert.True(File.Exists(
                Path.Combine(canonical, "Interfaces", "IFresh.g.cs")));
            Assert.False(File.Exists(
                Path.Combine(canonical, "Interfaces", "IOld.g.cs")));
            Assert.Single(Directory.EnumerateDirectories(
                root,
                $".{Path.GetFileName(canonical)}.backup-*",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public static TheoryData<OutputPromotionFailurePoint> ProfileFailurePoints => new()
    {
        OutputPromotionFailurePoint.BeforeStagingCopy,
        OutputPromotionFailurePoint.AfterStagingCopy,
        OutputPromotionFailurePoint.BeforeCanonicalSwap,
        OutputPromotionFailurePoint.AfterCanonicalBackupMove,
        OutputPromotionFailurePoint.AfterCandidatePromotion,
    };

    [Theory]
    [MemberData(nameof(ProfileFailurePoints))]
    public void ProfilePipeline_InjectedPromotionFailure_RestoresByteIdenticalCanonical(
        OutputPromotionFailurePoint failurePoint)
    {
        var output = CreateTempDirectory();
        var canonical = Path.Combine(output, "Profiles", "TinyProfile");
        try
        {
            WriteFile(Path.Combine(canonical, "old.g.cs"), "old profile\n");
            WriteFile(
                Path.Combine(canonical, "profile-coverage.json"),
                "{\"old\":true}\n");
            var before = SnapshotTree(canonical);
            var profile = new ProfileDefinition(
                "TinyProfile",
                "fixture",
                ["TinyEnum"],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/TinyProfile");

            Assert.Throws<InjectedPromotionException>(
                () => ProfilePipeline.Run(
                    profile,
                    new IrBundle(
                        CreateDummyManifest(),
                        [MakeEnumSymbol("TinyEnum")],
                        []),
                    output,
                    promotionFailureInjector: point =>
                    {
                        if (point == failurePoint)
                            throw new InjectedPromotionException(point);
                    }));

            AssertTreesEqual(before, SnapshotTree(canonical));
            AssertNoPromotionDebris(Path.GetDirectoryName(canonical)!, canonical);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ProfilePipeline_RecursiveBackupCleanupFailure_KeepsVerifiedCanonical()
    {
        var output = CreateTempDirectory();
        var canonical = Path.Combine(output, "Profiles", "TinyProfile");
        try
        {
            WriteFile(Path.Combine(canonical, "old.g.cs"), "old profile\n");
            WriteFile(
                Path.Combine(canonical, "profile-coverage.json"),
                "{\"old\":true}\n");
            var profile = new ProfileDefinition(
                "TinyProfile",
                "fixture",
                ["TinyEnum"],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/TinyProfile");
            var deletionCallbacks = 0;

            var exception = Assert.Throws<OutputPromotionCleanupException>(
                () => ProfilePipeline.Run(
                    profile,
                    new IrBundle(
                        CreateDummyManifest(),
                        [MakeEnumSymbol("TinyEnum")],
                        []),
                    output,
                    promotionFailureInjector: point =>
                    {
                        if (point == OutputPromotionFailurePoint.DuringBackupDeletion
                            && ++deletionCallbacks == 1)
                        {
                            throw new InjectedPromotionException(point);
                        }
                    }));

            Assert.IsType<InjectedPromotionException>(exception.InnerException);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Enums", "TinyEnum.g.cs")));
            Assert.False(File.Exists(Path.Combine(canonical, "old.g.cs")));
            Assert.Contains(
                "\"byteIdentityVerified\": true",
                File.ReadAllText(Path.Combine(canonical, "profile-coverage.json")));
            Assert.Single(Directory.EnumerateDirectories(
                Path.GetDirectoryName(canonical)!,
                $".{Path.GetFileName(canonical)}.backup-*",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ProfilePipeline_HandledPostCommitCleanupFailure_ReturnsVerifiedResult()
    {
        var output = CreateTempDirectory();
        var canonical = Path.Combine(output, "Profiles", "TinyProfile");
        try
        {
            WriteFile(Path.Combine(canonical, "old.g.cs"), "old profile\n");
            WriteFile(
                Path.Combine(canonical, "profile-coverage.json"),
                "{\"old\":true}\n");
            var profile = new ProfileDefinition(
                "TinyProfile",
                "fixture",
                ["TinyEnum"],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/TinyProfile");
            Exception? cleanupFailure = null;

            var result = ProfilePipeline.Run(
                profile,
                new IrBundle(
                    CreateDummyManifest(),
                    [MakeEnumSymbol("TinyEnum")],
                    []),
                output,
                promotionFailureInjector: point =>
                {
                    if (point == OutputPromotionFailurePoint.BeforeBackupDeletion)
                        throw new InjectedPromotionException(point);
                },
                cleanupFailureHandler: exception => cleanupFailure = exception);

            var promotionFailure =
                Assert.IsType<OutputPromotionCleanupException>(cleanupFailure);
            Assert.IsType<InjectedPromotionException>(
                promotionFailure.InnerException);
            Assert.True(result.Coverage.ByteIdentityVerified);
            Assert.Empty(result.PipelineResult.Errors);
            Assert.True(File.Exists(
                Path.Combine(canonical, "Enums", "TinyEnum.g.cs")));
            Assert.False(File.Exists(Path.Combine(canonical, "old.g.cs")));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static SymbolModel MakeInterfaceSymbol(
        string name,
        IReadOnlyList<MemberModel> members,
        string status = "matched",
        string? classification = "interface",
        IReadOnlyList<HeritageClauseModel>? heritage = null,
        IReadOnlyList<DeclarationModel>? declarations = null)
        => new(
            0,
            name,
            0,
            declarations ?? [MakeInterfaceDeclaration(name, 0, members, heritage)],
            declarations is { Count: > 1 },
            new SemanticModel(
                status,
                status == "unmatched" ? null : name,
                status == "unmatched" ? null : "definition",
                null,
                classification is null ? [] : [classification],
                [],
                [],
                false,
                false,
                [],
                false,
                false,
                false,
                [],
                []));

    private static DeclarationModel MakeInterfaceDeclaration(
        string name,
        int ordinal,
        IReadOnlyList<MemberModel> members,
        IReadOnlyList<HeritageClauseModel>? heritage = null)
        => new(
            ordinal,
            "interface",
            name,
            [],
            [],
            heritage ?? [],
            members,
            null,
            [],
            null,
            new DocumentationModel("", [], false),
            MakeLocation(ordinal + 1),
            null,
            false,
            new EventMapModel(false, []),
            []);

    private static DeclarationModel MakeGlobalFunctionDeclaration(
        string name,
        int ordinal,
        IReadOnlyList<ParameterModel> parameters)
        => new(
            ordinal,
            "globalFunction",
            name,
            [],
            [],
            [],
            [],
            null,
            parameters,
            new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            MakeLocation(ordinal + 1),
            null,
            false,
            new EventMapModel(false, []),
            []);

    private static MemberModel MakeMethod(
        int ordinal,
        string name,
        IReadOnlyList<ParameterModel> parameters,
        TypeNode? returnType = null)
        => new(
            ordinal,
            "method",
            new NameNode("identifier", name),
            false,
            false,
            false,
            [],
            parameters,
            null,
            returnType ?? new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            MakeLocation(ordinal + 10));

    private static MemberModel MakeProperty(
        int ordinal,
        string name,
        TypeNode type)
        => new(
            ordinal,
            "property",
            new NameNode("identifier", name),
            false,
            false,
            false,
            [],
            [],
            type,
            null,
            new DocumentationModel("", [], false),
            MakeLocation(ordinal + 10));

    private static ParameterModel MakeParameter(string name, TypeNode type)
        => new(
            0,
            name,
            false,
            false,
            type,
            null,
            new DocumentationModel("", [], false),
            MakeLocation(20));

    private static LocationModel MakeLocation(int line)
        => new("fixture.ts", new(line, 1, line), new(line, 10, line + 9));

    private static SymbolModel MakeEnumSymbol(string name)
        => new(
            0,
            name,
            0,
            [
                new DeclarationModel(
                    0,
                    "typeAlias",
                    name,
                    [],
                    [],
                    [],
                    [],
                    new UnionTypeNode(
                        [new LiteralTypeNode("StringLiteral", "\"value\"")]),
                    [],
                    null,
                    new DocumentationModel("", [], false),
                    MakeLocation(1),
                    null,
                    false,
                    new EventMapModel(false, []),
                    [])
            ],
            false,
            new SemanticModel(
                "matched",
                name,
                "definition",
                null,
                ["enum"],
                [],
                [],
                false,
                false,
                [],
                false,
                false,
                false,
                [],
                []));

    private static ManifestModel CreateDummyManifest()
        => new(
            1,
            new GenerationProfileModel("Window", ["Window"], true),
            new ManifestFilesModel(
                new("typescript-symbols.jsonl", "jsonl", "dummy", 0, new string('a', 64)),
                new("webidl-symbols.jsonl", "jsonl", "dummy", 0, new string('b', 64)),
                new("coverage.json", "json", "dummy", 1, new string('c', 64))),
            new ManifestCountsModel(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new ManifestProvenanceModel(
                new("test", "1.0.0", null),
                new("typescript", "1.0.0", "MIT", new string('d', 64), []),
                new("webref", "1.0.0", "MIT"),
                new("webidl2", "1.0.0", "MIT"),
                new("fixture", new string('e', 64), 0)));

    private static void WriteCanonicalStaticTree(string canonical)
    {
        WriteFile(
            Path.Combine(canonical, ".gitattributes"),
            "*.cs text eol=lf\n*.json text eol=lf\n");
        WriteFile(
            Path.Combine(canonical, "Profiles", "WakeLock", "keep.g.cs"),
            "wake lock profile\n");
        WriteFile(
            Path.Combine(canonical, "reviewed", "metadata.json"),
            "{\"reviewed\":true}\n");
        WriteFile(
            Path.Combine(canonical, "reviewed", "nested", "metadata.json"),
            "{\"reviewed\":\"nested\"}\n");
        WriteFile(
            Path.Combine(canonical, "emitter-manifest.json"),
            "{\"old\":true}\n");
    }

    private static void WriteExhaustiveStaging(string staging)
    {
        WriteFile(
            Path.Combine(staging, "Interfaces", "IFresh.g.cs"),
            "fresh\n");
        WriteFile(
            Path.Combine(staging, "emitter-manifest.json"),
            "{\"fresh\":true}\n");
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static Dictionary<string, byte[]> SnapshotTree(string directory)
        => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(directory, path),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static void AssertTreesEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(
            expected.Keys.OrderBy(path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var path in expected.Keys)
            Assert.Equal(expected[path], actual[path]);
    }

    private static void AssertNoPromotionDebris(string parent, string canonical)
    {
        var prefix = $".{Path.GetFileName(canonical)}.";
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(parent),
            path => Path.GetFileName(path)
                .StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "test-output",
            Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string StrictFailureCategory(string reason)
    {
        if (reason.Contains("Ambiguous symbol", StringComparison.Ordinal))
            return "override";
        if (reason.Contains("Unsupported union type", StringComparison.Ordinal))
            return "typed-union";
        if (reason.Contains("getter", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("setter", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("accessor", StringComparison.OrdinalIgnoreCase))
        {
            return "accessor";
        }
        if (reason.Contains("extends", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("heritage", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("base type", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("not interface/mixin", StringComparison.OrdinalIgnoreCase))
        {
            return "heritage";
        }
        if (reason.Contains("collides", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("collision", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("incompatible return types", StringComparison.OrdinalIgnoreCase))
        {
            return "overload-collision";
        }
        if (reason.Contains("Unresolved type reference", StringComparison.Ordinal))
            return "unresolved-standard-type";
        return "advanced-leaf";
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "blazorators.sln")))
                return directory;
            directory = Path.GetDirectoryName(directory);
        }

        throw new DirectoryNotFoundException("Could not locate blazorators.sln.");
    }

    private sealed class InjectedPromotionException(OutputPromotionFailurePoint point)
        : Exception($"Injected promotion failure at {point}.");
}
