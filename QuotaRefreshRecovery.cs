namespace CodexQuotaTray;

/// <summary>Recovers from a moved CLI or interrupted app-server without retrying account or protocol errors.</summary>
internal static class QuotaRefreshRecovery
{
    /// <summary>Discovers the CLI for every attempt and retries a technical failure once after a short update grace period.</summary>
    internal static async Task<Quota> ReadWithRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var path = CodexCli.FindPath()
                    ?? throw new InvalidOperationException(AppText.Get("Error.CliNotFound"));
                var quota = await CodexCli.ReadQuotaAsync(path, cancellationToken);
                if (attempt > 0) Log("quota refresh recovered on retry");
                return quota;
            }
            catch (Exception error) when (attempt == 0 && IsTransient(error) && !cancellationToken.IsCancellationRequested)
            {
                Log($"technical failure; rediscovering CLI and retrying once: {error.GetType().Name}: {error.Message}");
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    /// <summary>Restricts automatic recovery to transport, timeout, and executable-launch failures.</summary>
    internal static bool IsTransient(Exception error) =>
        error is IOException or TimeoutException or System.ComponentModel.Win32Exception;

    /// <summary>Records recovery attempts without allowing a logging failure to interrupt quota updates.</summary>
    internal static void Log(string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexQuotaTray");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "QuotaRefresh.log"),
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { /* Diagnostics must not prevent quota recovery. */ }
    }
}
