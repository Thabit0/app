using DawishContentStudio.Core;
using System.Collections.ObjectModel;
using Xunit;

namespace DawishContentStudio.Tests;

public sealed class ImageWorkspaceServiceTests
{
    [Fact]
    public void AddUniqueAcceptsImagesAndPreventsDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var image = Path.Combine(root, "product.JPG");
        var ignored = Path.Combine(root, "notes.txt");
        File.WriteAllBytes(image, [1]);
        File.WriteAllText(ignored, "ignore");
        var images = new ObservableCollection<LocalImageItem>();

        try
        {
            Assert.Equal(1, ImageWorkspaceService.AddUnique(images, [image, image, ignored]));
            Assert.Single(images);
            Assert.Equal(0, ImageWorkspaceService.AddUnique(images, [image]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RemoveSelectedKeepsOriginalFilesAndUnselectedItems()
    {
        var images = new ObservableCollection<LocalImageItem>
        {
            new() { Path = "one.jpg", FileName = "one.jpg", IsSelected = true },
            new() { Path = "two.jpg", FileName = "two.jpg", IsSelected = false }
        };

        Assert.Equal(1, ImageWorkspaceService.RemoveSelected(images));
        Assert.Single(images);
        Assert.Equal("two.jpg", images[0].FileName);
    }
}
