using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class AccessorReconcilerTests
{
    [Fact]
    public void Reconcile_MergesPropertiesAndAccessorsAcrossDeclarations()
    {
        DeclarationModel[] declarations =
        [
            Declaration(0, Property("value", StringType(), @readonly: true, ordinal: 2)),
            Declaration(3, Getter("value", StringType(), ordinal: 1)),
            Declaration(5, Setter("value", StringType(), ordinal: 4)),
        ];

        var result = Reconcile(declarations);
        var accessor = Assert.Single(result.Accessors);

        Assert.Equal("value", accessor.JavaScriptName);
        Assert.Equal("Value", accessor.CSharpName);
        Assert.True(accessor.IsSymmetric);
        Assert.Equal(3, accessor.Sources.Count);
        Assert.Equal(
            [
                "Host/decl[0]/member[2]/property/value",
                "Host/decl[3]/member[1]/getter/value",
                "Host/decl[5]/member[4]/setter/value",
            ],
            accessor.Sources.Select(source => source.Provenance));
    }

    [Fact]
    public void Reconcile_ReadonlyAndMutablePropertyMerge_IsMutable()
    {
        var result = Reconcile(
        [
            Declaration(0, Property("state", StringType(), @readonly: true)),
            Declaration(2, Property("state", StringType(), @readonly: false)),
        ]);

        var accessor = Assert.Single(result.Accessors);
        Assert.NotNull(accessor.Getter);
        Assert.NotNull(accessor.Setter);
        Assert.True(accessor.IsSymmetric);
        Assert.Equal(2, accessor.Getter.Sources.Count);
        Assert.Single(accessor.Setter.Sources);
        Assert.Equal(2, accessor.Setter.Sources[0].DeclarationOrdinal);
    }

    [Fact]
    public void Reconcile_SetterOnly_IsRetained()
    {
        var result = Reconcile(
        [
            Declaration(7, Setter("secret", StringType(), ordinal: 9)),
        ]);

        var accessor = Assert.Single(result.Accessors);
        Assert.Null(accessor.Getter);
        Assert.NotNull(accessor.Setter);
        Assert.Equal(
            "Host/decl[7]/member[9]/setter/secret",
            accessor.Setter.CanonicalSource.Provenance);
    }

    [Fact]
    public void Reconcile_DuplicateGetterWithDifferentNullability_FailsWithProvenance()
    {
        var nullableString = new UnionTypeNode(
        [
            StringType(),
            new LiteralTypeNode("NullKeyword", "null"),
        ]);
        var exception = Assert.Throws<TypeProjectionException>(() => Reconcile(
        [
            Declaration(0, Getter("name", StringType())),
            Declaration(4, Getter("name", nullableString)),
        ]));

        Assert.Contains("incompatible merged source types", exception.Message);
        Assert.Contains("Host/decl[0]/member[0]/getter/name", exception.Message);
        Assert.Contains("Host/decl[4]/member[0]/getter/name", exception.Message);
        Assert.Equal(
            "Host/decl[4]/member[0]/getter/name",
            exception.Provenance);
    }

    [Fact]
    public void Reconcile_DuplicateSetterWithDifferentGenericArguments_Fails()
    {
        var symbols = new[]
        {
            Symbol("Box", [new TypeParameterModel(0, "T", null, null)]),
        };
        var reconciler = new AccessorReconciler(new TypeResolver(symbols));
        DeclarationModel[] declarations =
        [
            Declaration(0, Setter(
                "item",
                new ReferenceTypeNode("Box", "Box", [StringType()]))),
            Declaration(1, Setter(
                "item",
                new ReferenceTypeNode("Box", "Box", [NumberType()]))),
        ];

        var exception = Assert.Throws<TypeProjectionException>(() =>
        {
            _ = reconciler.Reconcile("Host", declarations, Scope());
        });

        Assert.Contains("Set accessor 'Host.item'", exception.Message);
    }

    [Fact]
    public void Reconcile_SameClrTypeWithDifferentTransport_Fails()
    {
        var getterType = StringType("json-value");
        var secondGetterType = StringType("js-reference");
        var exception = Assert.Throws<TypeProjectionException>(() => Reconcile(
        [
            Declaration(0, Getter("value", getterType)),
            Declaration(1, Getter("value", secondGetterType)),
        ]));

        Assert.Contains("incompatible merged source types", exception.Message);
    }

    [Fact]
    public void StructuralIdentity_DistinguishesReferenceNullabilityOptionalityAndUndefined()
    {
        var clr = new ClrTypeIdentity("string", ClrTypeKind.Reference);
        var required = new AccessorTypeIdentity(clr, false, false, false, null);
        var nullable = required with { IsNullable = true };
        var optional = nullable with { IsOptional = true };
        var undefined = nullable with { IncludesUndefined = true };

        Assert.False(required.StructurallyEquals(nullable));
        Assert.False(nullable.StructurallyEquals(optional));
        Assert.False(nullable.StructurallyEquals(undefined));
    }

    [Fact]
    public void StructuralIdentity_RecursesThroughGenericArguments()
    {
        var stringBox = new AccessorTypeIdentity(
            new ClrTypeIdentity(
                "Box`1",
                ClrTypeKind.Reference,
                GenericArity: 1,
                TypeArguments: [new("string", ClrTypeKind.Reference)]),
            false,
            false,
            false,
            null);
        var numberBox = stringBox with
        {
            ClrType = stringBox.ClrType with
            {
                TypeArguments = [new ClrTypeIdentity("double", ClrTypeKind.Value)]
            }
        };

        Assert.False(stringBox.StructurallyEquals(numberBox));
    }

    [Fact]
    public void Reconcile_WrongSetterArityFailsAtSource()
    {
        var badSetter = Setter("value", StringType()) with
        {
            Parameters =
            [
                Parameter("first", StringType(), ordinal: 0),
                Parameter("second", StringType(), ordinal: 1),
            ]
        };
        var exception = Assert.Throws<TypeProjectionException>(() =>
            Reconcile([Declaration(6, badSetter)]));

        Assert.Contains("exactly one value parameter", exception.Message);
        Assert.Equal(
            "Host/decl[6]/member[0]/setter/value",
            exception.Provenance);
    }

    [Fact]
    public void Reconcile_NormalizedNameCollisionFailsDeterministically()
    {
        var exception = Assert.Throws<TypeProjectionException>(() => Reconcile(
        [
            Declaration(
                0,
                Property("data-value", StringType(), ordinal: 0),
                Property("dataValue", StringType(), ordinal: 1)),
        ]));

        Assert.Contains("both normalize to C# member 'DataValue'", exception.Message);
        Assert.Contains("property/data-value", exception.Message);
        Assert.Contains("property/dataValue", exception.Message);
    }

    [Fact]
    public void Reconcile_UsesSourceOrderForAccessorsAndDocumentation()
    {
        var second = Property("second", StringType(), ordinal: 8) with
        {
            Documentation = new("Second docs.", [], true)
        };
        var first = Getter("first", StringType(), ordinal: 2) with
        {
            Documentation = new("First docs.", [], false)
        };
        var firstDuplicate = Getter("first", StringType(), ordinal: 1) with
        {
            Documentation = new("More docs.", [], true)
        };

        var result = Reconcile(
        [
            Declaration(0, second, first),
            Declaration(4, firstDuplicate),
        ]);

        Assert.Equal(["first", "second"], result.Accessors.Select(a => a.JavaScriptName));
        Assert.Equal("First docs.\n\nMore docs.", result.Accessors[0].Documentation);
        Assert.True(result.Accessors[0].Deprecated);
    }

    private static AccessorReconciliationResult Reconcile(
        IReadOnlyList<DeclarationModel> declarations)
        => new AccessorReconciler(new TypeResolver([]))
            .Reconcile("Host", declarations, Scope());

    private static GenericScope Scope() => GenericScope.Create([], "Host");

    private static DeclarationModel Declaration(
        int ordinal,
        params MemberModel[] members)
        => new(
            ordinal,
            "interface",
            "Host",
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
        bool @readonly = false,
        int ordinal = 0)
        => new(
            ordinal,
            "property",
            new NameNode("identifier", name),
            false,
            @readonly,
            false,
            [],
            [],
            type,
            null,
            new DocumentationModel("", [], false),
            Location());

    private static MemberModel Getter(
        string name,
        TypeNode type,
        int ordinal = 0)
        => new(
            ordinal,
            "getter",
            new NameNode("identifier", name),
            false,
            true,
            false,
            [],
            [],
            null,
            type,
            new DocumentationModel("", [], false),
            Location());

    private static MemberModel Setter(
        string name,
        TypeNode type,
        int ordinal = 0)
        => new(
            ordinal,
            "setter",
            new NameNode("identifier", name),
            false,
            false,
            false,
            [],
            [Parameter("value", type)],
            null,
            null,
            new DocumentationModel("", [], false),
            Location());

    private static ParameterModel Parameter(
        string name,
        TypeNode type,
        int ordinal = 0)
        => new(
            ordinal,
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
                : new TransportModel(
                    transportKind,
                    false,
                    "string",
                    false,
                    transportKind == "json-value",
                    null),
        };

    private static KeywordTypeNode NumberType() => new("NumberKeyword");

    private static SymbolModel Symbol(
        string name,
        IReadOnlyList<TypeParameterModel> parameters)
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
                    parameters,
                    [],
                    [],
                    null,
                    [],
                    null,
                    new DocumentationModel("", [], false),
                    Location(),
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

    private static LocationModel Location()
        => new("fixture.ts", new(1, 1, 0), new(1, 1, 0));
}
