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
    DuringOwnedFileDeletion,
    DuringCanonicalDirectoryMove,
    DuringCandidateDirectoryMove,
}

public sealed class OutputPromotionCleanupException(
    string canonicalDirectory,
    string cleanupDirectory,
    Exception innerException)
    : IOException(
        $"Output promotion to '{canonicalDirectory}' committed and was byte-verified, " +
        $"but temporary promotion cleanup failed. The verified canonical output was " +
        $"preserved; recoverable debris may remain at '{cleanupDirectory}'.",
        innerException)
{
    public string CanonicalDirectory { get; } = canonicalDirectory;
    public string CleanupDirectory { get; } = cleanupDirectory;
}

public static class OutputPromotion
{
    private const int MaxFileSystemAttempts = 10;
    private const int MaxDirectoryMoveAttempts = 3;

    private static readonly string[] ExhaustiveOwnedDirectories =
    [
        "AdvancedTypes",
        "Callbacks",
        "Dictionaries",
        "Enums",
        "EventMaps",
        "Factories",
        "Globals",
        "Interfaces",
        "Namespaces",
        "Server",
        "Shared",
        "Typedefs",
        "WebAssembly",
    ];

    private static readonly IReadOnlySet<string> ExhaustiveOwnedFiles =
        new HashSet<string>(
        [
            "emitter-manifest.json",
            "host-parity.json",
        ],
        StringComparer.Ordinal);

    public static void PromoteExhaustive(
        string stagingDirectory,
        string canonicalDirectory,
        Action<OutputPromotionFailurePoint>? failureInjector = null,
        Action<OutputPromotionCleanupException>? cleanupFailureHandler = null,
        Action<TimeSpan>? retryDelay = null)
        => Promote(
            stagingDirectory,
            canonicalDirectory,
            preserveUnownedContent: true,
            failureInjector,
            cleanupFailureHandler,
            retryDelay);

    public static void PromoteProfile(
        string stagingDirectory,
        string canonicalDirectory,
        Action<OutputPromotionFailurePoint>? failureInjector = null,
        Action<OutputPromotionCleanupException>? cleanupFailureHandler = null,
        Action<TimeSpan>? retryDelay = null)
        => Promote(
            stagingDirectory,
            canonicalDirectory,
            preserveUnownedContent: false,
            failureInjector,
            cleanupFailureHandler,
            retryDelay);

    public static bool IsExhaustiveOwnedPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (ExhaustiveOwnedFiles.Contains(normalized))
            return true;

        var separator = normalized.IndexOf('/');
        var firstSegment = separator >= 0 ? normalized[..separator] : normalized;
        return ExhaustiveOwnedDirectories.Contains(firstSegment, StringComparer.Ordinal);
    }

    private static void Promote(
        string stagingDirectory,
        string canonicalDirectory,
        bool preserveUnownedContent,
        Action<OutputPromotionFailurePoint>? failureInjector,
        Action<OutputPromotionCleanupException>? cleanupFailureHandler,
        Action<TimeSpan>? retryDelay)
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
        var backupCopied = false;
        var candidatePromoted = false;
        var canonicalMutationStarted = false;

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
                DeleteExhaustiveOwnedContent(
                    candidate,
                    failureInjector,
                    retryDelay);
                ValidateExhaustiveStaging(staging);
                Inject(failureInjector, OutputPromotionFailurePoint.AfterOwnedContentDeletion);
            }

            Inject(failureInjector, OutputPromotionFailurePoint.BeforeStagingCopy);
            OutputDirectoryUtilities.CopyDirectory(staging, candidate, overwrite: true);
            Inject(failureInjector, OutputPromotionFailurePoint.AfterStagingCopy);
            VerifyPromotion(expectedFiles, candidate);

            var canonicalAlreadyCurrent = canonicalExisted
                && OutputVerifier.Verify(
                    expectedFiles,
                    originalCanonicalFiles).Identical;
            if (!canonicalAlreadyCurrent)
            {
                Inject(failureInjector, OutputPromotionFailurePoint.BeforeCanonicalSwap);
                if (canonicalExisted)
                {
                    try
                    {
                        MoveDirectoryWithRetry(
                            canonical,
                            backup,
                            retryDelay,
                            () => Inject(
                                failureInjector,
                                OutputPromotionFailurePoint.DuringCanonicalDirectoryMove),
                            MaxDirectoryMoveAttempts);
                        backupMoved = true;
                    }
                    catch (Exception moveException) when (
                        IsFileSystemFailure(moveException)
                        && Directory.Exists(canonical)
                        && !Directory.Exists(backup))
                    {
                        CopyDirectoryWithRetry(canonical, backup, retryDelay);
                        VerifyTree(
                            originalCanonicalFiles,
                            backup,
                            $"Copied rollback backup for '{canonical}' is not byte-identical");
                        backupCopied = true;
                    }
                    Inject(
                        failureInjector,
                        OutputPromotionFailurePoint.AfterCanonicalBackupMove);
                }

                if (backupCopied)
                {
                    canonicalMutationStarted = true;
                    SynchronizeDirectory(candidate, canonical, retryDelay);
                }
                else
                {
                    try
                    {
                        MoveDirectoryWithRetry(
                            candidate,
                            canonical,
                            retryDelay,
                            () => Inject(
                                failureInjector,
                                OutputPromotionFailurePoint.DuringCandidateDirectoryMove),
                            MaxDirectoryMoveAttempts);
                    }
                    catch (Exception moveException) when (
                        IsFileSystemFailure(moveException)
                        && Directory.Exists(candidate)
                        && !Directory.Exists(canonical))
                    {
                        canonicalMutationStarted = true;
                        SynchronizeDirectory(candidate, canonical, retryDelay);
                    }
                }
                candidatePromoted = true;
                Inject(failureInjector, OutputPromotionFailurePoint.AfterCandidatePromotion);

                VerifyPromotion(expectedFiles, canonical);
            }
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
                    backupCopied,
                    candidatePromoted,
                    canonicalMutationStarted,
                    originalCanonicalFiles,
                    retryDelay);
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
        var cleanupDirectory = candidate;
        try
        {
            Inject(
                failureInjector,
                OutputPromotionFailurePoint.AfterPostPromotionVerification);
            Inject(failureInjector, OutputPromotionFailurePoint.AfterPromotionCommit);
            if (Directory.Exists(candidate))
                DeleteDirectoryWithRetry(candidate, retryDelay: retryDelay);

            if (backupMoved || backupCopied)
            {
                Inject(
                    failureInjector,
                    OutputPromotionFailurePoint.BeforeBackupDeletion);
                cleanupDirectory = backup;
                DeleteDirectoryWithRetry(
                    backup,
                    () => Inject(
                        failureInjector,
                        OutputPromotionFailurePoint.DuringBackupDeletion),
                    retryDelay);
                backupMoved = false;
                backupCopied = false;
            }
        }
        catch (Exception cleanupException)
        {
            var failure = new OutputPromotionCleanupException(
                canonical,
                cleanupDirectory,
                cleanupException);
            if (cleanupFailureHandler is null)
                throw failure;

            cleanupFailureHandler(failure);
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
        bool backupCopied,
        bool candidatePromoted,
        bool canonicalMutationStarted,
        IReadOnlyList<GeneratedFile> originalCanonicalFiles,
        Action<TimeSpan>? retryDelay)
    {
        if (backupMoved || backupCopied)
        {
            if (!Directory.Exists(backup))
                throw new IOException(
                    $"Rollback backup for '{canonical}' is missing.");

            VerifyTree(
                originalCanonicalFiles,
                backup,
                $"Rollback backup for '{canonical}' is not byte-identical");
        }

        if (canonicalMutationStarted)
        {
            if (canonicalExisted)
            {
                if (!Directory.Exists(backup))
                    throw new IOException(
                        $"Rollback backup for '{canonical}' is missing.");
                SynchronizeDirectory(backup, canonical, retryDelay);
            }
            else if (Directory.Exists(canonical))
            {
                DeleteDirectoryWithRetry(canonical, retryDelay: retryDelay);
            }
        }
        else if (!backupCopied)
        {
            if (candidatePromoted && Directory.Exists(canonical))
                MoveDirectoryWithRetry(canonical, rejected, retryDelay);

            if (backupMoved && Directory.Exists(backup))
                MoveDirectoryWithRetry(backup, canonical, retryDelay);
            else if (canonicalExisted && !Directory.Exists(canonical))
                throw new IOException(
                    $"Rollback backup for '{canonical}' is missing.");
        }

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

    private static void DeleteExhaustiveOwnedContent(
        string directory,
        Action<OutputPromotionFailurePoint>? failureInjector,
        Action<TimeSpan>? retryDelay)
    {
        foreach (var ownedDirectory in ExhaustiveOwnedDirectories)
        {
            var path = Path.Combine(directory, ownedDirectory);
            if (Directory.Exists(path))
                DeleteDirectoryWithRetry(path, retryDelay: retryDelay);
        }

        foreach (var ownedFile in ExhaustiveOwnedFiles)
        {
            var path = Path.Combine(directory, ownedFile);
            if (File.Exists(path))
            {
                DeleteFileWithRetry(
                    path,
                    () => Inject(
                        failureInjector,
                        OutputPromotionFailurePoint.DuringOwnedFileDeletion),
                    retryDelay);
            }
        }
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

    private static void CopyDirectoryWithRetry(
        string source,
        string destination,
        Action<TimeSpan>? retryDelay)
    {
        Directory.CreateDirectory(destination);
        foreach (var sourceFile in Directory
            .EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(source, sourceFile);
            var destinationFile = Path.Combine(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            CopyFileWithRetry(
                sourceFile,
                destinationFile,
                overwrite: true,
                retryDelay);
        }
    }

    private static void SynchronizeDirectory(
        string source,
        string destination,
        Action<TimeSpan>? retryDelay)
    {
        Directory.CreateDirectory(destination);
        var sourceFiles = OutputVerifier.ScanDirectory(source)
            .ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var destinationFiles = OutputVerifier.ScanDirectory(destination)
            .ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        foreach (var staleFile in destinationFiles.Keys
            .Except(sourceFiles.Keys, StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            DeleteFileWithRetry(
                Path.Combine(destination, staleFile),
                beforeDelete: null,
                retryDelay);
        }

        foreach (var sourceFile in sourceFiles.Values
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            if (destinationFiles.TryGetValue(
                    sourceFile.RelativePath,
                    out var destinationFile)
                && string.Equals(
                    sourceFile.Sha256,
                    destinationFile.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyFileAtomicallyWithRetry(
                Path.Combine(source, sourceFile.RelativePath),
                Path.Combine(destination, sourceFile.RelativePath),
                retryDelay);
        }
    }

    private static void CopyFileAtomicallyWithRetry(
        string source,
        string destination,
        Action<TimeSpan>? retryDelay)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = $"{destination}.promotion-{Guid.NewGuid():N}.tmp";
        CopyFileWithRetry(source, temporary, overwrite: true, retryDelay);
        try
        {
            MoveFileWithRetry(temporary, destination, retryDelay);
        }
        catch
        {
            if (File.Exists(temporary))
                DeleteFileWithRetry(temporary, beforeDelete: null, retryDelay);
            throw;
        }
    }

    private static void CopyFileWithRetry(
        string source,
        string destination,
        bool overwrite,
        Action<TimeSpan>? retryDelay)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite);
                return;
            }
            catch (IOException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
        }
    }

    internal static void DeleteDirectoryWithRetry(
        string directory,
        Action? afterEntryDeleted = null,
        Action<TimeSpan>? retryDelay = null)
    {
        if (!Directory.Exists(directory))
            return;

        for (var attempt = 1; attempt <= MaxFileSystemAttempts; attempt++)
        {
            try
            {
                DeleteDirectoryTree(directory, afterEntryDeleted);
                return;
            }
            catch (IOException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
        }
    }

    private static void DeleteFileWithRetry(
        string path,
        Action? beforeDelete,
        Action<TimeSpan>? retryDelay)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                beforeDelete?.Invoke();
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
        }
    }

    private static void MoveDirectoryWithRetry(
        string source,
        string destination,
        Action<TimeSpan>? retryDelay = null,
        Action? beforeMove = null,
        int maxAttempts = MaxFileSystemAttempts)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                beforeMove?.Invoke();
                Directory.Move(source, destination);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
        }
    }

    private static void MoveFileWithRetry(
        string source,
        string destination,
        Action<TimeSpan>? retryDelay)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(destination))
                {
                    var attributes = File.GetAttributes(destination);
                    ClearReadOnly(destination, attributes);
                }
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxFileSystemAttempts)
            {
                WaitBeforeRetry(attempt, retryDelay);
            }
        }
    }

    private static bool IsFileSystemFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException;

    private static void WaitBeforeRetry(
        int attempt,
        Action<TimeSpan>? retryDelay)
    {
        var multiplier = 1 << Math.Min(attempt - 1, 4);
        var delay = TimeSpan.FromMilliseconds(Math.Min(100 * multiplier, 1_000));
        if (retryDelay is null)
            Thread.Sleep(delay);
        else
            retryDelay(delay);
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
