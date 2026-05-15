// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Lightweight string-snapshot helper.
///
/// Snapshots live next to the test source file in
/// <c>Snapshots/&lt;scenario&gt;/&lt;file&gt;</c>; missing snapshots are
/// auto-written on first run so a contributor can verify output by hand
/// and then commit it. Set the environment variable
/// <c>UPDATE_SNAPSHOTS=1</c> to rewrite existing snapshots; otherwise an
/// equality assertion is made (with line-ending normalization).
///
/// Snapshots are NOT embedded in the assembly: the helper uses
/// <see cref="CallerFilePathAttribute"/> so we get the on-disk path of
/// the test source at compile time and resolve siblings from there.
/// That keeps the workflow zero-config for source builds (this repo's
/// only mode).
/// </summary>
public static class SnapshotAsserter
{
    public static void AssertMatchesSnapshot(
        string scenario,
        string fileName,
        string actual,
        [CallerFilePath] string callerFile = "")
    {
        var testDir = Path.GetDirectoryName(callerFile)!;
        var snapshotDir = Path.Combine(testDir, "Snapshots", scenario);
        var snapshotPath = Path.Combine(snapshotDir, fileName);
        var shouldUpdate = string.Equals(
            Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"),
            "1",
            StringComparison.Ordinal);

        if (shouldUpdate || !File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(snapshotDir);
            File.WriteAllText(snapshotPath, actual);
            return;
        }

        var expected = File.ReadAllText(snapshotPath);
        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actual));
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n");
}
