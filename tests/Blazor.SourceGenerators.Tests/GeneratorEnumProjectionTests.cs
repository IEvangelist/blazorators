// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// C2 - end-to-end coverage for the <c>[JSAutoEnum]</c> attribute and
/// pipeline branch. Anchors a C# interface, runs the generator, and asserts
/// on the emitted enum + per-enum <c>JsonConverter&lt;TEnum&gt;</c> shape
/// alongside the BR0001/BR0006/BR0008/BR0009 diagnostics that surface when
/// the consumer asks for something we cannot project.
/// <para>
/// These tests deliberately exercise *behavior*, not raw source text, so
/// they remain robust under minor whitespace / comment tweaks in the
/// emitter. Snapshot-style byte-for-byte coverage lives in the higher-level
/// <c>GeneratorSnapshotTests</c>; here we just confirm that "the right
/// enum/converter/diagnostic shows up for the right input".
/// </para>
/// </summary>
public class GeneratorEnumProjectionTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    // A custom .d.ts the generator can be pointed at via
    // TypeDeclarationSources. Hosts both well-formed and pathological
    // aliases so each negative-path test has a stable, deterministic
    // fixture (the bundled lib.dom.d.ts evolves over time).
    private const string CustomDts = @"
type CustomReady = ""ready"" | ""busy"";
type CustomNotUnion = string;
type CustomCollide = ""a"" | ""A"";
";

    // A second custom .d.ts used by the multi-file TypeDeclarationSources
    // test. Only present so the "scan all parsers, prefer best
    // classification" logic gets exercised.
    private const string OtherDts = @"
type OtherAlias = ""one"" | ""two"";
";

    [Fact]
    public void EnumProjection_BareAttribute_InfersTypeNameFromAnchor()
    {
        // [JSAutoEnum] with no arguments should strip the leading "I"
        // from the anchor interface name and resolve the result
        // (`DocumentReadyState`) against the bundled lib.dom.d.ts.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum]
    public interface IDocumentReadyState { }
}";

        var result = GetRunResult(source);

        Assert.Empty(result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        // Hint name encodes the resolved namespace so co-named enums in
        // different namespaces never collide on AddSource. The format
        // mirrors the BuildHintName helper inside EnumProjectionEmitter.
        var generated = result.GeneratedTrees.FirstOrDefault(t =>
            Path.GetFileName(t.FilePath) ==
                "MyApp.Interop.DocumentReadyState.Enum.g.cs");

        Assert.NotNull(generated);

        var text = generated!.ToString();
        Assert.Contains("public enum DocumentReadyState", text);
        Assert.Contains("Complete", text);
        Assert.Contains("Interactive", text);
        Assert.Contains("Loading", text);
    }

    [Fact]
    public void EnumProjection_ExplicitTypeName_OverridesAnchorInference()
    {
        // The anchor interface name doesn't resemble the projected type;
        // the explicit TypeName must take precedence over inference.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(TypeName = ""InsertPosition"")]
    public interface IAnywhereYouLike { }
}";

        var result = GetRunResult(source);

        Assert.Empty(result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = result.GeneratedTrees.FirstOrDefault(t =>
            Path.GetFileName(t.FilePath) ==
                "MyApp.Interop.InsertPosition.Enum.g.cs");

        Assert.NotNull(generated);

        var text = generated!.ToString();
        Assert.Contains("public enum InsertPosition", text);

        // The TypeScript values for InsertPosition include hyphen-free
        // tokens that PascalCase verbatim, so each raw value is visible
        // both as an [EnumMember(Value="...")] decoration and as a
        // pure-PascalCase member identifier.
        Assert.Contains("Value = \"beforebegin\"", text);
        Assert.Contains("Value = \"afterbegin\"", text);
        Assert.Contains("Value = \"beforeend\"", text);
        Assert.Contains("Value = \"afterend\"", text);
    }

    [Fact]
    public void EnumProjection_EmitsCustomJsonConverter()
    {
        // The enum projection relies on its OWN JsonConverter rather than
        // System.Text.Json's built-in JsonStringEnumConverter, because the
        // latter does not honor [EnumMember(Value=...)] on every supported
        // .NET target. Verifying that the converter class is present (and
        // wired up via [JsonConverter] on the enum) guards against silent
        // regression to a serialization-broken state.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(TypeName = ""DocumentReadyState"")]
    public interface IReady { }
}";

        var result = GetRunResult(source);
        var text = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) ==
                "MyApp.Interop.DocumentReadyState.Enum.g.cs")
            .ToString();

        Assert.Contains(
            "[global::System.Text.Json.Serialization.JsonConverter(typeof(DocumentReadyStateJsonConverter))]",
            text);

        Assert.Contains(
            "public sealed class DocumentReadyStateJsonConverter " +
            ": global::System.Text.Json.Serialization.JsonConverter<DocumentReadyState>",
            text);

        // Read(...) / Write(...) overrides are required by the
        // JsonConverter<T> contract. Their absence would mean the
        // emitter produced a stub the consumer's compilation would
        // reject.
        Assert.Contains("public override DocumentReadyState Read(", text);
        Assert.Contains("public override void Write(", text);
    }

    [Fact]
    public void EnumProjection_AnchorNotPartial_StillEmits()
    {
        // [JSAutoEnum] anchors are pure discovery handles - the generated
        // enum is a sibling type, not an extension of the anchor's body -
        // so 'partial' must NOT be required. Regression guard against
        // accidentally borrowing the BR0005 gate from [JSAutoInterop].
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(TypeName = ""DocumentReadyState"")]
    public interface ICompletelyEmpty { }
}";

        var result = GetRunResult(source);

        Assert.Empty(result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.Contains(result.GeneratedTrees, t =>
            Path.GetFileName(t.FilePath) ==
                "MyApp.Interop.DocumentReadyState.Enum.g.cs");
    }

    [Fact]
    public void EnumProjection_GlobalNamespaceAnchor_OmitsNamespaceDeclaration()
    {
        // An anchor in the global namespace must produce a hint name
        // without a leading namespace dot and a source body with NO
        // `namespace ;` declaration (the C# compiler rejects an empty
        // namespace declaration).
        const string source = @"
[JSAutoEnum(TypeName = ""DocumentReadyState"")]
public interface IReady { }
";

        var result = GetRunResult(source);

        Assert.Empty(result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = result.GeneratedTrees.FirstOrDefault(t =>
            Path.GetFileName(t.FilePath) == "DocumentReadyState.Enum.g.cs");

        Assert.NotNull(generated);

        var text = generated!.ToString();
        Assert.DoesNotContain("namespace ;", text);
        Assert.DoesNotContain("namespace  ", text);
        Assert.Contains("public enum DocumentReadyState", text);
    }

    [Fact]
    public void EnumProjection_BR0001_WhenTypeNameInferenceFails()
    {
        // The anchor degenerates to an empty TypeName after stripping
        // the leading "I" and the trailing "Service" (BR0001 path).
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum]
    public interface IService { }
}";

        var result = GetRunResult(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "BR0001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void EnumProjection_BR0006_WhenAliasMissingFromAllSources()
    {
        // The DOM does not define `ThisAliasDoesNotExist`, and no
        // TypeDeclarationSources are configured, so the only resolved
        // parser (the bundled lib.dom.d.ts) reports AliasNotFound and we
        // emit BR0006.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(TypeName = ""ThisAliasDoesNotExist"")]
    public interface INope { }
}";

        var result = GetRunResult(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "BR0006" &&
                 d.GetMessage().Contains("ThisAliasDoesNotExist"));
    }

    [Fact]
    public void EnumProjection_BR0008_WhenAliasIsNotStringLiteralUnion()
    {
        // `CustomNotUnion` exists in the custom .d.ts but resolves to
        // `string`, not a string-literal union. BR0008 is the correct
        // failure mode; BR0006 would be wrong because the alias DOES
        // exist.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(
        TypeName = ""CustomNotUnion"",
        TypeDeclarationSources = new[] { ""custom.d.ts"" })]
    public interface INotUnion { }
}";

        var result = GetRunResult(source,
            additionalTexts: new[]
            {
                new InMemoryAdditionalText("custom.d.ts", CustomDts),
            });

        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "BR0008" && d.GetMessage().Contains("CustomNotUnion"));
    }

    [Fact]
    public void EnumProjection_BR0009_WhenMembersCollide()
    {
        // `CustomCollide` is `"a" | "A"` - both raw values PascalCase to
        // the same C# identifier ("A"), so the emitter must refuse to
        // produce an enum with duplicate members and surface BR0009.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(
        TypeName = ""CustomCollide"",
        TypeDeclarationSources = new[] { ""custom.d.ts"" })]
    public interface ICollide { }
}";

        var result = GetRunResult(source,
            additionalTexts: new[]
            {
                new InMemoryAdditionalText("custom.d.ts", CustomDts),
            });

        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "BR0009" && d.GetMessage().Contains("CustomCollide"));
    }

    [Fact]
    public void EnumProjection_SameTypeNameDifferentNamespaces_BothEmit()
    {
        // Hint names are keyed by (effectiveNamespace, enumName) so two
        // anchors projecting the same TypeName from different namespaces
        // must both succeed. AddSource throwing here would surface as a
        // generator crash.
        const string source = @"
namespace First
{
    [JSAutoEnum(TypeName = ""DocumentReadyState"")]
    public interface IReady { }
}

namespace Second
{
    [JSAutoEnum(TypeName = ""DocumentReadyState"")]
    public interface IReady { }
}";

        var result = GetRunResult(source);

        Assert.Empty(result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.Contains(result.GeneratedTrees, t =>
            Path.GetFileName(t.FilePath) ==
                "First.DocumentReadyState.Enum.g.cs");
        Assert.Contains(result.GeneratedTrees, t =>
            Path.GetFileName(t.FilePath) ==
                "Second.DocumentReadyState.Enum.g.cs");
    }

    [Fact]
    public void EnumProjection_DuplicateIdentity_SkipsSilently()
    {
        // Two anchors in the same namespace pointing at the same TS
        // alias produce one (effectiveNamespace, enumName) identity and
        // therefore exactly one output file. Without dedup, AddSource
        // would throw on the second hint-name registration.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(TypeName = ""DocumentReadyState"")]
    public interface IFirst { }

    [JSAutoEnum(TypeName = ""DocumentReadyState"")]
    public interface ISecond { }
}";

        var result = GetRunResult(source);

        Assert.Empty(result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        var matching = result.GeneratedTrees
            .Where(t => Path.GetFileName(t.FilePath) ==
                "MyApp.Interop.DocumentReadyState.Enum.g.cs")
            .ToArray();

        Assert.Single(matching);
    }

    [Fact]
    public void EnumProjection_MultiSource_FindsAliasInSecondSource()
    {
        // `CustomReady` is defined only in `custom.d.ts`. Listing both
        // sources ensures the executor scans every parser before falling
        // back to BR0006 - a regression where the loop short-circuited
        // on the first miss would falsely report alias-not-found.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(
        TypeName = ""CustomReady"",
        TypeDeclarationSources = new[] { ""other.d.ts"", ""custom.d.ts"" })]
    public interface IReady { }
}";

        var result = GetRunResult(source,
            additionalTexts: new[]
            {
                new InMemoryAdditionalText("other.d.ts", OtherDts),
                new InMemoryAdditionalText("custom.d.ts", CustomDts),
            });

        Assert.Empty(result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.Contains(result.GeneratedTrees, t =>
            Path.GetFileName(t.FilePath) ==
                "MyApp.Interop.CustomReady.Enum.g.cs");
    }

    [Fact]
    public void EnumProjection_GeneratedSourceCompilesCleanly()
    {
        // End-to-end "the emitted enum code actually compiles" guard.
        // We compile JUST the generated enum source against a fresh
        // Net80 reference set rather than the full
        // consumer-source-plus-generator-output round-trip - the latter
        // would also try to compile the synthesized attribute sources
        // (JSAutoInteropAttribute / JSAutoEnumAttribute / etc.), which
        // intentionally omit `using System;` because production
        // consumers enable ImplicitUsings. That's an unrelated
        // limitation of the test harness, not of the enum emitter; this
        // test stays narrowly focused on the artifact this file owns.
        const string source = @"
namespace MyApp.Interop
{
    [JSAutoEnum(TypeName = ""InsertPosition"")]
    public interface IInsert { }
}";

        var result = GetRunResult(source);
        var enumTree = result.GeneratedTrees.First(t =>
            Path.GetFileName(t.FilePath) ==
                "MyApp.Interop.InsertPosition.Enum.g.cs");

        var compilation = GetCompilation(new[] { enumTree });
        var diagnostics = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(diagnostics);
    }
}
