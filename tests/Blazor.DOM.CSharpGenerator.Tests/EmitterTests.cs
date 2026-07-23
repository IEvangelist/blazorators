// DictionaryEmitter and CallbackEmitter integration tests.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class CSharpWriterTests
{
    [Fact]
    public void XmlDoc_DecodesHtmlEntitiesAndEscapesXmlText()
    {
        var writer = new CSharpWriter();

        writer.XmlDoc("size &ge; 1 & value < limit &times; 2");

        Assert.Contains(
            "size \u2265 1 &amp; value &lt; limit \u00d7 2",
            writer.ToString());
    }
}

public sealed class DictionaryEmitterTests
{
    private static TypeResolver EmptyResolver() => new([]);
    private static TypeResolver WithSymbol(string name) =>
        new([new SymbolModel(0, name, 0, [], false,
            new SemanticModel("matched", name, "definition", null, ["interface"],
                [], [], false, false, [], false, false, false, [], []))]);

    private static TypeResolver WithDictionarySymbols(params string[] names)
        => new(names.Select((name, i) => new SymbolModel(i, name, 0, [], false,
            new SemanticModel("matched", name, "definition", null, ["dictionary"],
                [], [], false, false, [], false, false, false, [], []))).ToList());

    private static MemberModel MakeProp(int ordinal, string name, TypeNode type, bool optional = false, bool deprecated = false)
        => new(ordinal, "property",
            new NameNode("identifier", name),
            Optional: optional, Readonly: false, Static: false,
            TypeParameters: [], Parameters: [], Type: type, ReturnType: null,
            Documentation: new DocumentationModel("", [], deprecated),
            Location: new LocationModel("test", new(1,1,0), new(1,10,9)));

    private static SymbolModel MakeDictSymbol(
        string name,
        IReadOnlyList<MemberModel> members,
        IReadOnlyList<HeritageClauseModel>? heritage = null)
    {
        var decl = new DeclarationModel(
            Ordinal: 0, Kind: "interface", Name: name,
            Modifiers: [], TypeParameters: [],
            Heritage: heritage ?? [],
            Members: members, Type: null, Parameters: [], ReturnType: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(1,1,0), new(1,100,99)),
            VariableKind: null, ConstructorObject: false,
            EventMap: new EventMapModel(false, []),
            NamespaceMembers: []);

        return new SymbolModel(
            Ordinal: 0, Name: name, SymbolFlags: 64,
            Declarations: [decl], IsDeclarationMerged: false,
            Semantic: new SemanticModel(
                Status: "matched", WebIdlName: name, BindingKind: "definition",
                WebIdlMemberName: null, Classifications: ["dictionary"],
                Specifications: ["dom"], Exposures: [],
                ExposedOnWindow: false, ExposedOnWorker: false, GlobalNames: [],
                Serializable: false, Transferable: false, SecureContext: false,
                ExtendedAttributes: [], Bindings: []));
    }

    [Fact]
    public void Emit_AddEventListenerOptions_ProducesRecord()
    {
        // D4 regression: AddEventListenerOptions extends EventListenerOptions (a known dictionary).
        // The resolver must know EventListenerOptions is a dictionary so record inheritance is emitted.
        var resolver = WithDictionarySymbols("EventListenerOptions", "AbortSignal");
        var emitter = new DictionaryEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeDictSymbol("AddEventListenerOptions",
        [
            MakeProp(0, "once",    new KeywordTypeNode("BooleanKeyword"), optional: true),
            MakeProp(1, "passive", new KeywordTypeNode("BooleanKeyword"), optional: true),
            MakeProp(2, "signal",  new ReferenceTypeNode("AbortSignal", "AbortSignal", []), optional: true),
        ],
        heritage: [new HeritageClauseModel("extends",
            [new HeritageReferenceTypeNode("EventListenerOptions", null, [])])]);

        var source = emitter.Emit(symbol);

        // D4: record must inherit from EventListenerOptions
        Assert.Contains(
            "[global::Microsoft.JSInterop.DomJsonValue]",
            source);
        Assert.Contains("public record AddEventListenerOptions : EventListenerOptions", source);
        Assert.Contains("bool? Once", source);
        Assert.Contains("bool? Passive", source);
        Assert.Contains("[JsonPropertyName(\"once\")]", source);
        Assert.Contains(
            "[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]",
            source);
        Assert.DoesNotContain("object", source);
    }

    [Fact]
    public void Emit_OptionalNullableMember_PreservesOmittedAndExplicitNullStates()
    {
        var resolver = WithSymbol("AbortSignal");
        var emitter = new DictionaryEmitter(resolver, "1.0.0", "Blazor.DOM");
        var symbol = MakeDictSymbol(
            "RequestInit",
            [
                MakeProp(
                    0,
                    "signal",
                    new UnionTypeNode(
                    [
                        new ReferenceTypeNode("AbortSignal", "AbortSignal", []),
                        new LiteralTypeNode("NullKeyword", "null"),
                    ]),
                    optional: true),
            ]);

        var source = emitter.Emit(symbol);

        Assert.Contains(
            "DomOptional<IAbortSignal?> Signal",
            source);
        Assert.Contains(
            "[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]",
            source);
        Assert.DoesNotContain(
            "[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]",
            source);
    }

    [Fact]
    public void Emit_UnsupportedIntersection_IsExplicitNamedDeferral()
    {
        var resolver = EmptyResolver();
        var emitter = new DictionaryEmitter(resolver, "1.0.0", "Blazor.DOM");

        var symbol = MakeDictSymbol("ProblematicDict",
        [
            MakeProp(0, "bad", new IntersectionTypeNode([
                new KeywordTypeNode("StringKeyword"),
                new KeywordTypeNode("BooleanKeyword"),
            ])),
        ]);

        var result = emitter.EmitWithOutcomes(symbol);
        var outcome = Assert.Single(result.MemberOutcomes);

        Assert.Equal(MemberOutcomeStatus.Deferred, outcome.Status);
        Assert.Equal("intersection-composition", outcome.Phase);
        Assert.DoesNotContain("object", result.Source);
        Assert.DoesNotContain("Bad", result.Source);
    }

    [Fact]
    public void Emit_IsAutoGenerated_ContainsHeader()
    {
        var emitter = new DictionaryEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var symbol = MakeDictSymbol("Empty", []);
        var source = emitter.Emit(symbol);
        Assert.StartsWith("#nullable enable", source);
        Assert.Contains("// <auto-generated/>", source);
    }
}

public sealed class CallbackEmitterTests
{
    private static TypeResolver EmptyResolver() => new([]);
    private static TypeResolver WithSymbol(string name) =>
        new([new SymbolModel(0, name, 0, [], false,
            new SemanticModel("matched", name, "definition", null, ["interface"],
                [], [], false, false, [], false, false, false, [], []))]);

    [Fact]
    public void Emit_AudioDataOutputCallback_ProducesDelegate()
    {
        var resolver = WithSymbol("AudioData");
        var emitter = new CallbackEmitter(resolver, "1.0.0", "Blazor.DOM");

        var callSig = new MemberModel(
            Ordinal: 0, Kind: "callSignature", Name: null,
            Optional: false, Readonly: false, Static: false,
            TypeParameters: [],
            Parameters: [
                new ParameterModel(0, "output", false, false,
                    new ReferenceTypeNode("AudioData", "AudioData", []), null,
                    new DocumentationModel("", [], false),
                    new LocationModel("test", new(1,1,0), new(1,10,9))),
            ],
            Type: null,
            ReturnType: new KeywordTypeNode("VoidKeyword"),
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(1,1,0), new(1,50,49)));

        var decl = new DeclarationModel(
            Ordinal: 0, Kind: "interface", Name: "AudioDataOutputCallback",
            Modifiers: [], TypeParameters: [], Heritage: [],
            Members: [callSig], Type: null, Parameters: [], ReturnType: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(1,1,0), new(1,100,99)),
            VariableKind: null, ConstructorObject: false,
            EventMap: new EventMapModel(false, []),
            NamespaceMembers: []);

        var symbol = new SymbolModel(
            Ordinal: 0, Name: "AudioDataOutputCallback", SymbolFlags: 64,
            Declarations: [decl], IsDeclarationMerged: false,
            Semantic: new SemanticModel(
                Status: "matched", WebIdlName: "AudioDataOutputCallback",
                BindingKind: "definition", WebIdlMemberName: null,
                Classifications: ["callback"], Specifications: ["webcodecs"],
                Exposures: [], ExposedOnWindow: false, ExposedOnWorker: false,
                GlobalNames: [], Serializable: false, Transferable: false,
                SecureContext: false, ExtendedAttributes: [], Bindings: []));

        var source = emitter.Emit(symbol);

        Assert.Contains("public delegate void AudioDataOutputCallback", source);
        Assert.Contains("AudioData output", source);
        Assert.DoesNotContain("object", source);
    }

    [Fact]
    public void EmitWithOutcomes_FunctionOnlyAlias_ProjectsEverySignatureParameter()
    {
        var symbol = MakeAliasCallback(
            "FunctionOnlyCallback",
            MakeFunction("functionValue"));

        var result = new CallbackEmitter(
            EmptyResolver(),
            "1.0.0",
            "Blazor.DOM").EmitWithOutcomes(symbol);

        Assert.Contains(
            "public delegate void FunctionOnlyCallback(string functionValue);",
            result.Source);
        Assert.Empty(result.MemberOutcomes);
        var declaration = Assert.Single(result.DeclarationOutcomes ?? []);
        Assert.Equal(MemberOutcomeStatus.Projected, declaration.Status);
        var overload = Assert.Single(result.OverloadOutcomes ?? []);
        Assert.Equal(MemberOutcomeStatus.Projected, overload.Status);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            Assert.Single(overload.ParameterOutcomes).Status);
    }

    [Fact]
    public void EmitWithOutcomes_ObjectOnlyAlias_EmitsTypedObjectArm()
    {
        var symbol = MakeAliasCallback(
            "ObjectOnlyCallback",
            MakeObjectForm("accept", "objectValue"));

        var result = new CallbackEmitter(
            EmptyResolver(),
            "1.0.0",
            "Blazor.DOM").EmitWithOutcomes(symbol);

        Assert.Contains("public interface IObjectOnlyCallbackCallbackObject", result.Source);
        Assert.Contains("void Accept(string objectValue);", result.Source);
        Assert.Contains("public readonly struct ObjectOnlyCallback", result.Source);
        Assert.DoesNotContain("object value", result.Source);
        var member = Assert.Single(result.MemberOutcomes);
        Assert.Equal(MemberOutcomeStatus.Projected, member.Status);
        Assert.Null(member.Phase);
        Assert.Contains("/type/typeLiteral/member[0]", member.QualifiedKey);
        var overload = Assert.Single(result.OverloadOutcomes ?? []);
        Assert.Equal(MemberOutcomeStatus.Projected, overload.Status);
        Assert.Null(overload.Phase);
        Assert.Contains("/type/typeLiteral/member[0]/overload", overload.QualifiedKey);
        var parameter = Assert.Single(overload.ParameterOutcomes);
        Assert.Equal(MemberOutcomeStatus.Projected, parameter.Status);
        Assert.Contains("/parameter[0]/objectValue", parameter.Provenance);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            Assert.Single(result.DeclarationOutcomes ?? []).Status);
    }

    [Fact]
    public void EmitWithOutcomes_FunctionOrObjectAlias_ProjectsBothTypedArms()
    {
        var symbol = MakeAliasCallback(
            "FunctionOrObjectCallback",
            new UnionTypeNode(
            [
                new ParenthesizedTypeNode(MakeFunction("functionValue")),
                MakeObjectForm("accept", "objectValue"),
            ]));

        var result = new CallbackEmitter(
            EmptyResolver(),
            "1.0.0",
            "Blazor.DOM").EmitWithOutcomes(symbol);

        Assert.Contains(
            "public delegate void FunctionOrObjectCallbackFunction(string functionValue);",
            result.Source);
        Assert.Contains("void Accept(string objectValue);", result.Source);
        Assert.Contains("public readonly struct FunctionOrObjectCallback", result.Source);
        var objectMember = Assert.Single(result.MemberOutcomes);
        Assert.Equal(MemberOutcomeStatus.Projected, objectMember.Status);
        Assert.Null(objectMember.Phase);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            Assert.Single(result.DeclarationOutcomes ?? []).Status);

        var overloads = result.OverloadOutcomes ?? [];
        Assert.Equal(2, overloads.Count);
        var functionOverload = Assert.Single(
            overloads,
            overload => overload.Kind == "function");
        Assert.Equal(MemberOutcomeStatus.Projected, functionOverload.Status);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            Assert.Single(functionOverload.ParameterOutcomes).Status);
        var objectOverload = Assert.Single(
            overloads,
            overload => overload.Kind == "method");
        Assert.Equal(MemberOutcomeStatus.Projected, objectOverload.Status);
        Assert.Null(objectOverload.Phase);
        Assert.Equal(
            MemberOutcomeStatus.Projected,
            Assert.Single(objectOverload.ParameterOutcomes).Status);
    }

    private static SymbolModel MakeAliasCallback(string name, TypeNode type)
    {
        var declaration = new DeclarationModel(
            0,
            "typeAlias",
            name,
            [],
            [],
            [],
            [],
            type,
            [],
            null,
            new DocumentationModel("", [], false),
            MakeLocation(1),
            null,
            false,
            new EventMapModel(false, []),
            []);
        return new SymbolModel(
            0,
            name,
            0,
            [declaration],
            false,
            new SemanticModel(
                "matched",
                name,
                "definition",
                null,
                ["callback"],
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
    }

    private static FunctionTypeNode MakeFunction(string parameterName)
        => new(
            [],
            [MakeParameter(parameterName)],
            new KeywordTypeNode("VoidKeyword"));

    private static TypeLiteralTypeNode MakeObjectForm(
        string methodName,
        string parameterName)
        => new(
        [
            new MemberModel(
                0,
                "method",
                new NameNode("identifier", methodName),
                false,
                false,
                false,
                [],
                [MakeParameter(parameterName)],
                null,
                new KeywordTypeNode("VoidKeyword"),
                new DocumentationModel("", [], false),
                MakeLocation(3)),
        ]);

    private static ParameterModel MakeParameter(string name)
        => new(
            0,
            name,
            false,
            false,
            new KeywordTypeNode("StringKeyword"),
            null,
            new DocumentationModel("", [], false),
            MakeLocation(2));

    private static LocationModel MakeLocation(int line)
        => new(
            "callback-fixture.ts",
            new PositionModel(line, 1, line),
            new PositionModel(line, 10, line + 9));
}
