using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DawishContentStudio.Core;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace DawishContentStudio.Manager;

public partial class MainWindow : Window
{
    private string _syncFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DawishSync");
    private string? _selectedImage;
    private readonly List<string> _bulkImages = [];
    private IReadOnlyList<PostJob> _bulkPreviewJobs = [];

    public MainWindow()
    {
        InitializeComponent();
        SyncFolderTextBox.Text = _syncFolder;
        ScheduleDatePicker.SelectedDate = DateTime.Today;
        BulkStartDatePicker.SelectedDate = DateTime.Today;
        RefreshJobs();
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog { Description = "اختر مجلد DawishSync المشترك بين الجهازين" };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            _syncFolder = dialog.SelectedPath;
            SyncFolderTextBox.Text = _syncFolder;
            RefreshJobs();
        }
    }

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.webp|All files|*.*" };
        if (dialog.ShowDialog() == true)
        {
            _selectedImage = dialog.FileName;
            ImagePathTextBox.Text = _selectedImage;
            PreviewImageText.Text = Path.GetFileName(_selectedImage);
        }
    }

    private async void CreateJob_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var job = BuildSingleJob();
            var media = _selectedImage ?? "";
            var approval = new ApprovalService().Review(job, media);
            ShowApproval(approval);
            if (!approval.IsApproved)
            {
                MessageBox.Show("المنشور يحتاج تعديل قبل الاعتماد.");
                return;
            }

            var service = new SyncFolderService(SyncFolderTextBox.Text.Trim());
            await service.CreateJobAsync(job, media);
            StatusText.Text = "تم اعتماد المنشور وحفظه في مجلد المزامنة.";
            PreviewCaptionText.Text = job.Captions.Instagram;
            RefreshJobs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطأ");
        }
    }

    private PostJob BuildSingleJob()
    {
        if (string.IsNullOrWhiteSpace(_selectedImage) || !File.Exists(_selectedImage)) throw new InvalidOperationException("اختر صورة أولًا.");
        var scheduled = ParseDateTime(ScheduleDatePicker.SelectedDate, ScheduleTimeTextBox.Text);
        var caption = CaptionTextBox.Text.Trim();
        var tiktokMode = ReadTikTokMode(TikTokModeComboBox);
        return new PostJob
        {
            Id = JobId.Create("job"),
            Title = TitleTextBox.Text.Trim(),
            SourceImagePath = _selectedImage,
            ScheduledAt = scheduled,
            WebsiteUrl = WebsiteTextBox.Text.Trim(),
            Platforms = new PlatformSelection
            {
                Instagram = InstagramCheckBox.IsChecked == true,
                TikTok = TikTokCheckBox.IsChecked == true,
                Snapchat = SnapchatCheckBox.IsChecked == true
            },
            Captions = new PostCaptions
            {
                Instagram = caption,
                TikTok = caption,
                Snapchat = caption.Length > 80 ? caption[..80] : caption
            },
            Rules = new SafetyRules { NoMedicalClaims = true, TikTokNoMusic = true, SnapchatImageOnly = true },
            Publishing = new PublishingOptions { TikTokMode = tiktokMode, StopBeforeFinalPublishClick = true }
        };
    }

    private void ChooseBulkImages_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            _bulkImages.Clear();
            _bulkImages.AddRange(dialog.FileNames);
            BulkCountText.Text = $"تم اختيار {_bulkImages.Count} صورة.";
            BulkPreviewListBox.Items.Clear();
            foreach (var file in _bulkImages) BulkPreviewListBox.Items.Add(Path.GetFileName(file));
        }
    }

    private void PreviewBulk_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _bulkPreviewJobs = BuildBulkJobs();
            BulkPreviewListBox.Items.Clear();
            foreach (var job in _bulkPreviewJobs)
            {
                BulkPreviewListBox.Items.Add($"{job.ScheduledAt:yyyy-MM-dd HH:mm} — {job.Title} — TikTok: {job.Publishing.TikTokMode} — مستقل");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطأ");
        }
    }

    private async void CreateBulkJobs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var jobs = _bulkPreviewJobs.Count > 0 ? _bulkPreviewJobs : BuildBulkJobs();
            var approval = new ApprovalService();
            var bad = jobs.Select(j => (Job: j, Review: approval.Review(j, j.SourceImagePath))).Where(x => !x.Review.IsApproved).ToList();
            if (bad.Count > 0)
            {
                MessageBox.Show("بعض المنشورات تحتاج تعديل:\n" + string.Join("\n", bad.Take(5).Select(x => x.Job.Title + ": " + string.Join("، ", x.Review.Issues))));
                return;
            }

            var service = new SyncFolderService(SyncFolderTextBox.Text.Trim());
            await service.CreateJobsAsync(jobs);
            StatusText.Text = $"تم اعتماد {jobs.Count} منشور مستقل وحفظها في مجلد المزامنة.";
            RefreshJobs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطأ");
        }
    }

    private IReadOnlyList<PostJob> BuildBulkJobs()
    {
        if (_bulkImages.Count == 0) throw new InvalidOperationException("اختر صور متعددة أولًا.");
        var start = ParseDateTime(BulkStartDatePicker.SelectedDate, BulkStartTimeTextBox.Text);
        _ = int.TryParse(BulkPostsPerDayTextBox.Text, out var perDay);
        _ = int.TryParse(BulkMinutesBetweenTextBox.Text, out var between);
        var request = new BulkPostRequest
        {
            ImagePaths = _bulkImages.ToArray(),
            TitlePrefix = BulkTitleTextBox.Text.Trim(),
            SharedCaption = BulkCaptionTextBox.Text.Trim(),
            WebsiteUrl = WebsiteTextBox.Text.Trim(),
            CampaignName = BulkCampaignTextBox.Text.Trim(),
            Platforms = new PlatformSelection
            {
                Instagram = BulkInstagramCheckBox.IsChecked == true,
                TikTok = BulkTikTokCheckBox.IsChecked == true,
                Snapchat = BulkSnapchatCheckBox.IsChecked == true
            },
            TikTokMode = ReadTikTokMode(BulkTikTokModeComboBox),
            StartAt = start,
            PostsPerDay = Math.Max(1, perDay),
            MinutesBetweenPosts = Math.Max(15, between),
            SkipFriday = BulkSkipFridayCheckBox.IsChecked == true,
            EachImageAsSeparatePost = true
        };
        return new BulkJobPlanner().CreateJobs(request);
    }

    private void GenerateCaptionVariants_Click(object sender, RoutedEventArgs e)
    {
        var variants = new CaptionVariantService().CreateSafeVariants(TitleTextBox.Text.Trim(), WebsiteTextBox.Text.Trim());
        CaptionVariantsListBox.Items.Clear();
        CaptionVariantsListBox.Items.Add("رسمي: " + variants.Formal);
        CaptionVariantsListBox.Items.Add("مختصر: " + variants.Short);
        CaptionVariantsListBox.Items.Add("تسويقي: " + variants.Marketing);
    }

    private void CaptionVariantsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptionVariantsListBox.SelectedItem is string text)
        {
            var idx = text.IndexOf(':');
            CaptionTextBox.Text = idx >= 0 ? text[(idx + 1)..].Trim() : text;
            PreviewCaptionText.Text = CaptionTextBox.Text;
        }
    }

    private void JobsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (JobsListBox.SelectedItem is string item)
        {
            PreviewCaptionText.Text = item;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();
    private async void RefreshHeartbeat_Click(object sender, RoutedEventArgs e) => await RefreshHeartbeatAsync();
    private async void RefreshStatuses_Click(object sender, RoutedEventArgs e) => await RefreshStatusesAsync();

    private async Task RefreshAllAsync()
    {
        RefreshJobs();
        await RefreshHeartbeatAsync();
        await RefreshStatusesAsync();
    }

    private async void RefreshJobs()
    {
        try
        {
            _syncFolder = SyncFolderTextBox.Text.Trim();
            var service = new SyncFolderService(_syncFolder);
            var jobs = await service.ListJobsAsync();
            JobsListBox.Items.Clear();
            foreach (var job in jobs)
            {
                JobsListBox.Items.Add($"{job.ScheduledAt:yyyy-MM-dd HH:mm} — {job.Title} — {job.Status} — TikTok:{job.Publishing.TikTokMode}");
            }
            StatusText.Text = $"المجلد جاهز: {_syncFolder}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "تعذر قراءة المجلد: " + ex.Message;
        }
    }

    private async Task RefreshHeartbeatAsync()
    {
        try
        {
            var service = new SyncFolderService(SyncFolderTextBox.Text.Trim());
            var heartbeats = await service.ListHeartbeatsAsync();
            HeartbeatListBox.Items.Clear();
            foreach (var h in heartbeats)
                HeartbeatListBox.Items.Add($"{h.Device} — {h.Mode} — {h.UpdatedAt:yyyy-MM-dd HH:mm:ss} — {h.LastMessage}");
            HeartbeatText.Text = heartbeats.Count == 0 ? "جهاز المحل: لا يوجد heartbeat" : $"آخر جهاز: {heartbeats[0].Device} قبل {(DateTimeOffset.Now - heartbeats[0].UpdatedAt).TotalMinutes:N0} دقيقة";
        }
        catch (Exception ex)
        {
            HeartbeatText.Text = "تعذر قراءة حالة جهاز المحل: " + ex.Message;
        }
    }

    private async Task RefreshStatusesAsync()
    {
        var service = new SyncFolderService(SyncFolderTextBox.Text.Trim());
        var statuses = await service.ListStatusesAsync();
        StatusesListBox.Items.Clear();
        foreach (var status in statuses)
            StatusesListBox.Items.Add($"{status.UpdatedAt:yyyy-MM-dd HH:mm} — {status.JobId} — {status.Overall} — IG:{status.Instagram} TK:{status.TikTok} SC:{status.Snapchat}");
    }


    private async void MarkSelectedDone_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var jobId = GetSelectedStatusJobId();
            if (string.IsNullOrWhiteSpace(jobId))
            {
                MessageBox.Show("اختر حالة منشور من القائمة أولًا.");
                return;
            }
            var service = new SyncFolderService(SyncFolderTextBox.Text.Trim());
            service.MarkDone(jobId);
            await service.WriteLogAsync("manager", "manual done from manager: " + jobId);
            await RefreshStatusesAsync();
            StatusText.Text = "تم تعليم المنشور كمنشور تم: " + jobId;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطأ");
        }
    }

    private async void MarkSelectedFailed_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var jobId = GetSelectedStatusJobId();
            if (string.IsNullOrWhiteSpace(jobId))
            {
                MessageBox.Show("اختر حالة منشور من القائمة أولًا.");
                return;
            }
            var service = new SyncFolderService(SyncFolderTextBox.Text.Trim());
            service.MarkFailed(jobId, "manual failure from manager");
            await service.WriteLogAsync("manager", "manual failed from manager: " + jobId);
            await RefreshStatusesAsync();
            StatusText.Text = "تم تعليم المنشور كفشل: " + jobId;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطأ");
        }
    }

    private string? GetSelectedStatusJobId()
    {
        if (StatusesListBox.SelectedItem is not string item) return null;
        var parts = item.Split(" — ");
        return parts.Length >= 2 ? parts[1].Trim() : null;
    }

    private void ShowApproval(ApprovalResult result)
    {
        ApprovalListBox.Items.Clear();
        ApprovalListBox.Items.Add(result.Summary);
        foreach (var issue in result.Issues) ApprovalListBox.Items.Add("❌ " + issue);
        foreach (var warning in result.Warnings) ApprovalListBox.Items.Add("⚠️ " + warning);
        if (result.Issues.Count == 0 && result.Warnings.Count == 0) ApprovalListBox.Items.Add("✅ لا توجد مشاكل");
    }

    private static DateTimeOffset ParseDateTime(DateTime? date, string timeText)
    {
        var dateOnly = (date ?? DateTime.Today).Date;
        if (!TimeSpan.TryParseExact(timeText.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var time) && !TimeSpan.TryParse(timeText.Trim(), out time))
            throw new InvalidOperationException("اكتب الوقت مثل 19:00");
        return new DateTimeOffset(dateOnly.Add(time));
    }

    private static TikTokPublishMode ReadTikTokMode(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag && Enum.TryParse<TikTokPublishMode>(tag, out var mode)) return mode;
        return TikTokPublishMode.ImageOnly;
    }
}
