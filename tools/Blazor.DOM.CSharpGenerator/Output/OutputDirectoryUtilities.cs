namespace Blazor.DOM.CSharpGenerator.Output;

public static class OutputDirectoryUtilities
{
    public static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var dest = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite);
        }
    }

    public static void DeleteDirectoryContents(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            File.Delete(file);

        foreach (var subdirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            try
            {
                Directory.Delete(subdirectory);
            }
            catch
            {
                // Best-effort cleanup; parents may remain until children are removed.
            }
        }
    }
}
