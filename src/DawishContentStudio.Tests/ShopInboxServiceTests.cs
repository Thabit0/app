using DawishContentStudio.Core;
using Xunit;

namespace DawishContentStudio.Tests;

public sealed class ShopInboxServiceTests
{
    [Fact]
    public async Task DownloadsEachPostOnlyOnceAndKeepsMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "dawish-shop-inbox-" + Guid.NewGuid().ToString("N"));
        var inbox = new ShopInboxService(root);
        var calls = 0;
        var post = new CloudflarePost
        {
            Id = "post-1",
            MediaKey = "posts/post-1/original.png",
            Caption = "test",
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };

        try
        {
            async Task Download(string path)
            {
                calls++;
                await File.WriteAllBytesAsync(path, [1, 2, 3]);
            }

            var first = await inbox.DownloadAsync(post, Download);
            var second = await inbox.DownloadAsync(post, Download);

            Assert.Equal(first, second);
            Assert.True(File.Exists(first));
            Assert.True(inbox.Contains(post.Id));
            Assert.Equal(1, inbox.CountReady());
            Assert.Equal(1, calls);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
