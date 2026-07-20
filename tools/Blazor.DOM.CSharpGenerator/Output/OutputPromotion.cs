namespace Blazor.DOM.CSharpGenerator.Output;

public enum OutputPromotionFailurePoint
{
    BeforePreservedTreeCopy,
    AfterPreservedTreeCopy,
    BeforeOwnedContentDeletion,
    AfterOwnedContentDeletion,
    BeforeStagingCopy,
    AfterStagingCopy,
    BeforeCanonicalSwap,
    AfterCanonicalBackupMove,
    AfterCandidatePromotion,
    AfterPostPromotionVerification,
    BeforeBackupDeletion,
    AfterPromotionCommit,
    DuringBackupDeletion,
}

public sealed class OutputPromotionCleanupException(
    string canonicalDirectory,
    string backupDirectory,
    Exception innerException)
    : IOException(
        $"Output promotion to '{canonicalDirectory}' committed and was byte-verified, " +
        $"but backup cleanup failed. The verified canonical output was preserved; " +
        $"recoverable backup debris may remain at '{backupDirectory}'.",
        innerException)
{
    public string CanonicalDirectory { get; } = canonicalDirectory;
    public string BackupDirectory { get; } = backupDirectory;
}

public static class OutputPromotion
{
    private static readonly string[] ExhaustiveOwnedDirectories =
    [
        "Callbacks",
        "Dictionaries",
        "Enums",
        "Interfaces",
        "Typedefs",
    ];

    private const string ExhaustiveManifest = "emitter-manifest.json";

    public static void PromoteExhaustive(
        string stagingDirectory,
        string canonicalDirectory,
        Action<OutputPromotionFailurePoint>? failureInjector = null)
        => Promote(
            stagingDirectory,
            canonicalDirectory,
            preserveUnownedContent: true,
            failureInjector);

    public static void PromoteProfile(
        string stagingDirectory,
        string canonicalDirectory,
        Action<OutputPromotionFailurePoint>? failureInjector = null)
        => Promote(
            stagingDirectory,
            canonicalDirectory,
            preserveUnownedContent: false,
            failureInjector);

    public static bool IsExhaustiveOwnedPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (string.Equals(normalized, ExhaustiveManifest, StringComparison.Ordinal))
            return true;

        var separator = normalized.IndexOf('/');
        var firstSegment = separator >= 0 ? normalized[..separator] : normalized;
        return ExhaustiveOwnedDirectories.Contains(firstSegment, StringComparer.Ordinal);
    }

    private static void Promote(
        string stagingDirectory,
        string canonicalDirectory,
        bool preserveUnownedContent,
        Action<OutputPromotionFailurePoint>? failureInjector)
    {
        var staging = Path.GetFullPath(stagingDirectory);
        var canonical = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(canonicalDirectory));

        if (!Directory.Exists(staging))
            throw new DirectoryNotFoundException(
                $"Promotion staging directory does not exist: '{staging}'.");

        var parent = Path.GetDirectoryName(canonical)
            ?? throw new InvalidOperationException(
                $"Canonical output must have a parent directory: '{canonical}'.");
        var canonicalName = Path.GetFileName(canonical);
        if (string.IsNullOrWhiteSpace(canonicalName))
            throw new InvalidOperationException(
                $"Canonical output cannot be a volume root: '{canonical}'.");

        Directory.CreateDirectory(parent);

        var token = Guid.NewGuid().ToString("N");
        var candidate = Path.Combine(parent, $".{canonicalName}.candidate-{token}");
        var backup = Path.Combine(parent, $".{canonicalName}.backup-{token}");
        var rejected = Path.Combine(parent, $".{canonicalName}.rejected-{token}");
        var canonicalExisted = Directory.Exists(canonical);
        var originalCanonicalFiles = canonicalExisted
            ? OutputVerifier.ScanDirectory(canonical)
            : [];
        var stagingFiles = OutputVerifier.ScanDirectory(staging);
        var expectedFiles = BuildExpectedFiles(
            originalCanonicalFiles,
            stagingFiles,
            preserveUnownedContent);
        var backupMoved = false;
        var candidatePromoted = false;

        try
        {
            Directory.CreateDirectory(candidate);

            if (preserveUnownedContent && canonicalExisted)
            {
                Inject(failureInjector, OutputPromotionFailurePoint.BeforePreservedTreeCopy);
                OutputDirectoryUtilities.CopyDirectory(canonical, candidate, overwrite: true);
                Inject(failureInjector, OutputPromotionFailurePoint.AfterPreservedTreeCopy);
            }

            if (preserveUnownedContent)
            {
                Inject(failureInjector, OutputPromotionFailurePoint.BeforeOwnedContentDeletion);
                DeleteExhaustiveOwnedContent(candidate);
                ValidateExhaustiveStaging(staging);
                Inject(failureInjector, OutputPromotionFailurePoint.AfterOwnedContentDeletion);
            }

            Inject(failureInjector, OutputPromotionFailurePoint.BeforeStagingCopy);
            OutputDirectoryUtilities.CopyDirectory(staging, candidate, overwrite: true);
            Inject(failureInjector, OutputPromotionFailurePoint.AfterStagingCopy);
            VerifyPromotion(expectedFiles, candidate);

            Inject(failureInjector, OutputPromotionFailurePoint.BeforeCanonicalSwap);
            if (canonicalExisted)
            {
                Directory.Move(canonical, backup);
                backupMoved = true;
                Inject(failureInjector, OutputPromotionFailurePoint.AfterCanonicalBackupMove);
            }

            Directory.Move(candidate, canonical);
            candidatePromoted = true;
            Inject(failureInjector, OutputPromotionFailurePoint.AfterCandidatePromotion);

            VerifyPromotion(expectedFiles, canonical);
        }
        catch (Exception promotionException)
        {
            try
            {
                RollBack(
                    canonical,
                    candidate,
                    backup,
                    rejected,
                    canonicalExisted,
                    backupMoved,
                    candidatePromoted,
                    originalCanonicalFiles);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    $"Output promotion failed and rollback could not restore '{canonical}'.",
                    promotionException,
                    rollbackException);
            }

            throw;
        }

        // The transaction commits immediately after the canonical tree is byte-verified.
        // Backup deletion is post-commit cleanup and must never trigger rollback.
        try
        {
            Inject(
                failureInjector,
                OutputPromotionFailurePoint.AfterPostPromotionVerification);
            Inject(failureInjector, OutputPromotionFailurePoint.AfterPromotionCommit);
            if (backupMoved)
            {
                Inject(
                    failureInjector,
                    OutputPromotionFailurePoint.BeforeBackupDeletion);
                DeleteDirectoryWithRetry(
                    backup,
                    () => Inject(
                        failureInjector,
                        OutputPromotionFailurePoint.DuringBackupDeletion));
                backupMoved = false;
            }
        }
        catch (Exception cleanupException)
        {
            throw new OutputPromotionCleanupException(
                canonical,
                backup,
                cleanupException);
        }
    }

    private static IReadOnlyList<GeneratedFile> BuildExpectedFiles(
        IReadOnlyList<GeneratedFile> originalCanonicalFiles,
        IReadOnlyList<GeneratedFile> stagingFiles,
        bool preserveUnownedContent)
    {
        var expected = preserveUnownedContent
            ? originalCanonicalFiles
                .Where(file => !IsExhaustiveOwnedPath(file.RelativePath))
                .Concat(stagingFiles)
                .ToList()
            : stagingFiles.ToList();

        var duplicate = expected
            .GroupBy(file => file.RelativePath, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Promotion has conflicting ownership for '{duplicate.Key}'.");
        }

        return expected;
    }

    private static void RollBack(
        string canonical,
        string candidate,
        string backup,
        string rejected,
        bool canonicalExisted,
        bool backupMoved,
        bool candidatePromoted,
        IReadOnlyList<GeneratedFile> originalCanonicalFiles)
    {
        if (backupMoved)
        {
            if (!Directory.Exists(backup))
                throw new IOException(
                    $"Rollback backup for '{canonical}' is missing.");

            VerifyTree(
                originalCanonicalFiles,
                backup,
                $"Rollback backup for '{canonical}' is not byte-identical");
        }

        if (candidatePromoted && Directory.Exists(canonical))
            Directory.Move(canonical, rejected);

        if (backupMoved && Directory.Exists(backup))
            Directory.Move(backup, canonical);
        else if (canonicalExisted && !Directory.Exists(canonical))
            throw new IOException(
                $"Rollback backup for '{canonical}' is missing.");

        VerifyRollback(originalCanonicalFiles, canonical, canonicalExisted);

        // The rejected tree remains recoverable until the restored backup has
        // passed byte verification above.
        if (Directory.Exists(candidate))
            DeleteDirectoryWithRetry(candidate);
        if (Directory.Exists(rejected))
            DeleteDirectoryWithRetry(rejected);
        if (Directory.Exists(backup))
            DeleteDirectoryWithRetry(backup);
    }

    private static void ValidateExhaustiveStaging(string stagingDirectory)
    {
        var unownedPaths = OutputVerifier.ScanDirectory(stagingDirectory)
            .Select(file => file.RelativePath)
            .Where(path => !IsExhaustiveOwnedPath(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (unownedPaths.Count > 0)
        {
            throw new InvalidOperationException(
                "Exhaustive staging contains paths outside generated ownership boundaries: " +
                string.Join(", ", unownedPaths));
        }
    }

    private static void DeleteExhaustiveOwnedContent(string directory)
    {
        foreach (var ownedDirectory in ExhaustiveOwnedDirectories)
        {
            var path = Path.Combine(directory, ownedDirectory);
            if (Directory.Exists(path))
                DeleteDirectoryWithRetry(path);
        }

        var manifest = Path.Combine(directory, ExhaustiveManifest);
        if (File.Exists(manifest))
            File.Delete(manifest);
    }

    private static void VerifyPromotion(
        IReadOnlyList<GeneratedFile> expectedFiles,
        string promotedDirectory)
    {
        var promotedFiles = OutputVerifier.ScanDirectory(promotedDirectory);
        var verification = OutputVerifier.Verify(expectedFiles, promotedFiles);
        if (verification.Identical)
            return;

        throw new IOException(
            $"Promoted output verification failed for '{promotedDirectory}': " +
            $"{verification.Mismatches.Count} mismatches, " +
            $"{verification.OnlyInRun1.Count} missing paths, " +
            $"{verification.OnlyInRun2.Count} stale paths.");
    }

    private static void VerifyRollback(
        IReadOnlyList<GeneratedFile> originalCanonicalFiles,
        string canonicalDirectory,
        bool canonicalExisted)
    {
        if (!canonicalExisted)
        {
            if (Directory.Exists(canonicalDirectory))
                throw new IOException(
                    $"Rollback left a canonical directory that did not previously exist: '{canonicalDirectory}'.");
            return;
        }

        if (!Directory.Exists(canonicalDirectory))
            throw new IOException(
                $"Rollback did not restore canonical output: '{canonicalDirectory}'.");

        var verification = OutputVerifier.Verify(
            originalCanonicalFiles,
            OutputVerifier.ScanDirectory(canonicalDirectory));
        if (!verification.Identical)
            throw new IOException(
                $"Rollback did not restore byte-identical canonical output: '{canonicalDirectory}'.");
    }

    private static void VerifyTree(
        IReadOnlyList<GeneratedFile> expectedFiles,
        string directory,
        string failureMessage)
    {
        var verification = OutputVerifier.Verify(
            expectedFiles,
            OutputVerifier.ScanDirectory(directory));
        if (!verification.Identical)
            throw new IOException($"{failureMessage}: '{directory}'.");
    }

    internal static void DeleteDirectoryWithRetry(
        string directory,
        Action? afterEntryDeleted = null)
    {
        if (!Directory.Exists(directory))
            return;

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                DeleteDirectoryTree(directory, afterEntryDeleted);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(20 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(20 * attempt);
            }
        }
    }

    private static void DeleteDirectoryTree(
        string directory,
        Action? afterEntryDeleted)
    {
        if (!Directory.Exists(directory))
            return;

        var directoryAttributes = File.GetAttributes(directory);
        if ((directoryAttributes & FileAttributes.ReparsePoint) != 0)
        {
            ClearReadOnly(directory, directoryAttributes);
            Directory.Delete(directory, recursive: false);
            afterEntryDeleted?.Invoke();
            return;
        }

        foreach (var entry in Directory
            .EnumerateFileSystemEntries(directory)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                DeleteDirectoryTree(entry, afterEntryDeleted);
            }
            else
            {
                ClearReadOnly(entry, attributes);
                File.Delete(entry);
                afterEntryDeleted?.Invoke();
            }
        }

        ClearReadOnly(directory, directoryAttributes);
        Directory.Delete(directory, recursive: false);
        afterEntryDeleted?.Invoke();
    }

    private static void ClearReadOnly(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private static void Inject(
        Action<OutputPromotionFailurePoint>? failureInjector,
        OutputPromotionFailurePoint point)
        => failureInjector?.Invoke(point);
}
