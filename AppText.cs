using System.Globalization;
using System.Resources;

namespace CodexQuotaTray;

/// <summary>Loads the supported UI language from Windows and falls back to English.</summary>
internal static class AppText
{
    private static readonly ResourceManager Resources = new("CodexQuotaTray.Resources.Strings", typeof(AppText).Assembly);

    public static CultureInfo Culture { get; } = ResolveCulture(CultureInfo.CurrentUICulture);

    public static void Initialize()
    {
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }

    public static string Get(string name) => Resources.GetString(name, Culture) ?? name;

    public static string Format(string name, params object?[] arguments) =>
        string.Format(Culture, Get(name), arguments);

    private static CultureInfo ResolveCulture(CultureInfo windowsUiCulture) =>
        windowsUiCulture.TwoLetterISOLanguageName switch
        {
            "fr" => CultureInfo.GetCultureInfo("fr"),
            "de" => CultureInfo.GetCultureInfo("de"),
            _ => CultureInfo.GetCultureInfo("en")
        };
}
