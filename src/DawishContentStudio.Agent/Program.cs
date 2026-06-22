using DawishContentStudio.Core;

var syncFolder = GetArg("--sync") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DawishSync");
var loop = args.Any(a => a.Equals("--loop", StringComparison.OrdinalIgnoreCase));
var intervalSeconds = int.TryParse(GetArg("--interval"), out var n) ? Math.Max(10, n) : 30;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("Dawish Publisher Agent");
Console.WriteLine("بدون API — يقرأ مجلد المزامنة وينفذ المنشورات المستحقة.");
Console.WriteLine($"Sync: {syncFolder}");

var service = new SyncFolderService(syncFolder);
var agent = new PublisherAgent(service);

if (!loop)
{
    var count = await agent.ProcessDueJobsAsync();
    Console.WriteLine($"تمت معالجة {count} منشور.");
    return;
}

while (!Console.KeyAvailable)
{
    try
    {
        var count = await agent.ProcessDueJobsAsync();
        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} — processed: {count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine("خطأ: " + ex.Message);
        await service.WriteHeartbeatAsync("publisher-agent", "خطأ: " + ex.Message);
    }
    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
}

string? GetArg(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
    }
    return null;
}
