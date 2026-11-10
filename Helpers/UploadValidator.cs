namespace LDHub.Helpers;

/// <summary>
/// Shared checks for user uploads: extension whitelist, size cap, safe file names.
/// </summary>
public static class UploadValidator
{
    public static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".webp" };

    public static readonly string[] ModuleExtensions =
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".csv",
          ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm", ".mp3",
          ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

    public const long ImageMaxBytes  = 5L * 1024 * 1024;   // 5 MB
    public const long ModuleMaxBytes = 25L * 1024 * 1024;  // 25 MB (Kestrel's default request limit is ~30 MB)

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(IFormFile file, string[] allowedExtensions, long maxBytes)
    {
        if (file.Length > maxBytes)
            return $"File must not exceed {maxBytes / (1024 * 1024)} MB.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            return $"File type '{ext}' is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}.";

        return null;
    }

    /// <summary>Strips any path and unsafe characters from a client-supplied file name.</summary>
    public static string SafeFileName(string original)
    {
        var name = Path.GetFileName(original);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Replace(' ', '_');
        return name.Length > 100 ? name[^100..] : name;
    }
}
