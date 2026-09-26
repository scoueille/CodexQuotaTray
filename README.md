# Codex Quota Tray

[Français](#français) · [English](#english)

## Français

Indicateur de quotas Codex dans la zone de notification Windows. L’application lit le quota avec Codex CLI, actualise l’icône toutes les cinq minutes et affiche les heures de réinitialisation dans son infobulle.

L’interface, les infobulles, les notifications et les messages suivent la langue d’affichage de Windows en français, allemand ou anglais. L’anglais est utilisé pour les autres langues.

## Lecture de l’icône

- **Anneau extérieur : quota restant sur 5 h.** Sa couleur contraste avec le thème Windows; la piste montre la partie déjà consommée.
- **Anneau intérieur : quota hebdomadaire restant.** Son arc reprend la couleur du point central.
- **Point au centre de l’anneau intérieur : rythme d’usage quotidien.** Il est séparé de l’anneau par un léger espace et compare la consommation hebdomadaire au rythme attendu.

Codex fournit un quota hebdomadaire, pas un compteur journalier. Par défaut, l’application répartit donc ce quota en **7 allocations quotidiennes égales**. Dans **Configuration**, accessible par clic droit sur l’icône, le nombre de jours de travail peut être réglé de 1 à 7; la cible quotidienne devient alors le quota hebdomadaire divisé par ce nombre. Le réglage est mémorisé pour l’utilisateur Windows. L’application compare le quota utilisé à la cible cumulée; un excès reste reporté sur les jours suivants jusqu’à ce que la cible le rattrape.

La couleur du point représente la part de l’allocation quotidienne cible consommée : vert sous 25 %, jaune de 25 % à moins de 50 %, orange de 50 % à moins de 75 %, rouge de 75 % à 100 %, et violet au-delà de 100 %.

## Notifications

Windows signale le franchissement des seuils 25 %, 50 %, 75 % et 100 % de l’allocation quotidienne cible, ainsi que le passage sous 50 % ou 25 % de quota restant sur 5 h. La première lecture après le lancement établit la référence sans envoyer de notification rétroactive.

## Utilisation

1. Installer le Codex CLI si nécessaire.
2. Au premier lancement, clic droit sur l’icône puis **Se connecter à Codex CLI…**. Termine la connexion dans la fenêtre qui s’ouvre.
3. L’icône s’actualise automatiquement toutes les cinq minutes. **Actualiser** lance une lecture immédiate. Double-clic sur l’icône ouvre le tableau de bord d’utilisation. Le menu contextuel affiche la version de l’application, permet de modifier **Configuration**, de configurer **Lancer avec Windows** et de quitter l’application. Le raccourci créé dans le menu Démarrer utilise une icône dédiée.

Lors d’une mise à niveau, le programme se ferme pour laisser remplacer son exécutable puis se relance automatiquement. Le setup affiche seulement la progression de l’installation.

Le connecteur Codex de cette conversation peut lire le quota avec la session de l’application. Le programme autonome, lui, interroge `codex app-server` et dépend de l’authentification que la CLI voit dans son propre environnement. Il ne lit ni ne copie les jetons. Si son menu indique que la CLI n’est pas connectée, utilise **Se connecter à Codex CLI…**. L’état de connexion observé depuis un environnement isolé ne permet pas de conclure que la CLI lancée directement sur Windows est déconnectée.

## Prérequis développeur

- Windows 10 ou 11, x64.
- Le SDK .NET 10. Visual Studio doit être **Visual Studio 2026 (18.0 ou plus récent)** avec la charge de travail **Développement .NET Desktop**. Visual Studio 2022 ne prend pas en charge le ciblage .NET 10 dans l’IDE. Pour utiliser uniquement la ligne de commande, installe le [SDK .NET 10](https://learn.microsoft.com/dotnet/core/install/windows).
- Pour ouvrir et compiler le projet MSI depuis Visual Studio, installe l’extension gratuite [HeatWave Community pour Visual Studio](https://docs.firegiant.com/heatwave/). Elle prend en charge Visual Studio 2026 et les projets WiX modernes.
- Git est nécessaire pour cloner le dépôt. Le SDK WiX 6.0.2 est restauré automatiquement depuis NuGet par MSBuild; aucune installation WiX séparée n’est requise pour les builds en ligne de commande.
- Codex CLI connecté est requis pour exécuter l’application et lire un quota. Il n’est pas nécessaire pour compiler le code.

## Compilation

Avec le SDK .NET 10 installé sur Windows :

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained false
```

L’exécutable se trouve sous `bin\Release\net10.0-windows\win-x64\publish\CodexQuotaTray.exe`.

## Création du MSI

Le MSI est construit avec le SDK .NET 10 et WiX Toolset 6. Depuis la racine du dépôt, publier d’abord l’application autonome, puis compiler le projet WiX :

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\installer\app-single
dotnet build .\installer\CodexQuotaTrayInstaller.wixproj -c Release
```

Le MSI est produit dans `installer\bin\x64\Release\CodexQuotaTray.msi`. Les exécutables, MSI et autres sorties de compilation sont exclus du dépôt; ils sont générés localement avec ces commandes.

WiX Toolset 6 est soumis à l’[Open Source Maintenance Fee](https://docs.firegiant.com/wix/osmf/) si son utilisation génère des revenus; consulte ses conditions si tu distribues le MSI commercialement.

## Note de compatibilité

La lecture utilise le protocole `account/rateLimits/read` de `codex app-server`. Le serveur d’application Codex est actuellement expérimental; une mise à jour du CLI peut donc nécessiter une adaptation.

## Licence

Le code source de ce dépôt est distribué sous licence MIT; voir [LICENSE](LICENSE). Le MSI autonome généré embarque le runtime .NET pour Windows, qui est soumis à ses propres conditions Microsoft ([informations de licence .NET](https://github.com/dotnet/core/blob/main/license-information.md)). Le MSI n’est pas inclus dans le dépôt.

## English

Codex quota indicator in the Windows notification area. The app reads quota data through Codex CLI, refreshes the icon every five minutes, and shows reset times in its tooltip.

The interface, tooltips, notifications, and messages follow the Windows display language when it is French, German, or English. English is used for all other languages.

### Reading the icon

- **Outer ring: quota remaining in the 5-hour window.** Its color contrasts with the Windows theme; the track shows the consumed portion.
- **Inner ring: weekly quota remaining.** Its arc uses the same color as the center dot.
- **Dot at the center of the inner ring: daily usage pace.** A small gap separates it from the ring. Its color compares weekly usage with the expected pace.

Codex provides a weekly quota, not a separate daily counter. By default, the app divides it into **7 equal daily allocations**. In **Settings**, available from the tray icon's right-click menu, the number of workdays can be set from 1 to 7; the daily target then becomes the weekly quota divided by that number. The setting is saved for the current Windows user. The app compares usage with the cumulative target, so any overuse carries forward to later days until the target catches up.

The dot color represents how much of the target daily allocation has been used: green below 25%, yellow from 25% to below 50%, orange from 50% to below 75%, red from 75% through 100%, and purple above 100%.

### Notifications

Windows notifications appear when usage crosses 25%, 50%, 75%, or 100% of the target daily allocation, and when the remaining 5-hour quota drops below 50% or 25%. The first reading after launch establishes a baseline and does not trigger a notification.

### Usage

1. Install Codex CLI if needed.
2. On first launch, right-click the icon and choose **Se connecter à Codex CLI…** (“Sign in to Codex CLI”). Complete sign-in in the window that opens.
3. The icon refreshes automatically every five minutes. Choose **Actualiser** (“Refresh”) for an immediate reading. Double-click the icon to open the usage dashboard. The context menu displays the app version and lets you edit **Configuration** (“Settings”), configure **Lancer avec Windows** (“Start with Windows”), or choose **Quitter** (“Quit”). The Start menu shortcut uses a dedicated icon.

During an upgrade, the app closes so its executable can be replaced, then starts again automatically. The setup displays installation progress only.

The Codex connector in this conversation can read quota using the app session. The standalone app instead calls `codex app-server` and relies on the authentication available to the CLI in its own environment. It does not read or copy authentication tokens. If the menu says the CLI is not signed in, choose **Se connecter à Codex CLI…**. An authentication state observed in an isolated environment does not establish whether the CLI running directly on Windows is signed in.

### Developer prerequisites

- Windows 10 or 11, x64.
- The .NET 10 SDK. For Visual Studio, use **Visual Studio 2026 (18.0 or later)** with the **.NET desktop development** workload. Visual Studio 2022 does not support targeting .NET 10 in the IDE. For command-line builds only, install the [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/windows).
- To open and build the MSI project in Visual Studio, install the free [HeatWave Community extension for Visual Studio](https://docs.firegiant.com/heatwave/). It supports Visual Studio 2026 and modern WiX projects.
- Git is needed to clone the repository. MSBuild restores WiX SDK 6.0.2 from NuGet automatically; no separate WiX installation is required for command-line builds.
- A signed-in Codex CLI is required to run the app and read quota. It is not required to compile the code.

### Build

With the .NET 10 SDK installed on Windows:

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained false
```

The executable is written to `bin\Release\net10.0-windows\win-x64\publish\CodexQuotaTray.exe`.

### Build the MSI

The MSI is built with the .NET 10 SDK and WiX Toolset 6. From the repository root, publish the self-contained app first, then build the WiX project:

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\installer\app-single
dotnet build .\installer\CodexQuotaTrayInstaller.wixproj -c Release
```

The MSI is created at `installer\bin\x64\Release\CodexQuotaTray.msi`. Executables, MSI files, and other build outputs are excluded from the repository and generated locally by these commands.

WiX Toolset 6 is subject to the [Open Source Maintenance Fee](https://docs.firegiant.com/wix/osmf/) when its use generates revenue; review its terms if you distribute the MSI commercially.

### Compatibility

Quota reading uses the `account/rateLimits/read` protocol from `codex app-server`. The Codex app server is currently experimental, so a CLI update may require changes.

### License

The source code in this repository is distributed under the MIT License; see [LICENSE](LICENSE). The generated self-contained MSI bundles the .NET runtime for Windows, which is subject to separate Microsoft terms ([.NET licensing information](https://github.com/dotnet/core/blob/main/license-information.md)). The MSI is not included in the repository.
