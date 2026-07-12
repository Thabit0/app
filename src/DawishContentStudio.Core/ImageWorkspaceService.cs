using System.Collections.ObjectModel;

namespace DawishContentStudio.Core;

public static class ImageWorkspaceService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static int AddUnique(ObservableCollection<LocalImageItem> images, IEnumerable<string> paths)
    {
        var existing = images
            .Select(x => Path.GetFullPath(x.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var path in paths
                     .Where(File.Exists)
                     .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!existing.Add(path)) continue;
            images.Add(new LocalImageItem { Path = path, FileName = Path.GetFileName(path) });
            added++;
        }

        return added;
    }

    public static int RemoveSelected(ObservableCollection<LocalImageItem> images)
    {
        var selected = images.Where(x => x.IsSelected).ToArray();
        foreach (var image in selected) images.Remove(image);
        return selected.Length;
    }
}
