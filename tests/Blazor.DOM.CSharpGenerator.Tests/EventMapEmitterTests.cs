using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Blazor.DOM.CSharpGenerator.Profiles;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class EventMapEmitterTests
{
    [Fact]
    public void Emit_MergesInheritanceAndPreservesExactEventMetadata()
    {
        var eventType = Interface("Event");
        var baseMap = EventMap(
            "BaseEventMap",
            [Event("ready-state", eventType, "Ready docs.")]);
        var derived = EventMap(
            "DerivedEventMap",
            [
                Event("ready-state", eventType, "Merged docs.", deprecated: true),
                Event("event", eventType, "Keyword event."),
            ],
            ["BaseEventMap"]);
        var symbols = new[] { eventType, baseMap, derived };

        var result = new EventMapEmitter(
            new TypeResolver(symbols),
            "1.0.0",
            "Blazor.DOM",
            symbols).Emit(derived);

        Assert.Contains("public static partial class DerivedEventMap", result.Source);
        Assert.Contains("\"ready-state\"", result.Source);
        Assert.Contains("DomEventDescriptor<IEvent>", result.Source);
        Assert.Contains("[Obsolete]", result.Source);
        Assert.Contains("BaseEventMap/decl[0]/member[0]/ready-state", result.Source);
        Assert.Contains("DerivedEventMap/decl[0]/member[0]/ready-state", result.Source);
        Assert.Equal(2, result.MemberOutcomes.Count);
        Assert.All(result.MemberOutcomes, outcome =>
            Assert.Equal(MemberOutcomeStatus.Projected, outcome.Status));
    }

    [Fact]
    public void Emit_DisambiguatesCasingAndConflictingPayloadsDeterministically()
    {
        var eventType = Interface("Event");
        var mouseType = Interface("MouseEvent");
        var first = EventMap(
            "FirstEventMap",
            [
                Event("foo-bar", eventType),
                Event("FooBar", eventType),
                Event("change", eventType),
            ]);
        var second = EventMap(
            "SecondEventMap",
            [Event("change", mouseType)],
            ["FirstEventMap"]);
        var symbols = new[] { eventType, mouseType, first, second };
        var emitter = new EventMapEmitter(
            new TypeResolver(symbols),
            "1.0.0",
            "Blazor.DOM",
            symbols);

        var firstRun = emitter.Emit(second).Source;
        var secondRun = emitter.Emit(second).Source;

        Assert.Equal(firstRun, secondRun);
        Assert.Contains("FooBar_", firstRun);
        Assert.Equal(2, Count(firstRun, "\"change\""));
        Assert.Contains("DomEventDescriptor<IEvent>", firstRun);
        Assert.Contains("DomEventDescriptor<IMouseEvent>", firstRun);
    }

    [Fact]
    public void Emit_JsonPayloadCreatesTypedValueDescriptor()
    {
        var invalid = EventMap(
            "InvalidEventMap",
            [Event("count", new KeywordTypeNode("NumberKeyword")
            {
                CheckerType = "number",
                Transport = new TransportModel(
                    "json-value", false, "number", false, true, null),
            })]);

        var result = new EventMapEmitter(
            new TypeResolver([invalid]),
            "1.0.0",
            "Blazor.DOM",
            [invalid]).Emit(invalid);

        Assert.Contains("DomEventDescriptor<double>.Value(", result.Source);
        Assert.Contains("\"count\"", result.Source);
    }

    [Fact]
    public void Corpus_TypedEventsProfile_IsFailureFreeAndByteIdentical()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var profile = ProfileLoader.Load(Path.Combine(
            root,
            "data",
            "Blazor.DOM.Profiles",
            "TypedEvents.profile.json"));
        var output = Path.Combine(
            root,
            "artifacts",
            $"typed-events-profile-test-{Guid.NewGuid():N}");
        try
        {
            var result = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data));

            Assert.True(
                result.PipelineResult.Errors.Count == 0,
                "PROFILE_ERRORS: " + string.Join(
                    Environment.NewLine,
                    result.PipelineResult.Errors.Select(error =>
                        $"{error.SymbolName}: {error.Message}")));
            Assert.True(result.PipelineResult.Validation.IsValid);
            Assert.True(result.Coverage.ByteIdentityVerified);
            Assert.Empty(result.PipelineResult.Manifest.Accounting.DeferredSymbols);
            Assert.Empty(
                result.PipelineResult.Manifest.Accounting.DeferredMemberEntries);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    private static int Count(string value, string text)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(text, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += text.Length;
        }
        return count;
    }

    private static MemberModel Event(
        string name,
        SymbolModel payload,
        string documentation = "",
        bool deprecated = false) =>
        Event(name, new ReferenceTypeNode(payload.Name, payload.Name, [])
        {
            CheckerType = payload.Name,
            Transport = new TransportModel(
                "js-reference",
                false,
                payload.Name,
                false,
                false,
                "Live DOM event object."),
        }, documentation, deprecated);

    private static MemberModel Event(
        string name,
        TypeNode payload,
        string documentation = "",
        bool deprecated = false) =>
        new(
            0,
            "property",
            new NameNode("string", name),
            false,
            true,
            false,
            [],
            [],
            payload,
            null,
            new DocumentationModel(documentation, [], deprecated),
            Location());

    private static SymbolModel EventMap(
        string name,
        IReadOnlyList<MemberModel> members,
        IReadOnlyList<string>? bases = null) =>
        Symbol(
            name,
            [
                Declaration(
                    name,
                    members.Select((member, ordinal) =>
                        member with { Ordinal = ordinal }).ToList(),
                    bases ?? [],
                    eventMap: true)
            ],
            "interface");

    private static SymbolModel Interface(string name) =>
        Symbol(name, [Declaration(name, [], [], eventMap: false)], "interface");

    private static SymbolModel Symbol(
        string name,
        IReadOnlyList<DeclarationModel> declarations,
        string classification) =>
        new(
            0,
            name,
            64,
            declarations,
            false,
            new SemanticModel(
                "matched",
                name,
                "definition",
                null,
                [classification],
                ["dom"],
                ["Window"],
                true,
                false,
                [],
                false,
                false,
                false,
                [],
                []));

    private static DeclarationModel Declaration(
        string name,
        IReadOnlyList<MemberModel> members,
        IReadOnlyList<string> bases,
        bool eventMap) =>
        new(
            0,
            "interface",
            name,
            [],
            [],
            bases.Count == 0
                ? []
                : [new HeritageClauseModel(
                    "extends",
                    bases.Select(baseName => (TypeNode)new HeritageReferenceTypeNode(
                        baseName,
                        baseName,
                        [])).ToList())],
            members,
            null,
            [],
            null,
            new DocumentationModel("", [], false),
            Location(),
            null,
            false,
            new EventMapModel(eventMap, members
                .Select(member => member.Name!.Text)
                .ToList()),
            []);

    private static LocationModel Location() =>
        new("fixture.d.ts", new(1, 1, 0), new(1, 2, 1));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "blazorators.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
