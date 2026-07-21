using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Profiles;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class P1P9RegressionTests
{
    [Fact]
    public void TypeProjectionNullableValueType_PreservesDistinctOverloads()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var nullableDouble = new UnionTypeNode([
            new KeywordTypeNode("NumberKeyword"),
            new LiteralTypeNode("NullKeyword", "null")
        ]);

        var decl = MakeInterfaceDecl("Overloads",
        [
            MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", new KeywordTypeNode("NumberKeyword"))], ordinal: 0),
            MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", nullableDouble)], ordinal: 1),
        ]);

        var result = emitter.Emit(MakeSymbol("Overloads", "interface", [decl]));

        Assert.Contains("void Foo(double x);", result.Source);
        Assert.Contains("void Foo(double? x);", result.Source);
        Assert.Equal(2, result.Source.Split("void Foo(").Length - 1);
    }

    [Fact]
    public void TypeProjectionNullableReferenceType_DeduplicatesOverloads()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var nullableString = new UnionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new LiteralTypeNode("NullKeyword", "null")
        ]);

        var decl = MakeInterfaceDecl("Overloads",
        [
            MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", new KeywordTypeNode("StringKeyword"))], ordinal: 0),
            MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", nullableString)], ordinal: 1),
        ]);

        var result = emitter.Emit(MakeSymbol("Overloads", "interface", [decl]));

        Assert.Equal(1, result.Source.Split("void Foo(").Length - 1);
        Assert.Contains(result.MemberOutcomes, m => m.Name == "foo" && (m.Reason ?? "").Contains("Deduplicated", StringComparison.Ordinal));
    }

    [Fact]
    public void InterfaceEmitter_RestParam_AnyArray_EmitsParamsObjectArray_NotDoubleArray()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var restParam = MakeParam("args", new ArrayTypeNode(new KeywordTypeNode("AnyKeyword")), rest: true);
        var result = emitter.Emit(MakeSymbol("RestHost", "interface",
        [
            MakeInterfaceDecl("RestHost",
            [
                MakeMethodMember("log", new KeywordTypeNode("VoidKeyword"), [restParam])
            ])
        ]));

        Assert.Contains("void Log(params object[] args);", result.Source);
        Assert.DoesNotContain("object[][]", result.Source);
    }

    [Fact]
    public void CallbackEmitter_RestParam_AnyArray_EmitsParamsObjectArray()
    {
        var emitter = new CallbackEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var fn = new FunctionTypeNode(
            [],
            [MakeParam("args", new ArrayTypeNode(new KeywordTypeNode("AnyKeyword")), rest: true)],
            new KeywordTypeNode("VoidKeyword"));

        var source = emitter.Emit(MakeSymbol("LogCallback", "callback", [MakeTypeAliasDecl("LogCallback", fn)]));

        Assert.Contains("public delegate void LogCallback(params object[] args);", source);
        Assert.DoesNotContain("object[][]", source);
    }

    [Fact]
    public void InterfaceEmitter_MergedDecl_RequiredThenOptional_EmitsOptional()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var nullableString = new UnionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new LiteralTypeNode("NullKeyword", "null")
        ]);
        var decl0 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", new KeywordTypeNode("StringKeyword"))])],
            ordinal: 0);
        var decl1 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", nullableString, optional: true)])],
            ordinal: 1);

        var result = emitter.Emit(MakeSymbol("MethodHost", "interface", [decl0, decl1]));

        Assert.Contains("void Foo(string? x = default);", result.Source);
        Assert.DoesNotContain("void Foo(string x);", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_MergedDecl_OptionalThenRequired_EmitsOptional()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var nullableString = new UnionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new LiteralTypeNode("NullKeyword", "null")
        ]);
        var decl0 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", nullableString, optional: true)])],
            ordinal: 0);
        var decl1 = MakeInterfaceDecl("MethodHost",
            [MakeMethodMember("foo", new KeywordTypeNode("VoidKeyword"), [MakeParam("x", new KeywordTypeNode("StringKeyword"))])],
            ordinal: 1);

        var result = emitter.Emit(MakeSymbol("MethodHost", "interface", [decl0, decl1]));

        Assert.Contains("void Foo(string? x = default);", result.Source);
        Assert.DoesNotContain("void Foo(string x);", result.Source);
    }

    [Fact]
    public void InterfaceEmitter_LaterMemberFailure_EarlierMembersHaveOutcomes()
    {
        var emitter = new InterfaceEmitter(EmptyResolver(), "1.0.0", "Blazor.DOM");
        var symbol = MakeSymbol("BrokenHost", "interface",
        [
            MakeInterfaceDecl("BrokenHost",
            [
                MakePropMember("name", new KeywordTypeNode("StringKeyword"), ordinal: 0),
                MakeMethodMember("bad", new KeywordTypeNode("VoidKeyword"), [MakeParam("value", new ReferenceTypeNode("NoSuchType", null, []))], ordinal: 1),
            ])
        ]);

        var ex = Assert.Throws<InterfaceEmitException>(() => emitter.Emit(symbol));

        Assert.Contains(ex.PartialOutcomes, o => o.Name == "name" && o.Status == MemberOutcomeStatus.Projected);
        Assert.Contains(ex.PartialOutcomes, o => o.Name == "bad" && o.Status == MemberOutcomeStatus.Failed);
    }

    [Fact]
    public void InterfaceEmitter_AsymmetricAccessor_ProjectsBothSourceOutcomes()
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
        Assert.Equal(
            2,
            result.MemberOutcomes.Count(outcome =>
                outcome.Name == "relList"
                && outcome.Status == MemberOutcomeStatus.Projected));
    }

    [Fact]
    public void MemberOutcomeStatus_Failed_RecordedByPipeline()
    {
        var broken = MakeSymbol("BrokenHost", "interface",
        [
            MakeInterfaceDecl("BrokenHost",
            [
                MakePropMember("name", new KeywordTypeNode("StringKeyword"), ordinal: 0),
                MakeMethodMember("bad", new KeywordTypeNode("VoidKeyword"), [MakeParam("value", new ReferenceTypeNode("NoSuchType", null, []))], ordinal: 1),
            ])
        ]);

        var outputDir = CreateTempDir();
        try
        {
            var result = GenerationPipeline.Run(new IrBundle(CreateDummyManifest(), [broken], []), outputDir);

            Assert.Single(result.Errors);
            Assert.Equal(1, result.Manifest.Accounting.GenerationFailed);
            Assert.Contains(result.Manifest.Accounting.FailedMemberEntries, m => m.SymbolName == "BrokenHost" && m.MemberName == "bad");
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void InterfaceEmitter_DictionaryBase_UsesStructuralContract()
    {
        var symbol = MakeSymbol("CountQueuingStrategy", "interface",
        [
            MakeInterfaceDecl("CountQueuingStrategy", [], heritage:
            [
                new HeritageClauseModel("extends", [new HeritageReferenceTypeNode("QueuingStrategy", null, [])])
            ])
        ]);
        var dictionarySymbol = MakeSemanticSymbol(
            "QueuingStrategy",
            "dictionary",
            [MakeInterfaceDecl("QueuingStrategy", [])]);
        var resolver = new TypeResolver([dictionarySymbol, symbol]);
        var emitter = new InterfaceEmitter(resolver, "1.0.0", "Blazor.DOM");

        var result = emitter.Emit(symbol);
        var dictionary = new DictionaryEmitter(
            resolver,
            "1.0.0",
            "Blazor.DOM").Emit(
                dictionarySymbol);

        Assert.Contains(
            "public partial interface ICountQueuingStrategy : IQueuingStrategyContract",
            result.Source);
        Assert.Contains("public interface IQueuingStrategyContract", dictionary);
        Assert.Contains(
            "public record QueuingStrategy : IQueuingStrategyContract",
            dictionary);
    }

    [Fact]
    public void InterfaceEmitter_NonExtendsHeritage_FailsClosed()
    {
        var emitter = new InterfaceEmitter(WithInterfaces("BaseType"), "1.0.0", "Blazor.DOM");
        var symbol = MakeSymbol("Derived", "interface",
        [
            MakeInterfaceDecl("Derived", [], heritage:
            [
                new HeritageClauseModel("implements", [new HeritageReferenceTypeNode("BaseType", null, [])])
            ])
        ]);

        var ex = Assert.Throws<InterfaceEmitException>(() => emitter.Emit(symbol));

        Assert.Contains("unsupported heritage clause token 'implements'", ex.Message);
    }

    [Fact]
    public void ProfilePipeline_Run_PromotesTrueVerifiedCoverage_AndRemovesStaleFiles()
    {
        var outputDir = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "TinyProfile",
                "tiny",
                ["TinyEnum"],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/TinyProfile");
            var symbol = MakeEnumSymbol("TinyEnum");
            var canonicalDir = Path.Combine(outputDir, "Profiles", "TinyProfile");
            Directory.CreateDirectory(canonicalDir);
            File.WriteAllText(Path.Combine(canonicalDir, "stale.g.cs"), "// stale");

            var result = ProfilePipeline.Run(profile, new IrBundle(CreateDummyManifest(), [symbol], []), outputDir);
            var coveragePath = Path.Combine(canonicalDir, "profile-coverage.json");

            Assert.True(result.Coverage.ByteIdentityVerified);
            Assert.True(File.Exists(coveragePath));
            Assert.DoesNotContain("\r", File.ReadAllText(coveragePath));
            Assert.DoesNotContain("\"byteIdentityVerified\": false", File.ReadAllText(coveragePath));
            Assert.Contains("\"byteIdentityVerified\": true", File.ReadAllText(coveragePath));
            Assert.False(File.Exists(Path.Combine(canonicalDir, "stale.g.cs")));
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Program_PromotionCleanup_RemovesStaleFiles()
    {
        var sourceDir = CreateTempDir();
        var targetDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(sourceDir, "Fresh.g.cs"), "fresh");
            Directory.CreateDirectory(Path.Combine(targetDir, "Nested"));
            File.WriteAllText(Path.Combine(targetDir, "Nested", "Stale.g.cs"), "stale");

            OutputDirectoryUtilities.DeleteDirectoryContents(targetDir);
            OutputDirectoryUtilities.CopyDirectory(sourceDir, targetDir, overwrite: true);

            Assert.False(File.Exists(Path.Combine(targetDir, "Nested", "Stale.g.cs")));
            Assert.True(File.Exists(Path.Combine(targetDir, "Fresh.g.cs")));
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public void OutputWriter_WriteManifest_NormalizesLineFeedOnly()
    {
        var outputDir = CreateTempDir();
        try
        {
            var writer = new OutputWriter(outputDir);
            writer.WriteManifest(new { Name = "Example", Values = new[] { "a", "b" } });

            var bytes = File.ReadAllBytes(Path.Combine(outputDir, "emitter-manifest.json"));
            Assert.DoesNotContain((byte)'\r', bytes);
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void ProfilePipeline_WriteProfileCoverage_NormalizesLineFeedOnly()
    {
        var outputDir = CreateTempDir();
        try
        {
            var writeMethod = typeof(ProfilePipeline).GetMethod("WriteProfileCoverage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(writeMethod);

            var report = new ProfileCoverageReport(
                "Test",
                "desc",
                ["Root"],
                [],
                [],
                false,
                false,
                1,
                1,
                0,
                [],
                new AccountingSummary(1, 1, 1, 0, 0, 0, 0, ["Root"], [], [], [], [], []),
                [],
                [],
                true,
                []);

            writeMethod!.Invoke(null, [outputDir, report]);

            var bytes = File.ReadAllBytes(Path.Combine(outputDir, "profile-coverage.json"));
            Assert.DoesNotContain((byte)'\r', bytes);
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void CallbackEmitter_BlobCallback_EmitsIBlobNullable_NotObject()
    {
        var blobSymbol = new SymbolModel(0, "Blob", 0,
            [MakeInterfaceDecl("Blob", [])], false,
            new SemanticModel("matched", "Blob", "definition", null, ["interface"],
                [], [], false, false, [], true, false, false, [], []));
        var resolver = new TypeResolver([blobSymbol]);
        var emitter = new CallbackEmitter(resolver, "1.0.0", "Blazor.DOM");

        var blobOrNull = new UnionTypeNode([
            new ReferenceTypeNode("Blob", null, []),
            new LiteralTypeNode("NullKeyword", "null"),
        ]);
        var fn = new FunctionTypeNode(
            [],
            [new ParameterModel(0, "blob", false, false, blobOrNull, null,
                new DocumentationModel("", [], false),
                new LocationModel("test", new(0, 0, 0), new(0, 0, 0)))],
            new KeywordTypeNode("VoidKeyword"));
        var symbol = MakeSymbol("BlobCallback", "callback",
            [MakeTypeAliasDecl("BlobCallback", fn)]);

        var source = emitter.Emit(symbol);

        Assert.Contains("IBlob?", source);
        Assert.DoesNotContain("object blob", source);
        Assert.DoesNotContain("object?", source);
    }

    private static TypeResolver EmptyResolver() => new([]);

    private static TypeResolver WithInterfaces(params string[] names) =>
        new(names.Select((name, ordinal) => MakeSemanticSymbol(name, "interface", [MakeInterfaceDecl(name, [])], ordinal)).ToList());

    private static SymbolModel MakeSemanticSymbol(string name, string classification, IReadOnlyList<DeclarationModel> decls, int ordinal = 0)
        => new(ordinal, name, 0, decls, false, new SemanticModel(
            "matched", name, "definition", null, [classification],
            [], [], false, false, [], false, false, false, [], []));

    private static SymbolModel MakeSymbol(string name, string classification, IReadOnlyList<DeclarationModel> decls)
        => MakeSemanticSymbol(name, classification, decls);

    private static SymbolModel MakeEnumSymbol(string name)
        => new(0, name, 0,
            [new DeclarationModel(0, "typeAlias", name, [], [], [], [],
                new UnionTypeNode([new LiteralTypeNode("StringLiteral", "\"x\"")]),
                [], null, new DocumentationModel("", [], false),
                new LocationModel("test", new(0, 0, 0), new(0, 0, 0)),
                null, false, new EventMapModel(false, []), [])],
            false,
            new SemanticModel("matched", name, "definition", null, ["enum"],
                [], [], false, false, [], false, false, false, [], []));

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

    private static ParameterModel MakeParam(string name, TypeNode type, bool optional = false, bool rest = false)
        => new(0, name, optional, rest, type, null,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)));

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

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
