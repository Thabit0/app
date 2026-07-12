using Microsoft.Playwright;
using System.IO;

namespace DawishContentStudio.Manager;

public sealed class PersistentBrowserSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowserContext? _context;

    public PersistentBrowserSession(string? profileFolder = null)
    {
        ProfileFolder = profileFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DawishContentStudio", "PublishingBrowser");
    }

    public string ProfileFolder { get; }

    public async Task<IPage> OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(cancellationToken);
        var page = await context.NewPageAsync();
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 });
        await page.BringToFrontAsync();
        return page;
    }

    private async Task<IBrowserContext> GetContextAsync(CancellationToken cancellationToken)
    {
        if (_context is not null) return _context;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_context is not null) return _context;
            Directory.CreateDirectory(ProfileFolder);
            _playwright = await Playwright.CreateAsync();
            _context = await _playwright.Chromium.LaunchPersistentContextAsync(ProfileFolder, new()
            {
                Channel = "msedge",
                Headless = false,
                AcceptDownloads = true,
                ViewportSize = ViewportSize.NoViewport
            });
            return _context;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) await _context.CloseAsync();
        _playwright?.Dispose();
        _gate.Dispose();
    }
}
