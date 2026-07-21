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
                    "permissions": ["permission-x"],
                    "outputNamespace": "Test.Namespace",
                    "outputSubdirectory": "Profiles/Test",
                    "minimalDependencyContracts": true,
                    "memberIncludes": {
                        "TypeA": ["run@0/1"]
                    }
                }
                """;
            var path = Path.Combine(dir, "test.profile.json");
            File.WriteAllText(path, json);

            var profile = ProfileLoader.Load(path);
            Assert.Equal("TestProfile", profile.Name);
            Assert.Equal(["TypeA", "TypeB"], profile.RootSymbols);
            Assert.True(profile.SecureContext);
            Assert.Equal("Test.Namespace", profile.OutputNamespace);
            Assert.True(profile.MinimalDependencyContracts);
            Assert.Equal(["run@0/1"], profile.MemberIncludes!["TypeA"]);
            Assert.Equal(["permission-x"], profile.Permissions);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_ReviewedTransportOverride_RequiresRationale()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "name": "Invalid",
                    "description": "Invalid transport override",
                    "rootSymbols": ["Root"],
                    "secureContext": false,
                    "requiresUserActivation": false,
                    "features": [],
                    "outputNamespace": "Blazor.DOM",
                    "outputSubdirectory": "Profiles/Invalid",
                    "transportOverrides": [
                        {
                            "symbol": "Root",
                            "member": "result",
                            "kind": "runtime-inferred",
                            "rationale": ""
                        }
                    ]
                }
                """;
            var path = Path.Combine(dir, "invalid.profile.json");
            File.WriteAllText(path, json);

            var exception = Assert.Throws<InvalidDataException>(
                () => ProfileLoader.Load(path));

            Assert.Contains("non-empty rationale", exception.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_ReviewedJsReferenceOverride_IsAccepted()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "name": "Valid",
                    "description": "Reviewed live iterator transport",
                    "rootSymbols": ["Root"],
                    "secureContext": false,
                    "requiresUserActivation": false,
                    "features": [],
                    "outputNamespace": "Blazor.DOM",
                    "outputSubdirectory": "Profiles/Valid",
                    "transportOverrides": [
                        {
                            "symbol": "Root",
                            "member": "values",
                            "kind": "js-reference",
                            "rationale": "The result retains live JavaScript iterator identity."
                        }
                    ]
                }
                """;
            var path = Path.Combine(dir, "valid.profile.json");
            File.WriteAllText(path, json);

            var profile = ProfileLoader.Load(path);

            var transportOverride = Assert.Single(profile.TransportOverrides!);
            Assert.Equal("js-reference", transportOverride.Kind);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_ReviewedExclusion_RequiresRationale()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "name": "Invalid",
                    "description": "Invalid reviewed exclusion",
                    "rootSymbols": ["Root"],
                    "secureContext": false,
                    "requiresUserActivation": false,
                    "features": [],
                    "outputNamespace": "Blazor.DOM",
                    "outputSubdirectory": "Profiles/Invalid",
                    "reviewedExclusions": [
                        {
                            "symbol": "Root",
                            "member": "unsupported",
                            "rationale": ""
                        }
                    ]
                }
                """;
            var path = Path.Combine(dir, "invalid.profile.json");
            File.WriteAllText(path, json);

            var exception = Assert.Throws<InvalidDataException>(
                () => ProfileLoader.Load(path));

            Assert.Contains("non-empty rationale", exception.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_PackageProfileWithInvalidEntryPointPath_IsRejected()
    {
        var dir = CreateTempDir();
        try
        {
            var json = """
                {
                    "name": "Invalid",
                    "description": "Invalid entry point",
                    "rootSymbols": ["Root"],
                    "secureContext": false,
                    "requiresUserActivation": false,
                    "features": [],
                    "outputNamespace": "Blazor.DOM",
                    "outputSubdirectory": "Profiles/Invalid",
                    "entryPoints": [
                        {
                            "name": "Root",
                            "symbol": "Root",
                            "javaScriptPath": "navigator[\"root\"]"
                        }
                    ]
                }
                """;
            var path = Path.Combine(dir, "invalid.profile.json");
            File.WriteAllText(path, json);

            Assert.Throws<InvalidDataException>(() => ProfileLoader.Load(path));
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

    public static TheoryData<string> MaliciousOutputSubdirectories => new()
    {
        "",
        ".",
        "..",
        "Profiles",
        "Profiles/.",
        "Profiles/..",
        "Profiles/../Escape",
        "Profiles/Nested/../../Escape",
        "Profiles//Escape",
        "Profiles\\Escape",
        "Profiles\\..\\Escape",
        "/Profiles/Escape",
        "//server/share/Escape",
        "C:/Escape",
        "C:Profiles/Escape",
        "Profiles/C:Escape",
        "Profiles/WakeLock.",
        "Profiles/WakeLock ",
        "Profiles/CON",
        "Profiles/con.txt",
        "Profiles/AUX/Child",
        "Profiles/COM1",
        "Profiles/LPT9.log",
        "Profiles/Invalid?",
    };

    [Theory]
    [MemberData(nameof(MaliciousOutputSubdirectories))]
    public void Load_MaliciousOutputSubdirectory_IsRejected(string outputSubdirectory)
    {
        var dir = CreateTempDir();
        try
        {
            WriteProfileFile(
                dir,
                "malicious.profile.json",
                "Malicious",
                outputSubdirectory);

            Assert.Throws<InvalidDataException>(
                () => ProfileLoader.Load(
                    Path.Combine(dir, "malicious.profile.json")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [MemberData(nameof(MaliciousOutputSubdirectories))]
    public void ProfilePipeline_MaliciousOutputSubdirectory_FailsBeforeWriting(
        string outputSubdirectory)
    {
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "Malicious",
                "fixture",
                [],
                false,
                false,
                [],
                "Blazor.DOM",
                outputSubdirectory);

            Assert.Throws<InvalidDataException>(
                () => ProfilePipeline.Run(
                    profile,
                    new IrBundle(CreateDummyManifest(), [], []),
                    output));
            Assert.Empty(Directory.EnumerateFileSystemEntries(output));
        }
        finally { Directory.Delete(output, true); }
    }

    [Fact]
    public void LoadAll_CaseFoldedProfileDestinations_AreRejectedBeforeGeneration()
    {
        var dir = CreateTempDir();
        try
        {
            WriteProfileFile(
                dir,
                "first.profile.json",
                "First",
                "Profiles/WakeLock");
            WriteProfileFile(
                dir,
                "second.profile.json",
                "Second",
                "Profiles/wakelock");

            var exception = Assert.Throws<InvalidDataException>(
                () => ProfileLoader.LoadAll(dir));
            Assert.Contains("aliases", exception.Message);
            Assert.Contains("portable path normalization", exception.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("Profiles/wakelock")]
    [InlineData("Profiles/WAKELOCK")]
    public void ProfilePipeline_ExistingCaseFoldedDestination_FailsBeforeWriting(
        string outputSubdirectory)
    {
        var output = CreateTempDir();
        try
        {
            var canonical = Path.Combine(output, "Profiles", "WakeLock");
            Directory.CreateDirectory(canonical);
            File.WriteAllText(
                Path.Combine(canonical, "sentinel.g.cs"),
                "unchanged\n",
                Encoding.UTF8);
            var before = SnapshotTree(output);
            var profile = new ProfileDefinition(
                "Alias",
                "fixture",
                [],
                false,
                false,
                [],
                "Blazor.DOM",
                outputSubdirectory);

            var exception = Assert.Throws<InvalidDataException>(
                () => ProfilePipeline.Run(
                    profile,
                    new IrBundle(CreateDummyManifest(), [], []),
                    output));

            Assert.Contains("aliases existing portable path", exception.Message);
            AssertTreesEqual(before, SnapshotTree(output));
        }
        finally { Directory.Delete(output, true); }
    }

    [Fact]
    public void ProfilePipeline_ExistingUnicodeNormalizedDestination_FailsBeforeWriting()
    {
        var output = CreateTempDir();
        try
        {
            var canonical = Path.Combine(output, "Profiles", "Caf\u00e9");
            Directory.CreateDirectory(canonical);
            File.WriteAllText(
                Path.Combine(canonical, "sentinel.g.cs"),
                "unchanged\n",
                Encoding.UTF8);
            var before = SnapshotTree(output);
            var profile = new ProfileDefinition(
                "Alias",
                "fixture",
                [],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/Cafe\u0301");

            Assert.Throws<InvalidDataException>(
                () => ProfilePipeline.Run(
                    profile,
                    new IrBundle(CreateDummyManifest(), [], []),
                    output));

            AssertTreesEqual(before, SnapshotTree(output));
        }
        finally { Directory.Delete(output, true); }
    }

    [Fact]
    public void ProfilePipeline_ExistingReparsePointCannotEscapeProfilesRoot()
    {
        var output = CreateTempDir();
        try
        {
            var profiles = Path.Combine(output, "Profiles");
            var outside = Path.Combine(output, "outside");
            var link = Path.Combine(profiles, "linked");
            Directory.CreateDirectory(profiles);
            Directory.CreateDirectory(outside);

            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                    or PlatformNotSupportedException
                    or IOException)
            {
                return;
            }

            var profile = new ProfileDefinition(
                "Linked",
                "fixture",
                [],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/linked/Escape");

            Assert.Throws<InvalidDataException>(
                () => ProfilePipeline.Run(
                    profile,
                    new IrBundle(CreateDummyManifest(), [], []),
                    output));
            Assert.False(Directory.Exists(Path.Combine(outside, "Escape")));
        }
        finally { Directory.Delete(output, true); }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Dictionary<string, byte[]> SnapshotTree(string directory)
    {
        var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in Directory
            .EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            snapshot.Add(
                $"D:{Path.GetRelativePath(directory, path)}",
                []);
        }
        foreach (var path in Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            snapshot.Add(
                $"F:{Path.GetRelativePath(directory, path)}",
                File.ReadAllBytes(path));
        }
        return snapshot;
    }

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

    private static void WriteProfileFile(
        string dir,
        string filename,
        string name,
        string? outputSubdirectory = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            name,
            description = "Description",
            rootSymbols = Array.Empty<string>(),
            secureContext = false,
            requiresUserActivation = false,
            features = Array.Empty<string>(),
            outputNamespace = "Blazor.DOM",
            outputSubdirectory = outputSubdirectory ?? $"Profiles/{name}",
        });
        File.WriteAllText(Path.Combine(dir, filename), json);
    }

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
