# Codex Quota Tray

Petit indicateur dans la zone de notification Windows. L’anneau extérieur montre le pourcentage hebdomadaire restant avec une teinte complémentaire à la couleur DWM de Windows; l’anneau intérieur vert montre le pourcentage restant sur la fenêtre de 5 h. Le point en bas à droite indique la part de l’allocation quotidienne consommée : vert (<25 %), jaune (25–50 %), orange (50–75 %), rouge (75–100 %) et violet au-delà de 100 % (allocation dépassée). L’infobulle donne les deux quotas et leurs heures de réinitialisation.

Une notification Windows apparaît lors du franchissement des seuils 25 %, 50 %, 75 % et 100 % de la cible quotidienne, ainsi que lors du passage sous 50 % ou 25 % de quota restant sur 5 h. La première lecture après lancement établit le point de départ sans notification rétroactive.

Le quota accessible est hebdomadaire, sans compteur journalier distinct. La couleur estime donc le rythme en comparant l’usage hebdomadaire cumulé à la cible d’un septième par jour déjà écoulé. Un dépassement reste reporté sur les jours suivants jusqu’à ce que la cible le rattrape. À 100 % de l’allocation du jour, le point est rouge; il devient violet au-delà de 100 %.

## Utilisation

1. Installer le Codex CLI si nécessaire.
2. Au premier lancement, clic droit sur l’icône puis **Se connecter à Codex CLI…**. Termine la connexion dans la fenêtre qui s’ouvre.
3. L’icône se met à jour automatiquement toutes les 5 minutes. **Actualiser** lance une lecture immédiate. Double-clic sur l’icône ouvre le tableau de bord d’utilisation. L’option **Lancer avec Windows** du menu contextuel règle le démarrage automatique.

Le connecteur Codex de cette conversation peut lire le quota avec la session de l’application. Le programme autonome, lui, interroge `codex app-server` et dépend de l’authentification que la CLI voit dans son propre environnement. Il ne lit ni ne copie les jetons. Si son menu indique que la CLI n’est pas connectée, utilise **Se connecter à Codex CLI…**. L’état de connexion observé depuis un environnement isolé ne permet pas de conclure que la CLI lancée directement sur Windows est déconnectée.

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
dotnet build .\installer\CodexQuotaTray.wixproj -c Release
```

Le MSI est produit dans `installer\bin\x64\Release\CodexQuotaTray.msi`. Les exécutables, MSI et autres sorties de compilation sont exclus du dépôt; ils sont générés localement avec ces commandes.

## Note de compatibilité

La lecture utilise le protocole `account/rateLimits/read` de `codex app-server`. Le serveur d’application Codex est actuellement expérimental; une mise à jour du CLI peut donc nécessiter une adaptation.

## Licence

Le code source de ce dépôt est distribué sous licence MIT; voir [LICENSE](LICENSE). Le MSI autonome généré embarque le runtime .NET pour Windows, qui est soumis à ses propres conditions Microsoft ([informations de licence .NET](https://github.com/dotnet/core/blob/main/license-information.md)). Le MSI n’est pas inclus dans le dépôt.
