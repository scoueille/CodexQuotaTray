# Codex Quota Tray

Indicateur de quotas Codex dans la zone de notification Windows. L’application lit le quota avec Codex CLI, actualise l’icône toutes les cinq minutes et affiche les heures de réinitialisation dans son infobulle.

## Lecture de l’icône

- **Anneau extérieur : quota restant sur 5 h.** Sa couleur contraste avec le thème Windows; la piste montre la partie déjà consommée.
- **Anneau intérieur : quota hebdomadaire restant.** Son arc reprend la couleur du point central.
- **Point au centre de l’anneau intérieur : rythme d’usage quotidien.** Il est séparé de l’anneau par un léger espace et compare la consommation hebdomadaire au rythme attendu.

Codex fournit un quota hebdomadaire, pas un compteur journalier. L’application considère donc que l’allocation quotidienne cible vaut **1/7 du quota hebdomadaire**. Elle compare le quota hebdomadaire utilisé à la cible cumulée des journées déjà écoulées; un excès d’utilisation reste ainsi reporté sur les jours suivants jusqu’à ce que la cible le rattrape.

La couleur du point représente la part de l’allocation quotidienne cible consommée : vert sous 25 %, jaune de 25 % à moins de 50 %, orange de 50 % à moins de 75 %, rouge de 75 % à 100 %, et violet au-delà de 100 %.

## Notifications

Windows signale le franchissement des seuils 25 %, 50 %, 75 % et 100 % de l’allocation quotidienne cible, ainsi que le passage sous 50 % ou 25 % de quota restant sur 5 h. La première lecture après le lancement établit la référence sans envoyer de notification rétroactive.

## Utilisation

1. Installer le Codex CLI si nécessaire.
2. Au premier lancement, clic droit sur l’icône puis **Se connecter à Codex CLI…**. Termine la connexion dans la fenêtre qui s’ouvre.
3. L’icône s’actualise automatiquement toutes les cinq minutes. **Actualiser** lance une lecture immédiate. Double-clic sur l’icône ouvre le tableau de bord d’utilisation. Le menu contextuel permet aussi de configurer **Lancer avec Windows** et de quitter l’application.

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
