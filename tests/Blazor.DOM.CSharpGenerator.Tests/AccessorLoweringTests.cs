using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class AccessorLoweringTests
{
    [Fact]
    public void SetterOnly_EmitsIntentionalPropertySetterMethod()
    {
        var symbol = Symbol(
            "WriteOnly",
            [Declaration("WriteOnly", 0, Setter("token", StringType(), 3))]);
        var result = Emit([symbol], symbol);

        Assert.DoesNotContain(" Token {", result.Source);
        Assert.Contains("void SetToken(string value);", result.Source);
        Assert.Contains("\"token\"", result.Source);
        Assert.Contains("DomAccessorOperation.Set", result.Source);
        var outcome = Assert.Single(result.MemberOutcomes);
        Assert.Equal(MemberOutcomeStatus.Projected, outcome.Status);
        Assert.Equal(3, outcome.Ordinal);
        Assert.Equal(
            "WriteOnly/decl[0]/member[3]/setter/token",
            outcome.Provenance);
    }

    [Fact]
    public void SymmetricMutable_EmitsPropertyAndDirectionalMetadata()
    {
        var symbol = Symbol(
            "Mutable",
            [Declaration("Mutable", 0, Property("value", StringType(), 0))]);
        var source = Emit([symbol], symbol).Source;

        Assert.Contains("string Value { get; set; }", source);
        Assert.Equal(2, Count(source, "DomAccessor("));
        Assert.Contains("DomAccessorOperation.Get", source);
        Assert.Contains("DomAccessorOperation.Set", source);
    }

    [Fact]
    public void NullableValueAsymmetry_PreservesBothTypes()
    {
        var nullableNumber = new UnionTypeNode(
        [
            NumberType(),
            NullType(),
        ]);
        var symbol = Symbol(
            "Numeric",
            [
                Declaration(
                    "Numeric",
                    0,
                    Getter("value", NumberType(), 0),
                    Setter("value", nullableNumber, 1))
            ]);
        var source = Emit([symbol], symbol).Source;

        Assert.Contains("double Value { get; }", source);
        Assert.Contains("void SetValue(double? value);", source);
        Assert.DoesNotContain("double? Value", source);
    }

    [Fact]
    public void NullableReferenceAsymmetry_PreservesBothTypes()
    {
        var nullableString = new UnionTypeNode(
        [
            StringType(),
            NullType(),
        ]);
        var symbol = Symbol(
            "Textual",
            [
                Declaration(
                    "Textual",
                    0,
                    Getter("text", nullableString, 0),
                    Setter("text", StringType(), 1))
            ]);
        var source = Emit([symbol], symbol).Source;

        Assert.Contains("string? Text { get; }", source);
        Assert.Contains("void SetText(string value);", source);
    }

    [Fact]
    public void UnionVersusArm_PreservesSynthesizedGetterAndSetterArm()
    {
        var union = new UnionTypeNode([StringType(), NumberType()])
        {
            CheckerType = "string | number",
            Transport = Transport("json-value", "string | number"),
        };
        var symbol = Symbol(
            "ChoiceHost",
            [
                Declaration(
                    "ChoiceHost",
                    0,
                    Getter("choice", union, 0),
                    Setter("choice", StringType("json-value"), 1))
            ]);
        var resolver = new TypeResolver([symbol]);
        var result = new InterfaceEmitter(
            resolver,
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        var synthesized = Assert.Single(resolver.SynthesizedTypes);
        Assert.Contains(synthesized.Name, result.Source);
        Assert.EndsWith("StringOrNumberUnion", synthesized.Name);
        Assert.Contains("void SetChoice(string value);", result.Source);
    }

    [Fact]
    public void SameClrTypeDifferentTransport_LowersDirectionsSeparately()
    {
        var symbol = Symbol(
            "TransportHost",
            [
                Declaration(
                    "TransportHost",
                    0,
                    Getter("payload", StringType("js-reference"), 0),
                    Setter("payload", StringType("json-value"), 1))
            ]);
        var source = Emit([symbol], symbol).Source;

        Assert.Contains("string Payload { get; }", source);
        Assert.Contains("void SetPayload(string value);", source);
        Assert.Contains("DomTransportKind.JsReference", source);
        Assert.Contains("DomTransportKind.JsonValue", source);
    }

    [Fact]
    public void GenericAccessorAndThisGetter_UseLexicalScope()
    {
        var typeParameter = new TypeParameterModel(0, "T", null, null);
        var declaration = Declaration(
            "GenericHost",
            0,
            Getter(
                "self",
                new ReferenceTypeNode("this", null, [])
                {
                    CheckerType = "this",
                    Transport = Transport("js-reference", "this"),
                },
                0),
            Property(
                "item",
                new ReferenceTypeNode("T", "GenericHost.T", []),
                1)) with
        {
            TypeParameters = [typeParameter]
        };
        var symbol = Symbol("GenericHost", [declaration]);
        var source = Emit([symbol], symbol).Source;

        Assert.Contains("public partial interface IGenericHost<T>", source);
        Assert.Contains("IGenericHost<T> Self { get; }", source);
        Assert.Contains("T Item { get; set; }", source);
    }

    [Fact]
    public void ExactInheritedAccessor_IsAccountedWithoutHidingMember()
    {
        var baseSymbol = Symbol(
            "Base",
            [Declaration("Base", 0, Getter("value", StringType(), 0))]);
        var derived = Symbol(
            "Derived",
            [
                Declaration(
                    "Derived",
                    0,
                    Getter("value", StringType(), 4)) with
                {
                    Heritage =
                    [
                        new HeritageClauseModel(
                            "extends",
                            [new HeritageReferenceTypeNode("Base", "Base", [])])
                    ]
                }
            ]);
        var result = Emit([baseSymbol, derived], derived);

        Assert.Contains("public partial interface IDerived : IBase", result.Source);
        Assert.DoesNotContain("string Value", result.Source);
        var outcome = Assert.Single(result.MemberOutcomes);
        Assert.Equal(MemberOutcomeStatus.Projected, outcome.Status);
        Assert.Equal(4, outcome.Ordinal);
    }

    [Fact]
    public void DerivedSetterForInheritedGetter_EmitsSetterMethodWithoutNewProperty()
    {
        var baseSymbol = Symbol(
            "Base",
            [Declaration("Base", 0, Getter("value", StringType(), 0))]);
        var derived = Symbol(
            "Derived",
            [
                Declaration(
                    "Derived",
                    0,
                    Setter("value", StringType(), 2)) with
                {
                    Heritage =
                    [
                        new HeritageClauseModel(
                            "extends",
                            [new HeritageReferenceTypeNode("Base", "Base", [])])
                    ]
                }
            ]);
        var source = Emit([baseSymbol, derived], derived).Source;

        Assert.DoesNotContain("string Value", source);
        Assert.Contains("void SetValue(string value);", source);
    }

    [Fact]
    public void Method_EmitsAuthoritativeLogicalOperationMetadata()
    {
        var symbol = Symbol(
            "MethodHost",
            [
                Declaration(
                    "MethodHost",
                    0,
                    Method("read-value", [], 7))
            ]);

        var source = Emit([symbol], symbol).Source;

        Assert.Contains("DomOperation(", source);
        Assert.Contains("\"read-value\"", source);
        Assert.Contains("DomTransportKind.JsonValue", source);
        Assert.Contains("Promise = false", source);
        Assert.Contains("ReadValue", source);
    }

    [Fact]
    public void IncompatibleInheritedAccessor_LowersToExplicitGetterMethod()
    {
        var baseSymbol = Symbol(
            "Base",
            [Declaration("Base", 0, Getter("value", StringType(), 1))]);
        var derived = Symbol(
            "Derived",
            [
                Declaration(
                    "Derived",
                    0,
                    Getter("value", NumberType(), 2)) with
                {
                    Heritage =
                    [
                        new HeritageClauseModel(
                            "extends",
                            [new HeritageReferenceTypeNode("Base", "Base", [])])
                    ]
                }
            ]);

        var result = Emit([baseSymbol, derived], derived);

        Assert.DoesNotContain("double Value", result.Source);
        Assert.Contains("double GetValue();", result.Source);
        Assert.Contains("DomAccessorOperation.Get", result.Source);
        var outcome = Assert.Single(result.MemberOutcomes);
        Assert.Equal(
            "Derived/decl[0]/member[2]/getter/value",
            outcome.Provenance);
    }

    [Fact]
    public void PropertyAndMethodNameCollision_FailsExplicitly()
    {
        var symbol = Symbol(
            "Collision",
            [
                Declaration(
                    "Collision",
                    0,
                    Property("value", StringType(), 0),
                    Method("value", [], 1))
            ]);

        var exception = Assert.Throws<InterfaceEmitException>(() =>
            Emit([symbol], symbol));

        Assert.Contains("collides with accessor source", exception.Message);
        Assert.Contains("member[0]/property/value", exception.Message);
    }

    [Fact]
    public void LoweredSetterAndExistingMethodCollision_FailsExplicitly()
    {
        var symbol = Symbol(
            "Collision",
            [
                Declaration(
                    "Collision",
                    0,
                    Setter("value", StringType(), 0),
                    Method(
                        "setValue",
                        [Parameter("value", StringType())],
                        1))
            ]);

        var exception = Assert.Throws<InterfaceEmitException>(() =>
            Emit([symbol], symbol));

        Assert.Contains("C# member 'SetValue'", exception.Message);
        Assert.Contains("member[0]/setter/value", exception.Message);
    }

    [Fact]
    public void StaticAccessor_EmitsStaticAbstractContract()
    {
        var getter = Getter("current", StringType(), 0) with { Static = true };
        var setter = Setter("current", NumberType(), 1) with { Static = true };
        var symbol = Symbol(
            "Statics",
            [Declaration("Statics", 0, getter, setter)]);
        var source = Emit([symbol], symbol).Source;

        Assert.Contains("static abstract string Current { get; }", source);
        Assert.Contains("static abstract void SetCurrent(double value);", source);
    }

    [Fact]
    public void LoweringOrder_IsStableAcrossInputEnumeration()
    {
        var declaration = Declaration(
            "Ordered",
            0,
            Setter("zeta", StringType(), 8),
            Getter("alpha", StringType(), 2),
            Setter("alpha", NumberType(), 3));
        var symbol = Symbol("Ordered", [declaration]);

        var first = Emit([symbol], symbol).Source;
        var second = Emit([symbol], symbol).Source;

        Assert.Equal(first, second);
        Assert.True(
            first.IndexOf(" Alpha ", StringComparison.Ordinal)
            < first.IndexOf("SetZeta", StringComparison.Ordinal));
    }

    private static InterfaceEmitResult Emit(
        IReadOnlyList<SymbolModel> symbols,
        SymbolModel symbol)
        => new InterfaceEmitter(
            new TypeResolver(symbols),
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

    private static SymbolModel Symbol(
        string name,
        IReadOnlyList<DeclarationModel> declarations)
        => new(
            declarations[0].Ordinal,
            name,
            0,
            declarations,
            false,
            new SemanticModel(
                "matched",
                name,
                "definition",
                null,
                ["interface"],
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

    private static DeclarationModel Declaration(
        string name,
        int ordinal,
        params MemberModel[] members)
        => new(
            ordinal,
            "interface",
            name,
            [],
            [],
            [],
            members,
            null,
            [],
            null,
            new DocumentationModel("", [], false),
            Location(),
            null,
            false,
            new EventMapModel(false, []),
            []);

    private static MemberModel Property(
        string name,
        TypeNode type,
        int ordinal)
        => Member(ordinal, "property", name, type, null, []);

    private static MemberModel Getter(
        string name,
        TypeNode type,
        int ordinal)
        => Member(ordinal, "getter", name, null, type, []);

    private static MemberModel Setter(
        string name,
        TypeNode type,
        int ordinal)
        => Member(
            ordinal,
            "setter",
            name,
            null,
            null,
            [Parameter("value", type)]);

    private static MemberModel Method(
        string name,
        IReadOnlyList<ParameterModel> parameters,
        int ordinal)
        => Member(
            ordinal,
            "method",
            name,
            null,
            new KeywordTypeNode("VoidKeyword"),
            parameters);

    private static MemberModel Member(
        int ordinal,
        string kind,
        string name,
        TypeNode? type,
        TypeNode? returnType,
        IReadOnlyList<ParameterModel> parameters)
        => new(
            ordinal,
            kind,
            new NameNode("identifier", name),
            false,
            false,
            false,
            [],
            parameters,
            type,
            returnType,
            new DocumentationModel("", [], false),
            Location());

    private static ParameterModel Parameter(string name, TypeNode type)
        => new(
            0,
            name,
            false,
            false,
            type,
            null,
            new DocumentationModel("", [], false),
            Location());

    private static KeywordTypeNode StringType(string? transportKind = null)
        => new("StringKeyword")
        {
            CheckerType = "string",
            Transport = transportKind is null
                ? null
                : Transport(transportKind, "string"),
        };

    private static KeywordTypeNode NumberType()
        => new("NumberKeyword")
        {
            CheckerType = "number",
            Transport = Transport("json-value", "number"),
        };

    private static LiteralTypeNode NullType()
        => new("NullKeyword", "null")
        {
            CheckerType = "null",
            Transport = new TransportModel(
                "json-value",
                true,
                "null",
                false,
                true,
                null),
        };

    private static TransportModel Transport(string kind, string sourceType)
        => new(
            kind,
            false,
            sourceType,
            false,
            kind == "json-value",
            null);

    private static LocationModel Location()
        => new("accessors.ts", new(1, 1, 0), new(1, 1, 0));

    private static int Count(string value, string search)
        => value.Split(search, StringSplitOptions.None).Length - 1;
}
