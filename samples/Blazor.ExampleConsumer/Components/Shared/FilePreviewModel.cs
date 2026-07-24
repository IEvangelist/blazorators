namespace Blazor.ExampleConsumer.Components.Shared;

public enum FilePreviewKind
{
    Image,
    Pdf,
    Code,
    Text,
    Unavailable
}

public sealed record FilePreviewModel(
    string FileName,
    string MimeType,
    string Size,
    string Modified,
    FilePreviewKind Kind,
    string? Content = null,
    string? Language = null,
    string? Message = null)
{
    public string KindToken => Kind.ToString().ToLowerInvariant();
}
