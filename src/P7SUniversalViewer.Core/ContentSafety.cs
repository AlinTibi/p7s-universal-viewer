using System.Text;

namespace P7SUniversalViewer.Core;

public enum PreviewKind { Text, Pdf, Image, Unsupported }
public sealed record ContentType(string Label, string Extension, PreviewKind Preview);
public static class ContentSafety
{
    public static ContentType Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith("%PDF-"u8)) return new("PDF document", ".pdf", PreviewKind.Pdf);
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return new("PNG image", ".png", PreviewKind.Image);
        if (bytes.StartsWith(new byte[] { 255, 216, 255 })) return new("JPEG image", ".jpg", PreviewKind.Image);
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return new("GIF image", ".gif", PreviewKind.Image);
        if (bytes.StartsWith("PK\u0003\u0004"u8)) return new("ZIP / Office container (not decompressed)", ".zip", PreviewKind.Unsupported);
        try {
            var text = new UTF8Encoding(false, true).GetString(bytes[..Math.Min(bytes.Length, 65536)]);
            if (!text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t' and not '\uFEFF')) return new("UTF-8 text", ".txt", PreviewKind.Text);
        } catch (DecoderFallbackException) { }
        return new("Binary content", ".bin", PreviewKind.Unsupported);
    }
    public static string SafeFileName(string name, string extension)
    {
        name = name.Replace('\\', '/').Split('/').Last();
        var stem = Path.GetFileNameWithoutExtension(name);
        stem = new string(stem.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        if (stem.Length > 100) stem = stem[..100];
        if (string.IsNullOrWhiteSpace(stem) || stem is "." or "..") stem = "content";
        var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        if (reserved.Contains(stem.Split('.')[0], StringComparer.OrdinalIgnoreCase)) stem = "_" + stem;
        if (!new[] { ".pdf", ".png", ".jpg", ".gif", ".txt", ".zip", ".bin" }.Contains(extension)) extension = ".bin";
        return stem + extension;
    }
    public static string ContainedPath(string directory, string fileName)
    {
        if (fileName != Path.GetFileName(fileName) || fileName.Contains('\\') || fileName.Contains('/') || fileName.Contains(':') || fileName is "." or "..") throw new InvalidDataException("Choose a filename within the output folder.");
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(root, fileName));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Output is outside the selected folder.");
        // Reject redirected folders rather than following junctions/symlinks to unexpected destinations.
        for (DirectoryInfo? parent = new(directory); parent is not null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Choose an output folder without directory links.");
        return target;
    }
    public static async Task SaveNewAsync(string directory, string fileName, byte[] content)
    {
        var path = ContainedPath(directory, fileName);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await output.WriteAsync(content);
        await output.FlushAsync();
    }
}

public sealed class PreviewFiles : IDisposable
{
    private const string MarkerName = ".p7s-preview";
    private readonly string root;
    private readonly string session;
    private readonly FileStream lease;
    public bool Keep { get; set; }
    public string SessionDirectory => session;
    public PreviewFiles(string? directory = null)
    {
        root = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALMARFELD", "P7SUniversalViewer", "Previews");
        Directory.CreateDirectory(root);
        CleanupStale(root);
        session = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(session);
        File.WriteAllText(Path.Combine(session, MarkerName), "P7SUniversalViewer-preview-v2");
        lease = new FileStream(Path.Combine(session, ".lease"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }
    public string Write(byte[] bytes, string extension)
    {
        var filename = ContentSafety.SafeFileName(Guid.NewGuid().ToString("N"), extension);
        var path = ContentSafety.ContainedPath(session, filename);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); return path;
    }
    public void Clear()
    {
        if (Keep) return;
        foreach (var file in Directory.EnumerateFiles(session).Where(f => Path.GetFileName(f) is not MarkerName and not ".lease")) TryDelete(file);
    }
    public static void CleanupStale(string root)
    {
        if (!Directory.Exists(root) || IsLink(root)) return;
        foreach (var folder in Directory.EnumerateDirectories(root)) {
            if (IsLink(folder) || !Guid.TryParseExact(Path.GetFileName(folder), "N", out _) || File.Exists(Path.Combine(folder, ".keep"))) continue;
            var marker = Path.Combine(folder, MarkerName);
            if (!File.Exists(marker) || IsLink(marker) || new FileInfo(marker).Length > 128 || File.ReadAllText(marker) != "P7SUniversalViewer-preview-v2") continue;
            if (Directory.GetLastWriteTimeUtc(folder) > DateTime.UtcNow.AddDays(-1)) continue;
            try { using (new FileStream(Path.Combine(folder, ".lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
                foreach (var file in Directory.EnumerateFiles(folder)) if (!IsLink(file)) TryDelete(file);
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    public void Dispose()
    {
        if (Keep) File.WriteAllText(Path.Combine(session, ".keep"), "Retained by user choice");
        Clear(); lease.Dispose();
        if (!Keep) { TryDelete(Path.Combine(session, ".lease")); TryDelete(Path.Combine(session, MarkerName)); if (!Directory.EnumerateFileSystemEntries(session).Any()) Directory.Delete(session); }
    }
}
