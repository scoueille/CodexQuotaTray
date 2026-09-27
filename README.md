# Codex Quota Tray

[Français](#français) · [English](#english)

## Français

Indicateur de quotas Codex dans la zone de notification Windows et widget expérimental intégré à la barre des tâches. L’application lit le quota avec Codex CLI et actualise l’affichage toutes les cinq minutes. L’infobulle affiche chaque quota sur sa propre ligne, suivi de son heure de réinitialisation.

L’interface, les infobulles, les notifications et les messages suivent la langue d’affichage de Windows en français, allemand ou anglais. L’anglais est utilisé pour les autres langues.

## Lecture de l’icône

- **Anneau extérieur : quota restant sur 5 h.** Sa couleur contraste avec le thème Windows.
- **Anneau intérieur : quota hebdomadaire restant.** Son arc reprend la couleur du point central.
- **Point au centre de l’anneau intérieur : rythme d’usage quotidien.** Il est séparé de l’anneau par un léger espace et compare la consommation hebdomadaire au rythme attendu.

La partie consommée des deux anneaux est transparente et laisse voir le fond de la barre des tâches.

## Widget de la barre des tâches

Le widget affiche deux lignes : **5 h** avec le quota restant, son pourcentage et l’heure de réinitialisation; puis **7 j** avec le quota restant, son pourcentage et la date/heure de réinitialisation. Le point coloré entouré d’un cercle contrasté reprend le rythme de l’allocation quotidienne. Les deux barres indiquent la part restante de chaque quota avec une couleur choisie pour contraster avec le thème Windows. Le fond du widget est transparent. Le widget est activé par défaut; **Afficher le widget de la barre des tâches** dans le menu de l’icône permet de le masquer ou de le réafficher. La préférence est mémorisée.

Le widget est expérimental. Il se dessine dans la barre principale, à gauche du bouton météo/Widgets lorsqu’il est présent, sinon juste avant la zone de notification. Si Windows refuse cette intégration, l’application utilise une fenêtre superposée. Le widget suit la barre si l’Explorateur Windows redémarre. Windows ne réserve pas automatiquement de place pour ce type d’élément : selon le nombre de boutons affichés, il peut recouvrir une partie de la zone des applications. L’icône de notification reste disponible comme solution de repli. Le mode d’affichage et ses éventuels échecs sont consignés dans `%LOCALAPPDATA%\CodexQuotaTray\TaskbarWidget.log`.

Codex fournit un quota hebdomadaire, pas un compteur journalier. Par défaut, l’application répartit donc ce quota en **7 allocations quotidiennes égales**. Dans **Configuration**, accessible par clic droit sur l’icône, le nombre de jours de travail peut être réglé de 1 à 7; la cible quotidienne devient alors le quota hebdomadaire divisé par ce nombre. Le réglage est mémorisé pour l’utilisateur Windows. L’application compare le quota utilisé à la cible cumulée; un excès reste reporté sur les jours suivants jusqu’à ce que la cible le rattrape.

La couleur du point représente la part de l’allocation quotidienne cible consommée : vert sous 25 %, jaune de 25 % à moins de 50 %, orange de 50 % à moins de 75 %, rouge de 75 % à 100 %, et violet au-delà de 100 %.

## Notifications

Windows signale le franchissement des seuils 25 %, 50 %, 75 % et 100 % de l’allocation quotidienne cible, ainsi que le passage sous 50 % ou 25 % de quota restant sur 5 h. La première lecture après le lancement établit la référence sans envoyer de notification rétroactive. Les demandes et les événements d’affichage sont consignés dans `%LOCALAPPDATA%\CodexQuotaTray\Notifications.log`.

## Utilisation

1. Installer le Codex CLI si nécessaire.
2. Au premier lancement, clic droit sur l’icône puis **Se connecter à Codex CLI…**. Termine la connexion dans la fenêtre qui s’ouvre.
3. Le widget et l’icône s’actualisent automatiquement toutes les cinq minutes. **Actualiser** lance une lecture immédiate. Double-clic sur l’icône ouvre le tableau de bord d’utilisation. Le menu contextuel affiche la version de l’application, permet d’afficher ou masquer le widget, de modifier **Configuration**, de configurer **Lancer avec Windows** et de quitter l’application. Le raccourci créé dans le menu Démarrer utilise une icône dédiée.

Lors d’une mise à niveau, le programme se ferme pour laisser remplacer son exécutable puis se relance automatiquement. Le setup affiche seulement la progression de l’installation.

Le connecteur Codex de cette conversation peut lire le quota avec la session de l’application. Le programme autonome, lui, interroge `codex app-server` et dépend de l’authentification que la CLI voit dans son propre environnement. Il ne lit ni ne copie les jetons. Si son menu indique que la CLI n’est pas connectée, utilise **Se connecter à Codex CLI…**. L’état de connexion observé depuis un environnement isolé ne permet pas de conclure que la CLI lancée directement sur Windows est déconnectée.

## Prérequis développeur

- Windows 10 ou 11, x64.
- Le SDK .NET 10. Visual Studio doit être **Visual Studio 2026 (18.0 ou plus récent)** avec la charge de travail **Développement .NET Desktop**. Visual Studio 2022 ne prend pas en charge le ciblage .NET 10 dans l’IDE. Pour utiliser uniquement la ligne de commande, installe le [SDK .NET 10](https://learn.microsoft.com/dotnet/core/install/windows).
- Pour ouvrir et compiler le projet MSI depuis Visual Studio, installe l’extension gratuite [HeatWave Community pour Visual Studio](https://docs.firegiant.com/heatwave/). Elle prend en charge Visual Studio 2026 et les projets WiX modernes.
- Git est nécessaire pour cloner le dépôt. Le SDK WiX 6.0.2 est restauré automatiquement depuis NuGet par MSBuild; aucune installation WiX séparée n’est requise pour les builds en ligne de commande.
- Codex CLI connecté est requis pour exécuter l’application et lire un quota. Il n’est pas nécessaire pour compiler le code.
- Le projet de tests MSTest figure dans la solution et ses cas se lancent aussi depuis l’**Explorateur de tests** de Visual Studio. Le Codex CLI n’est pas nécessaire pour les tests : ses réponses sont simulées dans les scénarios.
- La première compilation et les tests restaurent leurs dépendances depuis NuGet.org; une connexion Internet est nécessaire tant qu’elles ne sont pas en cache.

## Compilation

Avec le SDK .NET 10 installé sur Windows :

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained false
```

L’exécutable se trouve sous `bin\Release\net10.0-windows\win-x64\publish\CodexQuotaTray.exe`.

## Tests métier

Les tests vérifient des situations d’usage : report d’une consommation de 2/7 ou 1,5/7 sur les jours suivants, effet du réglage des jours de travail, franchissement des seuils de notification et lecture des réponses de Codex. Depuis la racine du dépôt :

```powershell
dotnet test .\tests\CodexQuotaTray.Tests\CodexQuotaTray.Tests.csproj -c Release
```

MSTest et le SDK de test sont des dépendances de développement sous licence MIT; ils ne sont pas inclus dans l’application ni dans le MSI.

## Création du MSI

Le MSI est construit avec le SDK .NET 10 et WiX Toolset 6. Depuis la racine du dépôt, publier d’abord l’application autonome, puis compiler le projet WiX :

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\installer\app-single
dotnet build .\installer\CodexQuotaTrayInstaller.wixproj -c Release
```

Le MSI est produit dans `installer\bin\x64\Release\CodexQuotaTray.msi`. Les exécutables, MSI et autres sorties de compilation sont exclus du dépôt; ils sont générés localement avec ces commandes.

Le projet MSI utilise WiX Toolset 6. Selon la [documentation de WiX](https://docs.firegiant.com/wix/osmf/), son Open Source Maintenance Fee s’applique aux organisations dont les revenus annuels dépassent 10 000 USD, selon la définition de ces conditions.

## Intégration continue

Le workflow GitHub Actions **CI** exécute les tests, puis compile l’application et le MSI sur Windows à chaque push sur `main`, à chaque pull request vers `main` et sur lancement manuel. Le MSI est téléchargeable dans les artefacts de l’exécution sous le nom `CodexQuotaTray-Setup-win-x64`.

## Releases

Pour préparer une version, mettre à jour `Version`, `AssemblyVersion`, `FileVersion` et `InformationalVersion` dans `CodexQuotaTray.csproj`, ainsi que `Version` dans `installer/Product.wxs`. Après avoir poussé le commit et vérifié la CI, créer et pousser un tag correspondant, par exemple :

```powershell
git tag -a v1.1.12 -m "Codex Quota Tray v1.1.12"
git push origin v1.1.12
```

Le workflow **Release** vérifie que le tag et les versions correspondent, exécute les tests, construit le MSI depuis ce tag et crée une Release **en brouillon** avec des notes générées et `CodexQuotaTray-Setup-win-x64.msi`. Vérifier le brouillon et l’installateur dans l’onglet **Releases**, puis publier la Release manuellement.

## Note de compatibilité

La lecture utilise le protocole `account/rateLimits/read` de `codex app-server`. Le serveur d’application Codex est actuellement expérimental; une mise à jour du CLI peut donc nécessiter une adaptation.

## Licence

Le code source de ce dépôt est distribué sous licence MIT; voir [LICENSE](LICENSE). Le MSI autonome généré embarque le runtime .NET pour Windows, qui est soumis à ses propres conditions Microsoft ([informations de licence .NET](https://github.com/dotnet/core/blob/main/license-information.md)). Le MSI n’est pas inclus dans le dépôt.

## English

Codex quota indicator in the Windows notification area, with an experimental widget embedded in the taskbar. The app reads quota data through Codex CLI and refreshes the display every five minutes. The tooltip shows each quota on its own line, followed by its reset time.

The interface, tooltips, notifications, and messages follow the Windows display language when it is French, German, or English. English is used for all other languages.

### Reading the icon

- **Outer ring: quota remaining in the 5-hour window.** Its color contrasts with the Windows theme.
- **Inner ring: weekly quota remaining.** Its arc uses the same color as the center dot.
- **Dot at the center of the inner ring: daily usage pace.** A small gap separates it from the ring. Its color compares weekly usage with the expected pace.

The consumed part of each ring is transparent, allowing the taskbar background to show through.

### Taskbar widget

The widget displays two rows: **5h** with the remaining quota, its percentage, and reset time; then **7d** with the remaining quota, its percentage, and reset date and time. The colored dot with a contrasting outline shows the daily allocation pace. Both bars show how much of each quota remains in a color chosen to contrast with the Windows theme. The widget background is transparent. The widget is enabled by default; choose **Show taskbar widget** from the tray icon menu to hide or show it. The preference is saved.

The widget is experimental. It draws inside the primary taskbar to the left of the weather/Widgets button when present, or just before the notification area otherwise. If Windows rejects this integration, the app uses an overlay window. The widget follows the taskbar if Windows Explorer restarts. Windows does not reserve space automatically for this kind of widget, so it may cover part of the app-button area. The notification-area icon remains available as a fallback. The display mode and any failures are recorded in `%LOCALAPPDATA%\CodexQuotaTray\TaskbarWidget.log`.

Codex provides a weekly quota, not a separate daily counter. By default, the app divides it into **7 equal daily allocations**. In **Settings**, available from the tray icon's right-click menu, the number of workdays can be set from 1 to 7; the daily target then becomes the weekly quota divided by that number. The setting is saved for the current Windows user. The app compares usage with the cumulative target, so any overuse carries forward to later days until the target catches up.

The dot color represents how much of the target daily allocation has been used: green below 25%, yellow from 25% to below 50%, orange from 50% to below 75%, red from 75% through 100%, and purple above 100%.

### Notifications

Windows notifications appear when usage crosses 25%, 50%, 75%, or 100% of the target daily allocation, and when the remaining 5-hour quota drops below 50% or 25%. The first reading after launch establishes a baseline and does not trigger a notification. Requests and display events are recorded in `%LOCALAPPDATA%\CodexQuotaTray\Notifications.log`.

### Usage

1. Install Codex CLI if needed.
2. On first launch, right-click the icon and choose **Se connecter à Codex CLI…** (“Sign in to Codex CLI”). Complete sign-in in the window that opens.
3. The widget and icon refresh automatically every five minutes. Choose **Actualiser** (“Refresh”) for an immediate reading. Double-click the icon to open the usage dashboard. The context menu displays the app version and lets you show or hide the widget, edit **Configuration** (“Settings”), configure **Lancer avec Windows** (“Start with Windows”), or choose **Quitter** (“Quit”). The Start menu shortcut uses a dedicated icon.

During an upgrade, the app closes so its executable can be replaced, then starts again automatically. The setup displays installation progress only.

The Codex connector in this conversation can read quota using the app session. The standalone app instead calls `codex app-server` and relies on the authentication available to the CLI in its own environment. It does not read or copy authentication tokens. If the menu says the CLI is not signed in, choose **Se connecter à Codex CLI…**. An authentication state observed in an isolated environment does not establish whether the CLI running directly on Windows is signed in.

### Developer prerequisites

- Windows 10 or 11, x64.
- The .NET 10 SDK. For Visual Studio, use **Visual Studio 2026 (18.0 or later)** with the **.NET desktop development** workload. Visual Studio 2022 does not support targeting .NET 10 in the IDE. For command-line builds only, install the [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/windows).
- To open and build the MSI project in Visual Studio, install the free [HeatWave Community extension for Visual Studio](https://docs.firegiant.com/heatwave/). It supports Visual Studio 2026 and modern WiX projects.
- Git is needed to clone the repository. MSBuild restores WiX SDK 6.0.2 from NuGet automatically; no separate WiX installation is required for command-line builds.
- A signed-in Codex CLI is required to run the app and read quota. It is not required to compile the code.
- The MSTest project is included in the solution and can also be run from Visual Studio **Test Explorer**. The tests do not require Codex CLI: scenarios use simulated Codex responses.
- The first build and test run restore dependencies from NuGet.org; an Internet connection is needed until they are cached.

### Build

With the .NET 10 SDK installed on Windows:

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained false
```

The executable is written to `bin\Release\net10.0-windows\win-x64\publish\CodexQuotaTray.exe`.

### Business scenario tests

The tests cover user scenarios: carrying 2/7 or 1.5/7 of weekly usage into later days, changing the workday setting, crossing notification thresholds, and reading Codex responses. From the repository root:

```powershell
dotnet test .\tests\CodexQuotaTray.Tests\CodexQuotaTray.Tests.csproj -c Release
```

MSTest and the test SDK are MIT-licensed development dependencies; they are not included in the app or MSI.

### Build the MSI

The MSI is built with the .NET 10 SDK and WiX Toolset 6. From the repository root, publish the self-contained app first, then build the WiX project:

```powershell
dotnet publish .\CodexQuotaTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\installer\app-single
dotnet build .\installer\CodexQuotaTrayInstaller.wixproj -c Release
```

The MSI is created at `installer\bin\x64\Release\CodexQuotaTray.msi`. Executables, MSI files, and other build outputs are excluded from the repository and generated locally by these commands.

The MSI project uses WiX Toolset 6. According to the [WiX documentation](https://docs.firegiant.com/wix/osmf/), its Open Source Maintenance Fee applies to organizations with more than USD 10,000 in annual revenue, as defined by those terms.

### Continuous integration

The GitHub Actions **CI** workflow runs the tests, then builds the app and MSI on Windows for every push to `main`, every pull request targeting `main`, and manual runs. Download the MSI from the run’s artifacts as `CodexQuotaTray-Setup-win-x64`.

### Releases

To prepare a version, update `Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` in `CodexQuotaTray.csproj`, plus `Version` in `installer/Product.wxs`. Push the commit and check CI, then create and push the matching tag, for example:

```powershell
git tag -a v1.1.12 -m "Codex Quota Tray v1.1.12"
git push origin v1.1.12
```

The **Release** workflow checks the tag against the project versions, runs the tests, builds the MSI from that tag, and creates a **draft** Release with generated notes and `CodexQuotaTray-Setup-win-x64.msi`. Review the draft and installer in **Releases**, then publish the Release manually.

### Compatibility

Quota reading uses the `account/rateLimits/read` protocol from `codex app-server`. The Codex app server is currently experimental, so a CLI update may require changes.

### License

The source code in this repository is distributed under the MIT License; see [LICENSE](LICENSE). The generated self-contained MSI bundles the .NET runtime for Windows, which is subject to separate Microsoft terms ([.NET licensing information](https://github.com/dotnet/core/blob/main/license-information.md)). The MSI is not included in the repository.
