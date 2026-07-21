namespace Blazor.DOM.CSharpGenerator.Output;

public sealed class GenerationLock : IDisposable
{
    private readonly FileStream _stream;

    private GenerationLock(FileStream stream)
    {
        _stream = stream;
    }

    public static GenerationLock Acquire(
        string outputDirectory,
        TimeSpan? timeout = null)
    {
        var fullOutput = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(outputDirectory));
        var parent = Path.GetDirectoryName(fullOutput)
            ?? throw new InvalidOperationException(
                $"Output directory must have a parent: '{fullOutput}'.");
        Directory.CreateDirectory(parent);
        var lockPath = Path.Combine(
            parent,
            $".{Path.GetFileName(fullOutput)}.generation.lock");
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(10));

        while (true)
        {
            try
            {
                return new GenerationLock(new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None));
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }
        }
    }

    public void Dispose() => _stream.Dispose();
}
