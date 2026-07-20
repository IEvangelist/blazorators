using Blazor.DOM.CSharpGenerator.Accounting;

namespace Blazor.DOM.CSharpGenerator;

internal enum SymbolEmissionDisposition
{
    None,
    Projected,
    Deferred,
    Excluded,
    Failed,
}

internal sealed record PrimarySymbolEmission(
    SymbolEmissionDisposition Disposition,
    string? GeneratedFile = null,
    string? Phase = null,
    string? Reason = null,
    string? ExceptionType = null,
    IReadOnlyList<MemberOutcome>? MemberOutcomes = null,
    IReadOnlyList<DeclarationOutcome>? DeclarationOutcomes = null,
    IReadOnlyList<OverloadOutcome>? OverloadOutcomes = null)
{
    internal static PrimarySymbolEmission None { get; } =
        new(SymbolEmissionDisposition.None);
}

internal sealed record SupplementalSymbolEmission(
    IReadOnlyList<string> GeneratedFiles,
    string? FailureReason,
    string? ExceptionType,
    IReadOnlyList<MemberOutcome> MemberOutcomes,
    IReadOnlyList<DeclarationOutcome> DeclarationOutcomes,
    IReadOnlyList<OverloadOutcome> OverloadOutcomes)
{
    internal static SupplementalSymbolEmission None { get; } =
        new([], null, null, [], [], []);
}

internal sealed record SupplementalGenerationResult(
    IReadOnlyDictionary<string, SupplementalSymbolEmission> Symbols);
