using DawishContentStudio.Core;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DawishContentStudio.Manager;

public partial class MainWindow : Window
{
    private const string AdminPinHash = "77953d681afa5024b10da16b077cde32448d4c1b329b7fb9815974b716dd05c3";
    private const string ShopPinHash = "91b4d142823f7d20c5f08df69122de43f35f057a988d9619f6d3138485c9a203";
    private const string FixedCaption = "زوروا متجرنا بالبايو لرؤية باقي المنتجات\n#fyp #explore #عطارة_الدويش_الزلفي";
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly AppSettingsStore _settingsStore = new();
    private AppSettings _settings;
    private CancellationTokenSource? _shopCts;

    public ObservableCollection<LocalImageItem> Images { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _settings = _settingsStore.Load();

        HourComboBox.ItemsSource = Enumerable.Range(1, 12).Select(x => x.ToString("00"));
        MinuteComboBox.ItemsSource = new[] { "00", "15", "30", "45" };
        DailyCountComboBox.ItemsSource = Enumerable.Range(1, 20).ToList();
        HourComboBox.SelectedItem = "07";
        MinuteComboBox.SelectedItem = "00";
        DailyCountComboBox.SelectedItem = 4;
        ScheduleDatePicker.SelectedDate = DateTime.Today;

        RootTabs.SelectedIndex = _settings.IsLinked ? 1 : 0;
        if (_settings.IsLinked) PinPasswordBox.Focus();
    }

    private CloudflareClient Client()
    {
        var token = Unprotect(_settings.Token);
        if (string.IsNullOrWhiteSpace(_settings.WorkerUrl) || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("الجهاز غير مربوط بـ Cloudflare.");
        return new CloudflareClient(_settings.WorkerUrl, token);
    }

    private async void LinkDevice_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetupStatus.Text = "جاري فحص الاتصال...";
            var role = SetupShopRadio.IsChecked == true ? DeviceRole.Shop : DeviceRole.Admin;
            var url = SetupWorkerUrl.Text.Trim();
            var token = SetupToken.Password.Trim();
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("أدخل رابط Worker والـ Token.");

            var health = await new CloudflareClient(url, token).HealthAsync();
            if (!health.Ok) throw new InvalidOperationException("تعذر التحقق من Cloudflare.");

            _settings = new AppSettings
            {
                WorkerUrl = url,
                Token = Protect(token),
                Role = role,
                IsLinked = true,
                PinHash = role == DeviceRole.Admin ? AdminPinHash : ShopPinHash
            };
            _settingsStore.Save(_settings);
            SetupToken.Clear();
            SetupStatus.Text = "تم ربط الجهاز بنجاح.";
            RootTabs.SelectedIndex = 1;
            PinPasswordBox.Focus();
        }
        catch (Exception ex)
        {
            SetupStatus.Text = ex.Message;
        }
    }

    private void Login_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void PinPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }

    private void TryLogin()
    {
        var enteredHash = HashPin(PinPasswordBox.Password);
        var expected = string.IsNullOrWhiteSpace(_settings.PinHash)
            ? (_settings.Role == DeviceRole.Admin ? AdminPinHash : ShopPinHash)
            : _settings.PinHash;

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(enteredHash), Convert.FromHexString(expected)))
        {
            LoginErrorText.Text = "رمز الدخول غير صحيح.";
            PinPasswordBox.Clear();
            return;
        }

        LoginErrorText.Text = "";
        PinPasswordBox.Clear();
        if (_settings.Role == DeviceRole.Admin)
        {
            RootTabs.SelectedIndex = 2;
            _ = CheckManagerConnectionAsync();
        }
        else
        {
            RootTabs.SelectedIndex = 3;
            _ = RefreshDuePostsAsync();
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        StopShopStation_Click(sender, e);
        RootTabs.SelectedIndex = 1;
        PinPasswordBox.Focus();
    }

    private async Task CheckManagerConnectionAsync()
    {
        try
        {
            var result = await Client().HealthAsync();
            ManagerConnectionText.Text = result.Ok ? "Cloudflare متصل — جهاز المالك" : "Cloudflare غير متصل";
        }
        catch (Exception ex)
        {
            ManagerConnectionText.Text = "فشل الاتصال: " + ex.Message;
        }
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "اختر مجلد الصور", Multiselect = false };
        if (dialog.ShowDialog() != true) return;

        var files = Directory.EnumerateFiles(dialog.FolderName, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f), StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        Images.Clear();
        foreach (var file in files)
            Images.Add(new LocalImageItem { Path = file, FileName = Path.GetFileName(file) });
        UpdateProgress();
        ManagerStatusText.Text = files.Length == 0 ? "لم أجد صورًا داخل المجلد." : $"تم استيراد {files.Length} صورة. كل صورة منشور مستقل.";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var image in Images) image.IsSelected = true;
        UpdateSelectionCount();
    }

    private void SelectUnscheduled_Click(object sender, RoutedEventArgs e)
    {
        foreach (var image in Images) image.IsSelected = !image.ScheduledAt.HasValue;
        UpdateSelectionCount();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var image in Images) image.IsSelected = false;
        UpdateSelectionCount();
    }

    private void OpenSchedulePanel_Click(object sender, RoutedEventArgs e) => UpdateSelectionCount();
    private void OpenAutoPanel_Click(object sender, RoutedEventArgs e) => ManagerStatusText.Text = "حدد تاريخ البداية والوقت وعدد الصور يوميًا ثم اضغط التوزيع التلقائي.";

    private void ApplySchedule_Click(object sender, RoutedEventArgs e)
    {
        var selected = Images.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) { MessageBox.Show("حدد صورة أو أكثر أولًا."); return; }
        var dateTime = ReadScheduleDateTime();
        foreach (var image in selected) image.ScheduledAt = dateTime;
        ClearSelection_Click(sender, e);
        UpdateProgress();
        ManagerStatusText.Text = $"تم تعيين {selected.Count} صورة في {FormatDateTime(dateTime)}.";
    }

    private void ApplyAutoSchedule_Click(object sender, RoutedEventArgs e)
    {
        var pending = Images.Where(x => !x.ScheduledAt.HasValue).ToList();
        if (pending.Count == 0) { MessageBox.Show("لا توجد صور غير مجدولة."); return; }
        var perDay = DailyCountComboBox.SelectedItem is int n ? n : 4;
        var start = ReadScheduleDateTime();
        for (var i = 0; i < pending.Count; i++) pending[i].ScheduledAt = start.AddDays(i / perDay);
        UpdateProgress();
        ManagerStatusText.Text = $"تم توزيع {pending.Count} صورة تلقائيًا بمعدل {perDay} صور يوميًا.";
    }

    private async void FinishUpload_Click(object sender, RoutedEventArgs e)
    {
        if (Images.Count == 0) { MessageBox.Show("استورد مجلد الصور أولًا."); return; }
        var unscheduled = Images.Count(x => !x.ScheduledAt.HasValue);
        if (unscheduled > 0) { MessageBox.Show($"بقي {unscheduled} صورة غير مجدولة."); return; }

        var confirmation = MessageBox.Show($"سيتم رفع {Images.Count} منشور مستقل إلى Cloudflare. هل نبدأ؟", "تأكيد", MessageBoxButton.OKCancel);
        if (confirmation != MessageBoxResult.OK) return;

        try
        {
            var uploaded = 0;
            foreach (var image in Images)
            {
                if (!string.IsNullOrWhiteSpace(image.RemoteId)) { uploaded++; continue; }
                image.Status = "جاري الرفع";
                ManagerStatusText.Text = $"جاري رفع {uploaded + 1} من {Images.Count}";
                var response = await Client().UploadPostAsync(BuildRequest(image.ScheduledAt!.Value), image.Path);
                image.RemoteId = response.Id;
                image.Status = "تم الرفع";
                uploaded++;
                ScheduleProgress.Value = 100.0 * uploaded / Images.Count;
            }
            ManagerStatusText.Text = $"تم رفع {uploaded} منشور مستقل بنجاح.";
            MessageBox.Show("اكتمل رفع الجدول إلى Cloudflare.");
        }
        catch (Exception ex)
        {
            ManagerStatusText.Text = "توقفت العملية: " + ex.Message;
            MessageBox.Show(ex.Message, "خطأ أثناء الرفع");
        }
    }

    private static PostUploadRequest BuildRequest(DateTime scheduledAt) => new()
    {
        Caption = FixedCaption,
        InstagramEnabled = true,
        TikTokEnabled = true,
        SnapchatEnabled = true,
        InstagramLocation = "مدينة الزلفي",
        TikTokLocation = "عطارة الدويش",
        SnapchatLocation = "",
        ScheduledAt = scheduledAt,
        TikTokMode = "image_only",
        SnapchatMode = "image_only"
    };

    private DateTime ReadScheduleDateTime()
    {
        var date = ScheduleDatePicker.SelectedDate ?? DateTime.Today;
        var hour = int.TryParse(HourComboBox.SelectedItem?.ToString(), out var h) ? h : 7;
        var minute = int.TryParse(MinuteComboBox.SelectedItem?.ToString(), out var m) ? m : 0;
        var isPm = (AmPmComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "م";
        if (hour == 12) hour = 0;
        if (isPm) hour += 12;
        return date.Date.AddHours(hour).AddMinutes(minute);
    }

    private void UpdateProgress()
    {
        var scheduled = Images.Count(x => x.ScheduledAt.HasValue);
        var remaining = Images.Count - scheduled;
        var percent = Images.Count == 0 ? 0 : 100.0 * scheduled / Images.Count;
        ScheduleProgress.Value = percent;
        ProgressTitle.Text = Images.Count == 0 ? "استورد مجلد الصور للبدء" : $"{Images.Count} صورة — {scheduled} مجدولة";
        ProgressHint.Text = Images.Count == 0 ? "كل صورة ستصبح منشورًا مستقلًا" : $"بقي {remaining} صورة غير مجدولة — {percent:0}%";
        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        var count = Images.Count(x => x.IsSelected);
        SelectedCountText.Text = count == 0 ? "لم تحدد صورًا" : $"تم تحديد {count} صورة";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (RootTabs.SelectedIndex != 2) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A) { SelectAll_Click(sender, e); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O) { ChooseFolder_Click(sender, e); e.Handled = true; }
    }

    private void OpenInstagram_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.instagram.com/accounts/login/");
    private void OpenTikTok_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.tiktok.com/login");
    private void OpenSnapchat_Click(object sender, RoutedEventArgs e) => OpenUrl("https://web.snapchat.com/");

    private void TestAccounts_Click(object sender, RoutedEventArgs e)
    {
        AccountsStatusText.Text = "افتح المنصات الثلاث وسجّل الدخول. الجلسات تبقى محليًا داخل المتصفح على جهاز المحل.";
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async void RefreshDuePosts_Click(object sender, RoutedEventArgs e) => await RefreshDuePostsAsync();

    private async Task RefreshDuePostsAsync()
    {
        try
        {
            var posts = await Client().GetDuePostsAsync();
            DuePostsListBox.Items.Clear();
            foreach (var p in posts)
                DuePostsListBox.Items.Add($"{FormatDateTime(p.ScheduledAt.ToLocalTime())} — {p.Caption.Split('\n')[0]}");
            ShopConnectionText.Text = "Cloudflare متصل";
            if (posts.Count == 0) DuePostsListBox.Items.Add("لا توجد منشورات مستحقة الآن.");
        }
        catch (Exception ex)
        {
            ShopConnectionText.Text = "فشل الاتصال";
            DuePostsListBox.Items.Clear();
            DuePostsListBox.Items.Add(ex.Message);
        }
    }

    private void StartShopStation_Click(object sender, RoutedEventArgs e)
    {
        if (_shopCts is not null) return;
        _shopCts = new CancellationTokenSource();
        ShopStationText.Text = "تعمل — فحص كل 45 ثانية";
        _ = ShopLoopAsync(_shopCts.Token);
    }

    private void StopShopStation_Click(object sender, RoutedEventArgs e)
    {
        _shopCts?.Cancel();
        _shopCts = null;
        ShopStationText.Text = "متوقفة";
    }

    private async Task ShopLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Client().HeartbeatAsync(Environment.MachineName, "shop");
                await RefreshDuePostsAsync();
                await Task.Delay(TimeSpan.FromSeconds(45), token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ShopStationText.Text = "خطأ: " + ex.Message;
                await Task.Delay(TimeSpan.FromSeconds(45), token);
            }
        }
    }

    private static string HashPin(string pin) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pin))).ToLowerInvariant();

    private static string Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        try
        {
            var bytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
        }
        catch
        {
            return value;
        }
    }

    private static string FormatDateTime(DateTime value) => value.ToString("dd/MM/yyyy hh:mm tt").Replace("AM", "ص").Replace("PM", "م");
}
