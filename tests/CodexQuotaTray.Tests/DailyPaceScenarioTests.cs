using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexQuotaTray.Tests;

[TestClass]
public sealed class DailyPaceScenarioTests
{
    private static readonly DateTimeOffset WeekStart = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeekReset = WeekStart.AddDays(7);
    private const int WeeklyWindowMinutes = 7 * 24 * 60;

    /// <summary>Vérifie qu'une forte consommation le premier jour reste visible jusqu'au rattrapage de la cible.</summary>
    [TestMethod]
    public void TwoDailyAllocationsUsedOnFirstDayCarryIntoFollowingDays()
    {
        // 28 % du quota hebdomadaire approchent deux allocations de 1/7 avec la précision entière de Codex.
        var firstDay = PaceOnDay(weeklyUsedPercent: 28, dayIndex: 0, workDays: 7);
        var secondDay = PaceOnDay(weeklyUsedPercent: 28, dayIndex: 1, workDays: 7);
        var thirdDay = PaceOnDay(weeklyUsedPercent: 28, dayIndex: 2, workDays: 7);

        Assert.AreEqual(196d, Math.Round(firstDay.PercentOfDailyAllocation, 6));
        Assert.AreEqual(Color.FromArgb(176, 91, 229), firstDay.Color);
        Assert.AreEqual(96d, Math.Round(secondDay.PercentOfDailyAllocation, 6));
        Assert.AreEqual(Color.FromArgb(242, 63, 63), secondDay.Color);
        Assert.AreEqual(0d, thirdDay.PercentOfDailyAllocation);
        Assert.AreEqual(Color.FromArgb(48, 205, 96), thirdDay.Color);
    }

    /// <summary>Vérifie qu'une journée à environ une allocation et demie laisse un reliquat le lendemain.</summary>
    [TestMethod]
    public void OneAndHalfDailyAllocationsLeaveHalfOfNextDayUsed()
    {
        // 22 % du quota hebdomadaire approchent 1,5/7 avec la précision entière de Codex.
        var firstDay = PaceOnDay(weeklyUsedPercent: 22, dayIndex: 0, workDays: 7);
        var secondDay = PaceOnDay(weeklyUsedPercent: 22, dayIndex: 1, workDays: 7);

        Assert.AreEqual(154d, Math.Round(firstDay.PercentOfDailyAllocation, 6));
        Assert.AreEqual(Color.FromArgb(176, 91, 229), firstDay.Color);
        Assert.AreEqual(54d, Math.Round(secondDay.PercentOfDailyAllocation, 6));
        Assert.AreEqual(Color.FromArgb(255, 149, 24), secondDay.Color);
    }

    /// <summary>Vérifie que le réglage des jours de travail modifie la cible et donc l'indicateur du jour.</summary>
    [TestMethod]
    public void ChoosingFourWorkdaysGivesMoreDailyAllowanceThanSeven()
    {
        var sevenDays = PaceOnDay(weeklyUsedPercent: 10, dayIndex: 0, workDays: 7);
        var fourDays = PaceOnDay(weeklyUsedPercent: 10, dayIndex: 0, workDays: 4);

        Assert.AreEqual(70d, Math.Round(sevenDays.PercentOfDailyAllocation, 6));
        Assert.AreEqual(Color.FromArgb(255, 149, 24), sevenDays.Color);
        Assert.AreEqual(40d, Math.Round(fourDays.PercentOfDailyAllocation, 6));
        Assert.AreEqual(Color.FromArgb(255, 220, 32), fourDays.Color);
    }

    /// <summary>Calcule le rythme au milieu d'un jour donné d'une fenêtre hebdomadaire fixe.</summary>
    private static DailyPaceStatus PaceOnDay(int weeklyUsedPercent, int dayIndex, int workDays) =>
        DailyQuotaPace.Calculate(
            weeklyUsedPercent,
            WeeklyWindowMinutes,
            WeekReset,
            WeekStart.AddDays(dayIndex).AddHours(12),
            workDays);
}
