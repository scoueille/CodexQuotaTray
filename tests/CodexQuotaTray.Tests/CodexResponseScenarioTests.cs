using System.Drawing;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexQuotaTray.Tests;

[TestClass]
public sealed class CodexResponseScenarioTests
{
    /// <summary>Vérifie que les quotas utilisés renvoyés par Codex deviennent les valeurs restantes affichées.</summary>
    [TestMethod]
    public void CodexUsageResponseProvidesRemainingQuotasAndResetTimes()
    {
        using var response = JsonDocument.Parse("""
            {"result":{"rateLimits":{
              "primary":{"usedPercent":60,"resetsAt":1800000000},
              "secondary":{"usedPercent":95,"windowDurationMins":10080,"resetsAt":1800500000}
            }}}
            """);

        var quota = CodexCli.ParseQuota(response.RootElement);

        Assert.AreEqual(40, quota.FiveHourRemaining);
        Assert.AreEqual(5, quota.WeeklyRemaining);
        Assert.AreEqual(95, quota.WeeklyUsedPercent);
        Assert.AreEqual(10080L, quota.WeeklyWindowDurationMinutes);
        Assert.AreEqual(1800000000L, quota.FiveHourReset.ToUnixTimeSeconds());
        Assert.AreEqual(1800500000L, quota.WeeklyReset.ToUnixTimeSeconds());
    }

    /// <summary>Vérifie que des quotas lisibles restent affichés même sans durée permettant le rythme quotidien.</summary>
    [TestMethod]
    public void MissingWeeklyWindowDurationKeepsQuotasButMakesDailyPaceUnavailable()
    {
        using var response = JsonDocument.Parse("""
            {"result":{"rateLimits":{
              "primary":{"usedPercent":20,"resetsAt":1800000000},
              "secondary":{"usedPercent":30,"resetsAt":1800500000}
            }}}
            """);

        var quota = CodexCli.ParseQuota(response.RootElement);
        var dailyPace = DailyQuotaPace.Calculate(
            quota.WeeklyUsedPercent,
            quota.WeeklyWindowDurationMinutes,
            quota.WeeklyReset,
            DateTimeOffset.FromUnixTimeSeconds(1800000000),
            workDaysPerWeek: 7);

        Assert.AreEqual(80, quota.FiveHourRemaining);
        Assert.AreEqual(70, quota.WeeklyRemaining);
        Assert.IsNull(quota.WeeklyWindowDurationMinutes);
        Assert.AreEqual(Color.Gray, dailyPace.Color);
        Assert.AreEqual(AppText.Get("Pace.Unavailable"), dailyPace.Label);
    }

    /// <summary>Vérifie que la demande d'authentification de Codex devient un message de connexion compréhensible.</summary>
    [TestMethod]
    public void CodexAuthenticationErrorAsksUserToSignIn()
    {
        using var response = JsonDocument.Parse("""
            {"error":{"message":"authentication required"}}
            """);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => CodexCli.ParseQuota(response.RootElement));

        Assert.AreEqual(AppText.Get("Error.CliNotConnected"), error.Message);
    }

    /// <summary>Vérifie qu'une réponse sans quota principal signale l'indisponibilité des données.</summary>
    [TestMethod]
    public void MissingPrimaryQuotaShowsAnUnavailableMessage()
    {
        using var response = JsonDocument.Parse("""
            {"result":{"rateLimits":{"primary":null,"secondary":{"usedPercent":10}}}}
            """);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => CodexCli.ParseQuota(response.RootElement));

        Assert.AreEqual(AppText.Get("Error.PrimaryQuotaMissing"), error.Message);
    }
}
