using System.Globalization;
using System.Resources;

namespace CodexQuotaTray;

/// <summary>Loads the supported UI language from Windows and falls back to English.</summary>
internal static class AppText
{
    private static readonly ResourceManager Resources = new("CodexQuotaTray.Resources.Strings", typeof(AppText).Assembly);

    /// <summary>Langue prise en charge choisie à partir de la langue de Windows.</summary>
    public static CultureInfo Culture { get; } = ResolveCulture(CultureInfo.CurrentUICulture);

    /// <summary>Applique la langue choisie aux chaînes affichées par l'application.</summary>
    public static void Initialize()
    {
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }

    /// <summary>Renvoie une chaîne traduite, ou son nom si la ressource est absente.</summary>
    public static string Get(string name) => Resources.GetString(name, Culture) ?? name;

    /// <summary>Formate une chaîne traduite avec les arguments fournis.</summary>
    public static string Format(string name, params object?[] arguments) =>
        string.Format(Culture, Get(name), arguments);

    /// <summary>Choisit le français ou l'allemand selon Windows, avec l'anglais en secours.</summary>
    private static CultureInfo ResolveCulture(CultureInfo windowsUiCulture) =>
        windowsUiCulture.TwoLetterISOLanguageName switch
        {
            "fr" => CultureInfo.GetCultureInfo("fr"),
            "de" => CultureInfo.GetCultureInfo("de"),
            _ => CultureInfo.GetCultureInfo("en")
        };
}
