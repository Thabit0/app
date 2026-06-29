using DawishContentStudio.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var syncFolder = GetArg("--sync") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DawishSync");
var loop = HasArg("--loop");
var readinessOnly = HasArg("--readiness");
var initSettings = HasArg("--init-settings");
var intervalSeconds = int.TryParse(GetArg("--interval"), out var n) ? Math.Max(10, n) : 30;
var markDone = GetArg("--mark-done");
var markFailed = GetArg("--mark-failed");
var failReason = GetArg("--reason") ?? "manual failure";

Console.WriteLine("Dawish Publisher Agent v0.4");
Console.WriteLine("بدون API — يقرأ مجلد المزامنة وينفذ المنشورات المستحقة بأمان.");
Console.WriteLine($"Sync: {syncFolder}");

var service = new SyncFolderService(syncFolder);

if (initSettings)
{
    await service.WriteAgentSettingsAsync(new AgentSettings
    {
        SyncFolder = syncFolder,
        ScanIntervalSeconds = intervalSeconds,
        StopBeforeFinalPublishClick = true,
        OpenPlatformsAutomatically = true,
        KeepTikTokImageOnlyByDefault = true,
        SnapchatImageOnly = true,
        PreventSleepWhileRunning = true,
        OpenPublishAssistantPage = true,
        CopyInstagramCaptionToClipboard = true,
        RequireManualDoneConfirmation = true
    });
    Console.WriteLine("تم إنشاء agent-settings.json داخل مجلد settings.");
    return;
}

if (!string.IsNullOrWhiteSpace(markDone))
{
    service.MarkDone(markDone);
    await service.WriteLogAsync("agent", $"manual done: {markDone}");
    Console.WriteLine($"تم تعليم المنشور كمنشور تم: {markDone}");
    return;
}

if (!string.IsNullOrWhiteSpace(markFailed))
{
    service.MarkFailed(markFailed, failReason);
    await service.WriteLogAsync("agent", $"manual failed: {markFailed} — {failReason}");
    Console.WriteLine($"تم تعليم المنشور كفشل: {markFailed}");
    return;
}

if (readinessOnly)
{
    var readiness = await service.CheckDeviceReadinessAsync();
    Console.WriteLine(readiness.Summary);
    Console.WriteLine($"Jobs: {readiness.ReadyJobsCount}, Due: {readiness.DueJobsCount}, Browser: {readiness.BrowserHint}");
    return;
}

var settings = await service.ReadAgentSettingsAsync();
intervalSeconds = settings.ScanIntervalSeconds > 0 ? settings.ScanIntervalSeconds : intervalSeconds;
var agent = new PublisherAgent(service);

if (!loop)
{
    using var awake = settings.PreventSleepWhileRunning ? new PowerAwakeService() : null;
    awake?.KeepAwake();
    var count = await agent.ProcessDueJobsAsync();
    Console.WriteLine($"تمت معالجة {count} منشور.");
    return;
}

using var keepAwake = settings.PreventSleepWhileRunning ? new PowerAwakeService() : null;
keepAwake?.KeepAwake();
Console.WriteLine($"Loop mode every {intervalSeconds} seconds. اضغط Ctrl+C للإيقاف.");
Console.WriteLine("سيتم منع Sleep أثناء عمل Agent إذا كان الإعداد مفعلًا.");
while (true)
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
        await service.WriteLogAsync("agent", "ERROR: " + ex.Message);
    }
    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
}

bool HasArg(string name) => args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

string? GetArg(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
    }
    return null;
}
