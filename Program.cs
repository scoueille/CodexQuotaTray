using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Microsoft.Win32;
using System.Text.Json;
using System.Windows.Forms;

namespace CodexQuotaTray;

internal static class Program
{
    internal const string AppUserModelId = "CodexQuotaTray.Desktop";

    /// <summary>Version de l'assembly affichée dans le menu de l'icône et transmise à la CLI.</summary>
    internal static string AppVersion { get; } = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    // Démarre uniquement la fenêtre cachée qui porte l'icône et la boucle d'actualisation.
    /// <summary>Configure la langue et les préférences, puis lance l'application de zone de notification.</summary>
    [STAThread]
    private static void Main()
    {
        var appIdResult = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        if (appIdResult != 0)
            NotificationDiagnostics.Log($"process app ID registration failed: 0x{appIdResult:X8}");
        AppText.Initialize();
        QuotaPreferences.Load();
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationForm());
    }

    /// <summary>Assigns a stable Windows identity so notifications map to the installed Start shortcut.</summary>
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}

internal sealed class TrayApplicationForm : Form
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _widgetAttachTimer;
    private readonly TaskbarWidgetController _taskbarWidget;
    private readonly QuotaNotifier _quotaNotifier = new();
    private readonly string? _codexPath;
    private Icon? _currentIcon;
    private bool _refreshing;

    /// <summary>Crée l'icône, son menu et le minuteur qui actualise les quotas.</summary>
    public TrayApplicationForm()
    {
        // La fenêtre reste invisible : l'interface de l'application est le menu de l'icône de notification.
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;
        _codexPath = CodexCli.FindPath();
        _taskbarWidget = new TaskbarWidgetController(QuotaPreferences.TaskbarWidgetVisible, this);
        _statusItem = new ToolStripMenuItem(AppText.Get("Menu.StatusStartup")) { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(AppText.Get("Menu.Refresh"), null, (_, _) => RefreshQuota());
        menu.Items.Add(AppText.Get("Menu.Settings"), null, (_, _) => OpenSettings());
        var widgetItem = new ToolStripMenuItem(AppText.Get("Menu.ShowTaskbarWidget"))
        {
            CheckOnClick = true,
            Checked = _taskbarWidget.IsVisible
        };
        widgetItem.Click += (_, _) =>
        {
            var previousVisibility = _taskbarWidget.IsVisible;
            try
            {
                _taskbarWidget.SetVisible(widgetItem.Checked);
                QuotaPreferences.SaveTaskbarWidgetVisible(widgetItem.Checked);
            }
            catch (Exception ex)
            {
                widgetItem.Checked = previousVisibility;
                _taskbarWidget.SetVisible(previousVisibility);
                MessageBox.Show(
                    AppText.Format("Dialog.SettingsSaveError", ex.Message),
                    AppText.Get("Dialog.SettingsTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };
        menu.Items.Add(widgetItem);
        var startupItem = new ToolStripMenuItem(AppText.Get("Menu.StartWithWindows"))
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled()
        };
        startupItem.Click += (_, _) =>
        {
            try
            {
                StartupRegistration.SetEnabled(startupItem.Checked);
            }
            catch (Exception ex)
            {
                startupItem.Checked = !startupItem.Checked;
                MessageBox.Show(AppText.Format("Dialog.AutoStartError", ex.Message), AppText.Get("Dialog.AutoStartTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(AppText.Get("Menu.SignIn"), null, (_, _) => StartLogin());
        menu.Items.Add(AppText.Get("Menu.OpenDashboard"), null, (_, _) => OpenUsageDashboard());
        menu.Items.Add(new ToolStripMenuItem(AppText.Format("Menu.Version", Program.AppVersion)) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(AppText.Get("Menu.Quit"), null, (_, _) => Close());

        _currentIcon = TrayIcon.CreateUnavailable();
        _icon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = AppText.Get("Tooltip.Loading"),
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.BalloonTipShown += (_, _) => NotificationDiagnostics.Log("Windows displayed a notification balloon");
        _icon.BalloonTipClosed += (_, _) => NotificationDiagnostics.Log("Windows closed a notification balloon");
        _icon.DoubleClick += (_, _) => OpenUsageDashboard();

        _timer = new System.Windows.Forms.Timer { Interval = 5 * 60 * 1000 };
        _timer.Tick += (_, _) => RefreshQuota();
        _timer.Start();
        _widgetAttachTimer = new System.Windows.Forms.Timer { Interval = 750 };
        _widgetAttachTimer.Tick += (_, _) => _taskbarWidget.EnsureAttached();
        _widgetAttachTimer.Start();
        _taskbarWidget.EnsureAttached();
        RefreshQuota();
    }

    /// <summary>Lance la lecture des quotas sans bloquer l'interface.</summary>
    private void RefreshQuota()
    {
        if (_refreshing) return;
        if (_codexPath is null)
        {
            SetError(AppText.Get("Error.CliNotFound"));
            return;
        }

        _refreshing = true;
        _statusItem.Text = AppText.Get("Status.ReadingQuota");
        // L'appel CLI peut attendre le serveur; il s'exécute hors du fil UI pour garder le menu réactif.
        _ = Task.Run(async () =>
        {
            try
            {
                var quota = await CodexCli.ReadQuotaAsync(_codexPath);
                BeginInvoke(() => SetQuota(quota));
            }
            catch (Exception ex)
            {
                BeginInvoke(() => SetError(ex.Message));
            }
        });
    }

    /// <summary>Affiche les préférences et applique le nombre de jours enregistré.</summary>
    private void OpenSettings()
    {
        using var dialog = new SettingsDialog(QuotaPreferences.WorkDaysPerWeek);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            QuotaPreferences.Save(dialog.WorkDaysPerWeek);
            _quotaNotifier.ResetBaseline();
            RefreshQuota();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                AppText.Format("Dialog.SettingsSaveError", ex.Message),
                AppText.Get("Dialog.SettingsTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>Met à jour le menu, l'icône et les alertes à partir des quotas reçus.</summary>
    private void SetQuota(Quota quota)
    {
        _refreshing = false;
        // Le rythme quotidien est estimé à partir du quota hebdomadaire et du jour courant de la fenêtre.
        var dailyPace = DailyQuotaPace.Calculate(
            quota.WeeklyUsedPercent,
            quota.WeeklyWindowDurationMinutes,
            quota.WeeklyReset,
            DateTimeOffset.Now,
            QuotaPreferences.WorkDaysPerWeek);
        var notification = _quotaNotifier.GetCrossings(dailyPace.PercentOfDailyAllocation, quota.FiveHourRemaining);
        var shortText = AppText.Format("Status.Summary", quota.FiveHourRemaining, quota.WeeklyRemaining);
        _statusItem.Text = AppText.Format("Status.Details", shortText, dailyPace.PercentOfDailyAllocation, dailyPace.Label);
        var resetFormat = AppText.Get("Tooltip.ResetFormat");
        var tooltip = string.Join(
            Environment.NewLine,
            AppText.Format("Tooltip.Weekly", quota.WeeklyRemaining, quota.WeeklyReset.ToString(resetFormat, AppText.Culture)),
            AppText.Format("Tooltip.FiveHour", quota.FiveHourRemaining, quota.FiveHourReset.ToString(resetFormat, AppText.Culture)));
        _icon.Text = tooltip;
        SetIcon(TrayIcon.Create(quota.WeeklyRemaining, quota.FiveHourRemaining, dailyPace.Color));
        _taskbarWidget.SetQuota(quota, dailyPace.Color);
        if (notification is not null)
            ShowNotification(notification.Title, notification.Message, "threshold crossing");
    }

    /// <summary>Requests a notification balloon and records whether Windows reports displaying it.</summary>
    private void ShowNotification(string title, string message, string reason)
    {
        NotificationDiagnostics.Log($"notification requested ({reason}): {message}");
        try
        {
            _icon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
        }
        catch (Exception error)
        {
            NotificationDiagnostics.Log($"notification request failed: {error}");
        }
    }

    /// <summary>Affiche une erreur d'état et remplace l'icône par son apparence indisponible.</summary>
    private void SetError(string message)
    {
        _refreshing = false;
        _statusItem.Text = message.Length > 70 ? message[..67] + "…" : message;
        _icon.Text = AppText.Get("Tooltip.Unavailable");
        SetIcon(TrayIcon.CreateUnavailable());
        _taskbarWidget.SetUnavailable();
    }

    /// <summary>Remplace l'icône de notification et libère l'ancienne ressource graphique.</summary>
    private void SetIcon(Icon icon)
    {
        var previous = _currentIcon;
        _currentIcon = icon;
        _icon.Icon = icon;
        previous?.Dispose();
    }

    /// <summary>Ouvre la procédure de connexion de la CLI Codex.</summary>
    private void StartLogin()
    {
        if (_codexPath is null)
        {
            SetError(AppText.Get("Error.CliNotFound"));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_codexPath, "login") { UseShellExecute = true });
            _statusItem.Text = AppText.Get("Status.SignInComplete");
        }
        catch (Exception ex)
        {
            SetError(AppText.Format("Error.LoginStart", ex.Message));
        }
    }

    /// <summary>Ouvre dans le navigateur la page Codex des quotas et de leur utilisation.</summary>
    private static void OpenUsageDashboard()
    {
        Process.Start(new ProcessStartInfo("https://chatgpt.com/codex/settings/usage") { UseShellExecute = true });
    }

    /// <summary>Masque la fenêtre technique dès qu'elle a été affichée.</summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Hide();
    }

    /// <summary>Libère le minuteur et les icônes quand l'application se ferme.</summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Libère les ressources graphiques et l'icône système quand l'utilisateur quitte l'application.
        _timer.Stop();
        _timer.Dispose();
        _widgetAttachTimer.Stop();
        _widgetAttachTimer.Dispose();
        _taskbarWidget.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _currentIcon?.Dispose();
        base.OnFormClosed(e);
    }
}

/// <summary>Contient le titre et le texte d'une notification de quota.</summary>
internal sealed record QuotaNotification(string Title, string Message);

/// <summary>Writes notification delivery events to a local diagnostic log.</summary>
internal static class NotificationDiagnostics
{
    /// <summary>Appends a timestamped notification event without affecting the tray app if logging fails.</summary>
    public static void Log(string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexQuotaTray");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "Notifications.log"),
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // A diagnostic log must never keep the quota indicator from running.
        }
    }
}

internal sealed class QuotaNotifier
{
    private static readonly int[] DailyThresholds = [25, 50, 75, 100];
    private static readonly int[] FiveHourThresholds = [50, 25];
    private bool _hasPreviousSample;
    private double _previousDailyPace;
    private int _previousFiveHourRemaining;

    /// <summary>Crée une notification si une limite quotidienne ou de cinq heures vient d'être franchie.</summary>
    public QuotaNotification? GetCrossings(double dailyPace, int fiveHourRemaining)
    {
        // La première lecture initialise la référence sans produire d'alerte rétroactive.
        if (!_hasPreviousSample)
        {
            Remember(dailyPace, fiveHourRemaining);
            return null;
        }

        var lines = new List<string>();
        var crossedDaily = DailyThresholds
            .Where(threshold => _previousDailyPace < threshold && dailyPace >= threshold)
            .Select(threshold => AppText.Format("Notification.DailyThreshold", threshold));
        var dailyMessage = string.Join(AppText.Get("List.Separator"), crossedDaily);
        if (dailyMessage.Length > 0)
            lines.Add(AppText.Format("Notification.DailyLine", dailyMessage));

        var crossedFiveHour = FiveHourThresholds
            .Where(threshold => _previousFiveHourRemaining >= threshold && fiveHourRemaining < threshold)
            .Select(threshold => AppText.Format("Notification.FiveHourThreshold", threshold));
        var fiveHourMessage = string.Join(AppText.Get("List.And"), crossedFiveHour);
        if (fiveHourMessage.Length > 0)
            lines.Add(AppText.Format("Notification.FiveHourLine", fiveHourMessage));

        Remember(dailyPace, fiveHourRemaining);
        return lines.Count == 0
            ? null
            : new QuotaNotification(AppText.Get("Notification.Title"), string.Join(" ", lines));
    }

    /// <summary>Oublie le relevé précédent afin de redémarrer le suivi sans alerte immédiate.</summary>
    public void ResetBaseline() => _hasPreviousSample = false;

    /// <summary>Mémorise les valeurs courantes pour détecter les prochains franchissements.</summary>
    private void Remember(double dailyPace, int fiveHourRemaining)
    {
        _previousDailyPace = dailyPace;
        _previousFiveHourRemaining = fiveHourRemaining;
        _hasPreviousSample = true;
    }
}

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexQuotaTray";

    /// <summary>Indique si le lancement automatique de l'application est activé pour cet utilisateur.</summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>Active ou désactive le lancement de l'application à l'ouverture de session Windows.</summary>
    public static void SetEnabled(bool enabled)
    {
        // La clé Run sous HKCU active le démarrage pour l'utilisateur courant, sans droits administrateur.
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException(AppText.Get("Error.StartupKeyUnavailable"));
        if (enabled)
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

internal static class CodexCli
{
    /// <summary>Recherche l'exécutable Codex dans le chemin configuré, PATH ou son dossier habituel.</summary>
    public static string? FindPath()
    {
        // Priorité au chemin explicitement configuré, puis PATH, puis emplacement habituel de l'installation Codex.
        var configured = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(directory.Trim('"'), "codex.exe");
            if (File.Exists(candidate)) return candidate;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var installRoot = Path.Combine(localAppData, "OpenAI", "Codex", "bin");
        if (Directory.Exists(installRoot))
        {
            return Directory.GetFiles(installRoot, "codex.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        return null;
    }

    /// <summary>Interroge le serveur local de la CLI Codex et renvoie les quotas du compte.</summary>
    public static async Task<Quota> ReadQuotaAsync(string codexPath)
    {
        // Démarre app-server en mode stdio et échange avec lui en JSON-RPC, sans lire de jeton d'authentification.
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(codexPath, "app-server --stdio")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.Environment["CODEX_NO_UPDATE_CHECK"] = "1";
        process.ErrorDataReceived += (_, _) => { };
        if (!process.Start()) throw new InvalidOperationException(AppText.Get("Error.AppServerStart"));
        process.BeginErrorReadLine();

        try
        {
            var initializeRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    clientInfo = new { name = "codex-quota-tray", version = Program.AppVersion },
                    capabilities = new { experimentalApi = true }
                }
            });
            await process.StandardInput.WriteLineAsync(initializeRequest);
            await process.StandardInput.FlushAsync();
            using var initialize = await ReadResponseAsync(process, 1);
            if (initialize.RootElement.TryGetProperty("error", out var initError))
                throw new InvalidOperationException(ReadError(initError));

            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"initialized\",\"params\":{}}");
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":{\"excludeResetCreditDetails\":true}}");
            await process.StandardInput.FlushAsync();

            using var response = await ReadResponseAsync(process, 2);
            return ParseQuota(response.RootElement);
        }
        finally
        {
            try { process.StandardInput.Close(); } catch { }
            if (!process.HasExited) process.Kill(true);
        }
    }

    /// <summary>Convertit une réponse JSON-RPC de quotas en valeurs utilisables par l'interface.</summary>
    internal static Quota ParseQuota(JsonElement response)
    {
        if (response.TryGetProperty("error", out var error))
        {
            var message = ReadError(error);
            if (message.Contains("authentication required", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(AppText.Get("Error.CliNotConnected"));
            throw new InvalidOperationException(message);
        }

        var result = response.GetProperty("result");
        var limits = result.GetProperty("rateLimits");
        if (limits.ValueKind == JsonValueKind.Null || !limits.TryGetProperty("primary", out var primary) || primary.ValueKind == JsonValueKind.Null)
            throw new InvalidOperationException(AppText.Get("Error.PrimaryQuotaMissing"));

        var weekly = limits.GetProperty("secondary");
        return new Quota(
            Remaining(primary),
            Remaining(weekly),
            UsedPercent(weekly),
            WindowDurationMinutes(weekly),
            Reset(primary),
            Reset(weekly));
    }

    /// <summary>Attend la réponse JSON-RPC correspondant à l'identifiant demandé.</summary>
    private static async Task<JsonDocument> ReadResponseAsync(Process process, int expectedId)
    {
        // Ignore les notifications non sollicitées et attend la réponse portant l'identifiant JSON-RPC demandé.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
            if (line is null) throw new InvalidOperationException(AppText.Get("Error.AppServerClosed"));
            using var message = JsonDocument.Parse(line);
            if (!message.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || id.GetInt32() != expectedId)
                continue;
            return JsonDocument.Parse(line);
        }
    }

    /// <summary>Calcule le pourcentage de quota restant dans une fenêtre de limite.</summary>
    private static int Remaining(JsonElement window) => Math.Clamp(100 - window.GetProperty("usedPercent").GetInt32(), 0, 100);

    /// <summary>Lit le pourcentage déjà consommé dans une fenêtre de limite.</summary>
    private static int UsedPercent(JsonElement window) => window.GetProperty("usedPercent").GetInt32();

    /// <summary>Lit la durée de la fenêtre de quota si le serveur l'a fournie.</summary>
    private static long? WindowDurationMinutes(JsonElement window) =>
        window.TryGetProperty("windowDurationMins", out var duration) && duration.ValueKind == JsonValueKind.Number
            ? duration.GetInt64()
            : null;

    /// <summary>Convertit l'heure de réinitialisation Unix en heure locale, ou renvoie une valeur vide.</summary>
    private static DateTimeOffset Reset(JsonElement window)
    {
        if (!window.TryGetProperty("resetsAt", out var reset) || reset.ValueKind != JsonValueKind.Number)
            return DateTimeOffset.MinValue;
        return DateTimeOffset.FromUnixTimeSeconds(reset.GetInt64()).ToLocalTime();
    }

    /// <summary>Extrait le message d'erreur JSON-RPC ou renvoie le texte d'erreur générique.</summary>
    private static string ReadError(JsonElement error) =>
        error.TryGetProperty("message", out var message) ? message.GetString() ?? AppText.Get("Error.Generic") : AppText.Get("Error.Generic");
}

/// <summary>Regroupe les quotas restants et les heures de réinitialisation renvoyés par Codex.</summary>
internal sealed record Quota(
    int FiveHourRemaining,
    int WeeklyRemaining,
    int WeeklyUsedPercent,
    long? WeeklyWindowDurationMinutes,
    DateTimeOffset FiveHourReset,
    DateTimeOffset WeeklyReset);

/// <summary>Décrit le rythme quotidien calculé, sa couleur et son libellé.</summary>
internal sealed record DailyPaceStatus(double PercentOfDailyAllocation, Color Color, string Label);

internal static class DailyQuotaPace
{
    /// <summary>Compare l'usage hebdomadaire à l'allocation quotidienne selon le jour courant.</summary>
    public static DailyPaceStatus Calculate(
        int weeklyUsedPercent,
        long? windowDurationMinutes,
        DateTimeOffset weeklyReset,
        DateTimeOffset now,
        int workDaysPerWeek)
    {
        if (windowDurationMinutes is null or <= 0 || weeklyReset == DateTimeOffset.MinValue)
            return new DailyPaceStatus(0, Color.Gray, AppText.Get("Pace.Unavailable"));

        var allocationDays = Math.Clamp(workDaysPerWeek, 1, 7);
        var weekStart = weeklyReset.ToUniversalTime() - TimeSpan.FromMinutes(windowDurationMinutes.Value);
        var elapsedDays = Math.Clamp((now.ToUniversalTime() - weekStart).TotalDays, 0d, 6.999999d);
        var completedDays = Math.Clamp((int)Math.Floor(elapsedDays), 0, allocationDays - 1);
        var dailyAllocationPercent = 100d / allocationDays;

        // Compare la consommation hebdomadaire à la cible cumulée des jours précédents :
        // un dépassement d'un jour reste visible jusqu'à ce que la cible quotidienne le rattrape.
        var usedBeyondPriorDays = weeklyUsedPercent - completedDays * dailyAllocationPercent;
        var percentOfDailyAllocation = Math.Max(0d, usedBeyondPriorDays / dailyAllocationPercent * 100d);

        if (percentOfDailyAllocation < 25d) return new(percentOfDailyAllocation, Color.FromArgb(48, 205, 96), AppText.Get("Pace.Green"));
        if (percentOfDailyAllocation < 50d) return new(percentOfDailyAllocation, Color.FromArgb(255, 220, 32), AppText.Get("Pace.Yellow"));
        if (percentOfDailyAllocation < 75d) return new(percentOfDailyAllocation, Color.FromArgb(255, 149, 24), AppText.Get("Pace.Orange"));
        if (percentOfDailyAllocation <= 100d) return new(percentOfDailyAllocation, Color.FromArgb(242, 63, 63), AppText.Get("Pace.Red"));
        return new(percentOfDailyAllocation, Color.FromArgb(176, 91, 229), AppText.Get("Pace.Purple"));
    }

}

internal static class TrayIcon
{
    /// <summary>Crée l'icône avec les quotas restants et la couleur du rythme quotidien.</summary>
    public static Icon Create(int weeklyRemaining, int fiveHourRemaining, Color dailyDotColor)
    {
        var accentColor = ThemeContrastColor.GetWeeklyRingColor();
        return BuildIcon((graphics, size) =>
            DrawQuotaRings(graphics, size, weeklyRemaining, fiveHourRemaining, accentColor, dailyDotColor, unavailable: false));
    }

    /// <summary>Crée l'icône indiquant que les quotas ne sont pas disponibles.</summary>
    public static Icon CreateUnavailable() => BuildIcon((graphics, size) =>
        DrawQuotaRings(graphics, size, 0, 0, Color.FromArgb(232, 151, 91), Color.Gray, unavailable: true));

    /// <summary>Construit une icône multi-résolution en dessinant chaque image embarquée.</summary>
    private static Icon BuildIcon(Action<Graphics, int> draw)
    {
        // Fournit plusieurs résolutions dans un seul fichier ICO pour que Windows choisisse celle adaptée à l'échelle.
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
        var pngFrames = new List<byte[]>(sizes.Length);
        foreach (var size in sizes)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                draw(graphics, size);
            }
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            pngFrames.Add(png.ToArray());
        }

        using var iconData = new MemoryStream();
        using (var writer = new BinaryWriter(iconData, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);
            var imageOffset = 6 + 16 * sizes.Length;
            for (var i = 0; i < sizes.Length; i++)
            {
                var sizeByte = sizes[i] == 256 ? (byte)0 : (byte)sizes[i];
                writer.Write(sizeByte);
                writer.Write(sizeByte);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)pngFrames[i].Length);
                writer.Write((uint)imageOffset);
                imageOffset += pngFrames[i].Length;
            }
            foreach (var frame in pngFrames) writer.Write(frame);
        }
        iconData.Position = 0;
        return new Icon(iconData);
    }

    /// <summary>Dessine les arcs des quotas restants et le point quotidien sur un fond transparent.</summary>
    private static void DrawQuotaRings(Graphics graphics, int size, int weekly, int fiveHour, Color accentColor, Color dailyDotColor, bool unavailable)
    {
        // L'anneau externe représente les 5 h; l'anneau interne représente la semaine; la partie consommée n'est pas dessinée.
        var scale = size / 32f;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var outer = new RectangleF(3f * scale, 3f * scale, 26f * scale, 26f * scale);
        var inner = new RectangleF(7.8f * scale, 7.8f * scale, 16.4f * scale, 16.4f * scale);

        if (unavailable)
        {
            using var unavailableOuter = new Pen(accentColor, 4.8f * scale);
            using var unavailableInner = new Pen(dailyDotColor, 3.2f * scale);
            graphics.DrawEllipse(unavailableOuter, outer);
            graphics.DrawEllipse(unavailableInner, inner);
            return;
        }

        DrawProgressArc(graphics, outer, 4.8f * scale, fiveHour, accentColor);
        DrawProgressArc(graphics, inner, 3.2f * scale, weekly, dailyDotColor);
        DrawDailyDot(graphics, size, dailyDotColor);
    }

    /// <summary>Dessine au centre de l'icône le point coloré du rythme quotidien.</summary>
    private static void DrawDailyDot(Graphics graphics, int size, Color color)
    {
        var scale = size / 32f;
        var bounds = new RectangleF(11.2f * scale, 11.2f * scale, 9.6f * scale, 9.6f * scale);
        using var whiteOutline = new Pen(Color.FromArgb(245, 255, 255, 255), 1.2f * scale);
        using var darkOutline = new Pen(Color.FromArgb(250, 23, 27, 32), 0.8f * scale);
        graphics.DrawEllipse(whiteOutline, bounds);
        var inner = RectangleF.Inflate(bounds, -0.45f * scale, -0.45f * scale);
        graphics.DrawEllipse(darkOutline, inner);
        var core = RectangleF.Inflate(inner, -0.4f * scale, -0.4f * scale);
        using var fill = new SolidBrush(color);
        graphics.FillEllipse(fill, core);
    }

    /// <summary>Dessine l'arc correspondant au pourcentage de quota restant.</summary>
    private static void DrawProgressArc(Graphics graphics, RectangleF bounds, float width, int remaining, Color color)
    {
        if (remaining <= 0) return;
        using var pen = new Pen(color, width)
        {
            StartCap = LineCap.Round,
            EndCap = remaining == 100 ? LineCap.Flat : LineCap.Round
        };
        if (remaining >= 100)
            graphics.DrawEllipse(pen, bounds);
        else
            graphics.DrawArc(pen, bounds, -90, 360f * Math.Clamp(remaining, 0, 100) / 100f);
    }

}

internal static class ThemeContrastColor
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Chooses a transparent color key close to the Windows theme to limit antialiasing halos.</summary>
    public static Color GetWidgetTransparencyKey()
    {
        var lightBackground = SystemInformation.HighContrast
            ? SystemColors.Control.GetBrightness() > 0.5f
            : IsLightTheme();
        return lightBackground ? Color.FromArgb(254, 255, 254) : Color.FromArgb(1, 0, 1);
    }

    /// <summary>Choisit une couleur d'anneau qui contraste avec la couleur d'accent de Windows.</summary>
    public static Color GetWeeklyRingColor()
    {
        // Utilise la couleur d'accent DWM, tournée sur le cercle chromatique pour contraster avec le fond du thème.
        if (SystemInformation.HighContrast) return SystemColors.Highlight;

        var accent = TryGetDwmColorization() ?? SystemColors.Highlight;
        var isLightTheme = IsLightTheme();
        var lightness = isLightTheme ? 0.42 : 0.68;
        return FromHsl((accent.GetHue() + 180f) % 360f, 0.88f, (float)lightness);
    }

    /// <summary>Chooses readable widget text colors for the current Windows appearance.</summary>
    public static Color GetWidgetTextColor()
    {
        if (SystemInformation.HighContrast) return SystemColors.WindowText;
        return IsLightTheme() ? Color.FromArgb(40, 40, 40) : Color.FromArgb(242, 242, 242);
    }

    /// <summary>Lit la couleur d'accent Windows auprès de DWM, si elle est disponible.</summary>
    private static Color? TryGetDwmColorization()
    {
        try
        {
            if (DwmGetColorizationColor(out var argb, out _) == 0)
                return Color.FromArgb((int)((argb >> 16) & 0xff), (int)((argb >> 8) & 0xff), (int)(argb & 0xff));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return null;
    }

    /// <summary>Détermine si Windows utilise un thème clair à partir du registre et des couleurs système.</summary>
    private static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue("SystemUsesLightTheme") is int value) return value != 0;
            if (key?.GetValue("AppsUseLightTheme") is int appValue) return appValue != 0;
        }
        catch (System.Security.SecurityException) { }
        return SystemColors.Window.GetBrightness() > 0.5f;
    }

    /// <summary>Convertit une couleur HSL en couleur RGB.</summary>
    private static Color FromHsl(float hue, float saturation, float lightness)
    {
        var chroma = (1f - Math.Abs(2f * lightness - 1f)) * saturation;
        var h = hue / 60f;
        var x = chroma * (1f - Math.Abs(h % 2f - 1f));
        var (r, g, b) = h switch
        {
            < 1f => (chroma, x, 0f),
            < 2f => (x, chroma, 0f),
            < 3f => (0f, chroma, x),
            < 4f => (0f, x, chroma),
            < 5f => (x, 0f, chroma),
            _ => (chroma, 0f, x)
        };
        var m = lightness - chroma / 2f;
        return Color.FromArgb(
            (int)Math.Round((r + m) * 255),
            (int)Math.Round((g + m) * 255),
            (int)Math.Round((b + m) * 255));
    }

    /// <summary>Récupère la couleur d'accentuation Windows exposée par le gestionnaire de fenêtres.</summary>
    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmGetColorizationColor(out uint colorizationColor, out bool opaqueBlend);
}
