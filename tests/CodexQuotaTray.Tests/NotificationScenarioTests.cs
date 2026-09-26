using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexQuotaTray.Tests;

[TestClass]
public sealed class NotificationScenarioTests
{
    /// <summary>Vérifie qu'ouvrir l'application après plusieurs seuils ne déclenche pas d'alertes anciennes.</summary>
    [TestMethod]
    public void StartingWithQuotaAlreadyConsumedDoesNotNotifyRetroactively()
    {
        var notifier = new QuotaNotifier();

        Assert.IsNull(notifier.GetCrossings(dailyPace: 80, fiveHourRemaining: 20));
        Assert.IsNull(notifier.GetCrossings(dailyPace: 80, fiveHourRemaining: 20));
    }

    /// <summary>Vérifie qu'une seule actualisation regroupe les seuils quotidiens franchis sans les répéter.</summary>
    [TestMethod]
    public void OneRefreshCrossingSeveralDailyThresholdsProducesOneAlert()
    {
        var notifier = new QuotaNotifier();
        notifier.GetCrossings(dailyPace: 10, fiveHourRemaining: 90);

        var alert = notifier.GetCrossings(dailyPace: 80, fiveHourRemaining: 90);

        Assert.IsNotNull(alert);
        StringAssert.Contains(alert.Message, "25");
        StringAssert.Contains(alert.Message, "50");
        StringAssert.Contains(alert.Message, "75");
        Assert.IsNull(notifier.GetCrossings(dailyPace: 80, fiveHourRemaining: 90));
    }

    /// <summary>Vérifie que les alertes de cinq heures apparaissent sous 50 % et sous 25 % restants.</summary>
    [TestMethod]
    public void FiveHourAlertsFireOnlyAfterRemainingQuotaDropsBelowEachLimit()
    {
        var notifier = new QuotaNotifier();
        notifier.GetCrossings(dailyPace: 0, fiveHourRemaining: 50);

        Assert.IsNull(notifier.GetCrossings(dailyPace: 0, fiveHourRemaining: 50));
        var belowFifty = notifier.GetCrossings(dailyPace: 0, fiveHourRemaining: 49);
        Assert.IsNotNull(belowFifty);
        StringAssert.Contains(belowFifty.Message, "50");

        Assert.IsNull(notifier.GetCrossings(dailyPace: 0, fiveHourRemaining: 25));
        var belowTwentyFive = notifier.GetCrossings(dailyPace: 0, fiveHourRemaining: 24);
        Assert.IsNotNull(belowTwentyFive);
        StringAssert.Contains(belowTwentyFive.Message, "25");
    }

    /// <summary>Vérifie qu'un changement de configuration repart d'une nouvelle lecture sans alerte rétroactive.</summary>
    [TestMethod]
    public void ChangingWorkdaySettingStartsAQuietNotificationBaseline()
    {
        var notifier = new QuotaNotifier();
        notifier.GetCrossings(dailyPace: 10, fiveHourRemaining: 100);
        notifier.ResetBaseline();

        Assert.IsNull(notifier.GetCrossings(dailyPace: 80, fiveHourRemaining: 20));
        var newUsage = notifier.GetCrossings(dailyPace: 101, fiveHourRemaining: 20);
        Assert.IsNotNull(newUsage);
        StringAssert.Contains(newUsage.Message, "100");
    }

    /// <summary>Vérifie qu'une actualisation peut signaler ensemble le rythme quotidien et le quota de cinq heures.</summary>
    [TestMethod]
    public void DailyAndFiveHourThresholdsCrossedTogetherShareOneNotification()
    {
        var notifier = new QuotaNotifier();
        notifier.GetCrossings(dailyPace: 20, fiveHourRemaining: 51);

        var alert = notifier.GetCrossings(dailyPace: 30, fiveHourRemaining: 49);

        Assert.IsNotNull(alert);
        StringAssert.Contains(alert.Message, "25");
        StringAssert.Contains(alert.Message, "50");
    }
}
