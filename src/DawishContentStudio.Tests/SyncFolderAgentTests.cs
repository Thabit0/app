using DawishContentStudio.Core;
using Xunit;

namespace DawishContentStudio.Tests;

public class SyncFolderAgentTests
{
    [Fact]
    public async Task SyncFolderCreatesRequiredAgentFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "dawish-sync-test-" + Guid.NewGuid().ToString("N"));
        var sync = new SyncFolderService(root);

        Assert.True(Directory.Exists(Path.Combine(root, "jobs")));
        Assert.True(Directory.Exists(Path.Combine(root, "status")));
        Assert.True(Directory.Exists(Path.Combine(root, "heartbeat")));
        Assert.True(Directory.Exists(Path.Combine(root, "plans")));
        Assert.True(Directory.Exists(Path.Combine(root, "settings")));

        var settings = await sync.ReadAgentSettingsAsync();
        Assert.Equal(root, settings.SyncFolder);
    }

    [Fact]
    public async Task ReadinessWritesStatusFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "dawish-readiness-test-" + Guid.NewGuid().ToString("N"));
        var sync = new SyncFolderService(root);
        var readiness = await sync.CheckDeviceReadinessAsync();

        Assert.Equal(root, readiness.SyncFolder);
        Assert.True(File.Exists(Path.Combine(root, "status", Environment.MachineName + ".readiness.json")));
    }
}
