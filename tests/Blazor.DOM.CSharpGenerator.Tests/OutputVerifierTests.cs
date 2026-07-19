// OutputWriter and OutputVerifier tests: byte identity across two runs.

using Blazor.DOM.CSharpGenerator.Output;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class OutputWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public OutputWriterTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Write_CreatesFile_WithDeterministicName()
    {
        var writer = new OutputWriter(_tempDir);
        var path = writer.Write("AlignSetting", "// content\npublic enum AlignSetting { }");

        Assert.Equal("AlignSetting.g.cs", Path.GetFileName(path));
        Assert.True(File.Exists(Path.Combine(_tempDir, path)));
    }

    [Fact]
    public void Write_NormalizesLineEndingsToLF()
    {
        var writer = new OutputWriter(_tempDir);
        var content = "line1\r\nline2\r\nline3";
        writer.Write("Test", content);

        var bytes = File.ReadAllBytes(Path.Combine(_tempDir, "Test.g.cs"));
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public void Write_SameContentTwice_ProducesIdenticalHashes()
    {
        var content = "// identical\npublic enum X { A, B }";

        var dir1 = Path.Combine(_tempDir, "run1");
        var dir2 = Path.Combine(_tempDir, "run2");
        var w1 = new OutputWriter(dir1);
        var w2 = new OutputWriter(dir2);

        w1.Write("MyEnum", content);
        w2.Write("MyEnum", content);

        Assert.Equal(w1.WrittenFiles[0].Sha256, w2.WrittenFiles[0].Sha256);
    }

    [Fact]
    public void OutputVerifier_Verify_ReturnsTrueForIdenticalRuns()
    {
        var content = "// identical\npublic enum Y { A }";
        var dir1 = Path.Combine(_tempDir, "v1");
        var dir2 = Path.Combine(_tempDir, "v2");
        var w1 = new OutputWriter(dir1);
        var w2 = new OutputWriter(dir2);
        w1.Write("Y", content);
        w2.Write("Y", content);

        var result = OutputVerifier.Verify(w1.WrittenFiles, w2.WrittenFiles);
        Assert.True(result.Identical);
        Assert.Empty(result.Mismatches);
    }

    [Fact]
    public void OutputVerifier_Verify_ReturnsFalseForDifferentContent()
    {
        var dir1 = Path.Combine(_tempDir, "d1");
        var dir2 = Path.Combine(_tempDir, "d2");
        var w1 = new OutputWriter(dir1);
        var w2 = new OutputWriter(dir2);
        w1.Write("Z", "// version 1");
        w2.Write("Z", "// version 2");

        var result = OutputVerifier.Verify(w1.WrittenFiles, w2.WrittenFiles);
        Assert.False(result.Identical);
        Assert.Single(result.Mismatches);
    }
}
