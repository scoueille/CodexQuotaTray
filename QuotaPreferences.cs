using Microsoft.Win32;

namespace CodexQuotaTray;

/// <summary>Persists the user's daily quota allocation preference for this Windows account.</summary>
internal static class QuotaPreferences
{
    private const string RegistryPath = @"Software\CodexQuotaTray\Settings";
    private const string WorkDaysValueName = "WorkDaysPerWeek";
    private const string TaskbarWidgetVisibleValueName = "TaskbarWidgetVisible";

    /// <summary>Nombre de jours sur lesquels répartir l'allocation hebdomadaire.</summary>
    public static int WorkDaysPerWeek { get; private set; } = 7;

    /// <summary>Indicates whether the taskbar widget should appear when the application starts.</summary>
    public static bool TaskbarWidgetVisible { get; private set; } = true;

    /// <summary>Charge la préférence depuis le registre et conserve 7 en cas d'absence ou d'erreur.</summary>
    public static void Load()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            if (key?.GetValue(WorkDaysValueName) is int value)
                WorkDaysPerWeek = Math.Clamp(value, 1, 7);
            if (key?.GetValue(TaskbarWidgetVisibleValueName) is int widgetValue)
                TaskbarWidgetVisible = widgetValue != 0;
        }
        catch (System.Security.SecurityException)
        {
            // Conserve la valeur par défaut si Windows ne permet pas de lire les préférences.
        }
        catch (IOException)
        {
            // Une préférence illisible ne doit pas empêcher l'application de démarrer.
        }
    }

    /// <summary>Enregistre le nombre de jours choisi, limité à une valeur de 1 à 7.</summary>
    public static void Save(int workDaysPerWeek)
    {
        var value = Math.Clamp(workDaysPerWeek, 1, 7);
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException(AppText.Get("Error.SettingsKeyUnavailable"));
        key.SetValue(WorkDaysValueName, value, RegistryValueKind.DWord);
        WorkDaysPerWeek = value;
    }

    /// <summary>Saves whether the taskbar widget is shown for this Windows user.</summary>
    public static void SaveTaskbarWidgetVisible(bool visible)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException(AppText.Get("Error.SettingsKeyUnavailable"));
        key.SetValue(TaskbarWidgetVisibleValueName, visible ? 1 : 0, RegistryValueKind.DWord);
        TaskbarWidgetVisible = visible;
    }
}
