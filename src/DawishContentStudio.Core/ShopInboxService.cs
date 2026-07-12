using System.Text.Json;

namespace DawishContentStudio.Core;

public sealed class ShopInboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ShopInboxService(string? rootFolder = null)
    {
        RootFolder = rootFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DawishContentStudio", "ShopInbox");
        Directory.CreateDirectory(RootFolder);
    }

    public string RootFolder { get; }

    public bool Contains(string postId) => File.Exists(MetadataPath(postId)) && FindImage(postId) is not null;

    public string? FindImage(string postId)
    {
        var folder = PostFolder(postId);
        if (!Directory.Exists(folder)) return null;
        return Directory.EnumerateFiles(folder, "original.*").FirstOrDefault();
    }

    public async Task<string> DownloadAsync(
        CloudflarePost post,
        Func<string, Task> downloadToPath,
        CancellationToken cancellationToken = default)
    {
        if (Contains(post.Id)) return FindImage(post.Id)!;

        var folder = PostFolder(post.Id);
        Directory.CreateDirectory(folder);
        var extension = SafeExtension(post.MediaKey);
        var finalPath = Path.Combine(folder, "original" + extension);
        var temporaryPath = finalPath + ".downloading";

        try
        {
            await downloadToPath(temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath, true);
            await File.WriteAllTextAsync(
                MetadataPath(post.Id),
                JsonSerializer.Serialize(post, JsonOptions),
                cancellationToken);
            return finalPath;
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public int CountReady() => Directory.Exists(RootFolder)
        ? Directory.EnumerateFiles(RootFolder, "post.json", SearchOption.AllDirectories).Count()
        : 0;

    private string PostFolder(string postId) => Path.Combine(RootFolder, SafeId(postId));
    private string MetadataPath(string postId) => Path.Combine(PostFolder(postId), "post.json");

    private static string SafeId(string value) => string.Concat(value.Select(c =>
        char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

    private static string SafeExtension(string mediaKey) => Path.GetExtension(mediaKey).ToLowerInvariant() switch
    {
        ".png" => ".png",
        ".webp" => ".webp",
        ".jpeg" => ".jpeg",
        _ => ".jpg"
    };
}
