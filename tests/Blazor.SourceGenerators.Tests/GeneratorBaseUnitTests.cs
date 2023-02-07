// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
///  Borrowed from: https://github.com/jaredpar/channel9-source-generators/blob/main/GeneratorsUnitTests/GeneratorBaseUnitTests.cs
/// </summary>
public abstract class GeneratorBaseUnitTests
{
    public abstract IEnumerable<ISourceGenerator> SourceGenerators { get; }

    public static Compilation GetCompilation(string sourceCode) =>
        GetCompilation(new[] { CSharpSyntaxTree.ParseText(sourceCode) });

    public static Compilation GetCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            options: options)
            .WithReferenceAssemblies(ReferenceAssemblyKind.Net60);

        return compilation;
    }

    public GeneratorDriverRunResult GetRunResult(string sourceCode)
    {
        var compilation = GetCompilation(sourceCode);
        var driver = CSharpGeneratorDriver.Create(SourceGenerators);
        return driver
            .RunGenerators(compilation)
            .GetRunResult();
    }

    public void VerifyCompiles(string sourceCode)
    {
        var all = new SyntaxTree[] { CSharpSyntaxTree.ParseText(sourceCode) };
        var result = GetRunResult(sourceCode);
        var compilation = GetCompilation(all.Concat(result.GeneratedTrees));
        var diagnostics = compilation
            .GetDiagnostics()
            .Where(x => x.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        Assert.Empty(diagnostics);
    }

    public static void VerifyGeneratedCode(string expectedCode, SyntaxTree actualTree)
    {
        var actualCode = Trim(actualTree.ToString());

        Assert.Equal(Trim(expectedCode), actualCode);

        static string Trim(string s)
        {
            return s.Trim(' ', '\n', '\r');
        }
    }
}
