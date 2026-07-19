// Tests for TransitiveDependencyResolver, ProfileLoader, and ProfilePipeline.

using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Profiles;
using System.Text.Json;
using System.Text;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class ProfileSystemTests
{
    // ── TransitiveDependencyResolver ────────────────────────────────────────────

    [Fact]
    public void Resolve_EmptyRoots_ReturnsEmpty()
    {
        var index = new Dictionary<string, SymbolModel>();
        var result = TransitiveDependencyResolver.Resolve([], index);
        Assert.Empty(result);
    }

    [Fact]
    public void Resolve_RootNotInIndex_IncludesRootByName()
    {
        var index = new Dictionary<string, SymbolModel>();
        var result = TransitiveDependencyResolver.Resolve(["UnknownType"], index);
        Assert.Contains("UnknownType", result);
    }

    [Fact]
    public void Resolve_FollowsReferenceTypeNodes()
    {
        // Foo references Bar; Bar references nothing
        var barDecl = MakeDecl("interface", []);
        var bar = MakeSymbol("Bar", [barDecl]);

        var fooDecl = MakeDecl("interface",
            [MakeProp(new ReferenceTypeNode("Bar", null, []))]);
        var foo = MakeSymbol("Foo", [fooDecl]);

        var index = new Dictionary<string, SymbolModel>
        {
            ["Foo"] = foo,
            ["Bar"] = bar,
        };

        var result = TransitiveDependencyResolver.Resolve(["Foo"], index);
        Assert.Contains("Foo", result);
        Assert.Contains("Bar", result);
    }

    [Fact]
    public void Resolve_FollowsHeritageReferences()
    {
        var baseDecl = MakeDecl("interface", []);
        var baseSymbol = MakeSymbol("BaseType", [baseDecl]);

        var childDecl = MakeDeclWithHeritage("interface",
            [new HeritageReferenceTypeNode("BaseType", null, [])]);
        var child = MakeSymbol("ChildType", [childDecl]);

        var index = new Dictionary<string, SymbolModel>
        {
            ["ChildType"] = child,
            ["BaseType"] = baseSymbol,
        };

        var result = TransitiveDependencyResolver.Resolve(["ChildType"], index);
        Assert.Contains("ChildType", result);
        Assert.Contains("BaseType", result);
    }

    [Fact]
    public void Resolve_HandlesCircularReferences_NoCycle()
    {
        // A → B → A (circular)
        var aDecl = MakeDecl("interface",
            [MakeProp(new ReferenceTypeNode("B", null, []))]);
        var a = MakeSymbol("A", [aDecl]);

        var bDecl = MakeDecl("interface",
            [MakeProp(new ReferenceTypeNode("A", null, []))]);
        var b = MakeSymbol("B", [bDecl]);

        var index = new Dictionary<string, SymbolModel>
        {
            ["A"] = a,
            ["B"] = b,
        };

        var result = TransitiveDependencyResolver.Resolve(["A"], index);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.Equal(2, result.Count);  // No infinite loop
    }

    [Fact]
    public void Resolve_FollowsUnionTypeMembers()
    {
        var refDecl = MakeDecl("interface", []);
        var refSym = MakeSymbol("RefType", [refDecl]);

        var unionNode = new UnionTypeNode([
            new ReferenceTypeNode("RefType", null, []),
            new KeywordTypeNode("StringKeyword"),
        ]);

        var hostDecl = MakeDecl("interface", [MakeProp(unionNode)]);
        var host = MakeSymbol("HostType", [hostDecl]);

        var index = new Dictionary<string, SymbolModel>
        {
            ["HostType"] = host,
            ["RefType"] = refSym,
        };

        var result = TransitiveDependencyResolver.Resolve(["HostType"], index);
        Assert.Contains("RefType", result);
    }

    [Fact]
    public void Resolve_FollowsArrayElementType()
    {
        var elemDecl = MakeDecl("interface", []);
        var elemSym = MakeSymbol("ElemType", [elemDecl]);

        var arrayNode = new ArrayTypeNode(new ReferenceTypeNode("ElemType", null, []));
        var hostDecl = MakeDecl("interface", [MakeProp(arrayNode)]);
        var host = MakeSymbol("HostType", [hostDecl]);

        var index = new Dictionary<string, SymbolModel>
        {
            ["HostType"] = host,
            ["ElemType"] = elemSym,
        };

        var result = TransitiveDependencyResolver.Resolve(["HostType"], index);
        Assert.Contains("ElemType", result);
    }

    // ── ProfileLoader ───────────────────────────────────────────────────────────

    [Fact]
    public void LoadAll_EmptyDirectory_ReturnsEmpty()
    {
        var dir = CreateTempDir();
        try
        {
            var profiles = ProfileLoader.LoadAll(dir);
            Assert.Empty(profiles);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_ValidProfile_DeserializesCorrectly()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "name": "TestProfile",
                    "description": "A test profile",
                    "rootSymbols": ["TypeA", "TypeB"],
                    "secureContext": true,
                    "requiresUserActivation": false,
                    "features": ["feature-x"],
                    "outputNamespace": "Test.Namespace",
                    "outputSubdirectory": "Profiles/Test"
                }
                """;
            var path = Path.Combine(dir, "test.profile.json");
            File.WriteAllText(path, json);

            var profile = ProfileLoader.Load(path);
            Assert.Equal("TestProfile", profile.Name);
            Assert.Equal(["TypeA", "TypeB"], profile.RootSymbols);
            Assert.True(profile.SecureContext);
            Assert.Equal("Test.Namespace", profile.OutputNamespace);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadAll_LoadsAllProfileJsonFiles()
    {
        var dir = CreateTempDir();
        try
        {
            WriteProfileFile(dir, "Alpha.profile.json", "Alpha");
            WriteProfileFile(dir, "Beta.profile.json", "Beta");

            var profiles = ProfileLoader.LoadAll(dir);
            Assert.Equal(2, profiles.Count);
            var names = profiles.Select(p => p.Name).OrderBy(n => n).ToList();
            Assert.Equal(["Alpha", "Beta"], names);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteProfileFile(string dir, string filename, string name)
    {
        var json = $@"{{
            ""name"": ""{name}"",
            ""description"": ""Description"",
            ""rootSymbols"": [],
            ""secureContext"": false,
            ""requiresUserActivation"": false,
            ""features"": [],
            ""outputNamespace"": ""Blazor.DOM"",
            ""outputSubdirectory"": ""Profiles/{name}""
        }}";
        File.WriteAllText(Path.Combine(dir, filename), json);
    }

    private static SymbolModel MakeSymbol(string name, IReadOnlyList<DeclarationModel> decls)
        => new(0, name, 0, decls, false, new SemanticModel(
            "matched", null, null, null, [], [], [], false, false, [], false, false, false, [], []));

    private static DeclarationModel MakeDecl(
        string kind, IReadOnlyList<MemberModel> members)
        => new(0, kind, "name", [], [], [], members,
            null, [], null, new DocumentationModel("", [], false),
            new LocationModel("", new PositionModel(0, 0, 0), new PositionModel(0, 0, 0)),
            null, false, new EventMapModel(false, []), []);

    private static DeclarationModel MakeDeclWithHeritage(
        string kind, IReadOnlyList<TypeNode> heritageTypes)
        => new(0, kind, "name", [],
            [],
            [new HeritageClauseModel("extends", heritageTypes)],
            [],
            null, [], null, new DocumentationModel("", [], false),
            new LocationModel("", new PositionModel(0, 0, 0), new PositionModel(0, 0, 0)),
            null, false, new EventMapModel(false, []), []);

    private static MemberModel MakeProp(TypeNode type)
        => new(0, "property", new NameNode("identifier", "prop"),
            false, false, false, [], [],
            type, null,
            new DocumentationModel("", [], false),
            new LocationModel("", new PositionModel(0, 0, 0), new PositionModel(0, 0, 0)));
}
