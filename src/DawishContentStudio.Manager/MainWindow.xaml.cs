using System.Globalization;
using System.IO;
using System.Windows;
using WpfControls = System.Windows.Controls;
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
    private CancellationTokenSource? _localPublisherCts;
    private Task? _localPublisherTask;

    public MainWindow()
    {
        InitializeComponent();
        SyncFolderTextBox.Text = _syncFolder;
        ScheduleDatePicker.SelectedDate = DateTime.Today;
        BulkStartDatePicker.SelectedDate = DateTime.Today;
        RefreshJobs();
        UpdateDeviceModeBadge();
    }

    protected override void OnClosed(EventArgs e)
    {
        _localPublisherCts?.Cancel();
        base.OnClosed(e);
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
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.webp|All files|*.*" };
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
                System.Windows.MessageBox.Show("المنشور يحتاج تعديل قبل الاعتماد.");
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
            System.Windows.MessageBox.Show(ex.Message, "خطأ");
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
        var dialog = new Microsoft.Win32.OpenFileDialog
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
            System.Windows.MessageBox.Show(ex.Message, "خطأ");
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
                System.Windows.MessageBox.Show("بعض المنشورات تحتاج تعديل:\n" + string.Join("\n", bad.Take(5).Select(x => x.Job.Title + ": " + string.Join("، ", x.Review.Issues))));
                return;
            }

            var service = new SyncFolderService(SyncFolderTextBox.Text.Trim());
            await service.CreateJobsAsync(jobs);
            StatusText.Text = $"تم اعتماد {jobs.Count} منشور مستقل وحفظها في مجلد المزامنة.";
            RefreshJobs();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "خطأ");
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
        InstagramPreviewCaptionText.Text = variants.Formal;
        TikTokPreviewCaptionText.Text = variants.Short + "\nبدون صوت — بدون أغاني";
        SnapchatPreviewCaptionText.Text = variants.Short;
    }

    private void CaptionVariantsListBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (CaptionVariantsListBox.SelectedItem is string text)
        {
            var idx = text.IndexOf(':');
            CaptionTextBox.Text = idx >= 0 ? text[(idx + 1)..].Trim() : text;
            PreviewCaptionText.Text = CaptionTextBox.Text;
            InstagramPreviewCaptionText.Text = CaptionTextBox.Text;
            TikTokPreviewCaptionText.Text = CaptionTextBox.Text + "\nبدون صوت — بدون أغاني";
            SnapchatPreviewCaptionText.Text = CaptionTextBox.Text.Length > 80 ? CaptionTextBox.Text[..80] : CaptionTextBox.Text;
        }
    }

    private void JobsListBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (JobsListBox.SelectedItem is string item)
        {
            PreviewCaptionText.Text = item;
        }
    }

    private async void StartLocalPublisher_Click(object sender, RoutedEventArgs e)
    {
        if (_localPublisherCts is not null)
        {
            System.Windows.MessageBox.Show("محطة النشر تعمل بالفعل على هذا الجهاز.");
            return;
        }

        try
        {
            var syncFolder = SyncFolderTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(syncFolder)) throw new InvalidOperationException("اختر مجلد المزامنة أولًا.");

            var service = new SyncFolderService(syncFolder);
            service.EnsureStructure();
            await service.WriteAgentSettingsAsync(new AgentSettings
            {
                SyncFolder = syncFolder,
                StopBeforeFinalPublishClick = true,
                OpenPlatformsAutomatically = true,
                KeepTikTokImageOnlyByDefault = true,
                SnapchatImageOnly = true,
                PreventSleepWhileRunning = true,
                OpenPublishAssistantPage = true,
                CopyInstagramCaptionToClipboard = true,
                RequireManualDoneConfirmation = true
            });

            _localPublisherCts = new CancellationTokenSource();
            var token = _localPublisherCts.Token;
            LocalPublisherStatusText.Text = "محطة النشر: تعمل على هذا الجهاز. لا تطفئ الجهاز ولا تتركه يدخل Sleep.";
            DashboardPublisherText.Text = "تعمل";

            _localPublisherTask = Task.Run(async () =>
            {
                using var awake = new PowerAwakeService();
                awake.KeepAwake();
                var agent = new PublisherAgent(new SyncFolderService(syncFolder));
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var count = await agent.ProcessDueJobsAsync(token);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            LocalPublisherStatusText.Text = $"محطة النشر: تعمل — آخر فحص عالج {count} منشور — {DateTime.Now:HH:mm:ss}";
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() => LocalPublisherStatusText.Text = "محطة النشر: خطأ — " + ex.Message);
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }
        catch (Exception ex)
        {
            _localPublisherCts = null;
            LocalPublisherStatusText.Text = "محطة النشر: متوقفة";
            DashboardPublisherText.Text = "متوقفة";
            System.Windows.MessageBox.Show(ex.Message, "خطأ تشغيل محطة النشر");
        }
    }

    private void StopLocalPublisher_Click(object sender, RoutedEventArgs e)
    {
        _localPublisherCts?.Cancel();
        _localPublisherCts = null;
        LocalPublisherStatusText.Text = "محطة النشر: متوقفة";
        DashboardPublisherText.Text = "متوقفة";
    }

    private async void RunLocalPublisherOnce_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var syncFolder = SyncFolderTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(syncFolder)) throw new InvalidOperationException("اختر مجلد المزامنة أولًا.");
            var agent = new PublisherAgent(new SyncFolderService(syncFolder));
            var count = await agent.ProcessDueJobsAsync();
            LocalPublisherStatusText.Text = $"تم فحص المنشورات الآن. تمت معالجة {count} منشور.";
            await RefreshStatusesAsync();
            await RefreshHeartbeatAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "خطأ");
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
            SyncStateCardText.Text = "جاهزة";
            SyncFolderShortText.Text = Path.GetFileName(_syncFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            TotalJobsCardText.Text = jobs.Count.ToString(CultureInfo.InvariantCulture);
            PendingJobsCardText.Text = jobs.Count(j => !j.Status.Contains("done", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            StatusText.Text = "تعذر قراءة المجلد: " + ex.Message;
            SyncStateCardText.Text = "مشكلة";
            SyncFolderShortText.Text = "راجع المجلد";
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
            ShopDeviceCardText.Text = heartbeats.Count == 0 ? "غير متصل" : "متصل";
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
        LastStatusCardText.Text = statuses.Count == 0 ? "لا يوجد" : $"{statuses[0].Overall} — {statuses[0].UpdatedAt:HH:mm}";
    }


    private async void MarkSelectedDone_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var jobId = GetSelectedStatusJobId();
            if (string.IsNullOrWhiteSpace(jobId))
            {
                System.Windows.MessageBox.Show("اختر حالة منشور من القائمة أولًا.");
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
            System.Windows.MessageBox.Show(ex.Message, "خطأ");
        }
    }

    private async void MarkSelectedFailed_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var jobId = GetSelectedStatusJobId();
            if (string.IsNullOrWhiteSpace(jobId))
            {
                System.Windows.MessageBox.Show("اختر حالة منشور من القائمة أولًا.");
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
            System.Windows.MessageBox.Show(ex.Message, "خطأ");
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
        ApprovalCenterMirrorListBox.Items.Clear();
        ApprovalListBox.Items.Add(result.Summary);
        ApprovalCenterMirrorListBox.Items.Add(result.Summary);
        foreach (var issue in result.Issues)
        {
            ApprovalListBox.Items.Add("❌ " + issue);
            ApprovalCenterMirrorListBox.Items.Add("❌ " + issue);
        }
        foreach (var warning in result.Warnings)
        {
            ApprovalListBox.Items.Add("⚠️ " + warning);
            ApprovalCenterMirrorListBox.Items.Add("⚠️ " + warning);
        }
        if (result.Issues.Count == 0 && result.Warnings.Count == 0)
        {
            ApprovalListBox.Items.Add("✅ لا توجد مشاكل");
            ApprovalCenterMirrorListBox.Items.Add("✅ لا توجد مشاكل");
        }
    }

    private void ShowDashboard_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 0;
    private void ShowOnboarding_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 8;
    private void ShowPosts_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 1;
    private void ShowBulk_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 2;
    private void ShowApprovalCenter_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 3;
    private void ShowPreview_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 4;
    private void ShowDevice_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 5;
    private void ShowArchive_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 6;
    private void ShowSettings_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 7;

    private void DeviceMode_Checked(object sender, RoutedEventArgs e) => UpdateDeviceModeBadge();

    private void UpdateDeviceModeBadge()
    {
        if (DeviceModeBadgeText is null) return;
        DeviceModeBadgeText.Text = PublisherModeRadio?.IsChecked == true ? "الوضع: جهاز المحل" : "الوضع: جهاز الإدارة";
    }

    private static DateTimeOffset ParseDateTime(DateTime? date, string timeText)
    {
        var dateOnly = (date ?? DateTime.Today).Date;
        if (!TimeSpan.TryParseExact(timeText.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var time) && !TimeSpan.TryParse(timeText.Trim(), out time))
            throw new InvalidOperationException("اكتب الوقت مثل 19:00");
        return new DateTimeOffset(dateOnly.Add(time));
    }

    private static TikTokPublishMode ReadTikTokMode(WpfControls.ComboBox combo)
    {
        if (combo.SelectedItem is WpfControls.ComboBoxItem item && item.Tag is string tag && Enum.TryParse<TikTokPublishMode>(tag, out var mode)) return mode;
        return TikTokPublishMode.ImageOnly;
    }
}
