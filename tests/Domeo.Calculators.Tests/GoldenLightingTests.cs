using Domeo.Calculators.Lighting;
using Xunit;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Сверка порта с эталоном, снятым с оригинального кода calcal.ru.
/// </summary>
public class GoldenLightingTests
{
    public static IEnumerable<object[]> Cases => GoldenCases.Load<LightingCase>("lighting.json");

    [Theory]
    [MemberData(nameof(Cases))]
    internal void Совпадает_с_оригиналом(LightingCase testCase)
    {
        LightingCaseInput i = testCase.Input;

        LightingResult? actual = LightingCalculator.Calculate(new LightingInput(
            i.Area, i.Height, i.RoomType, i.WallColor, i.LampType, i.FixtureWatt, i.HoursPerDay, i.Tariff));

        if (testCase.Expected is null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        LightingExpected e = testCase.Expected;

        Assert.Equal(e.RequiredLux, actual.RequiredLux);
        Assert.Equal(e.RequiredLumens, actual.RequiredLumens);
        Assert.Equal(e.FixtureLumen, actual.FixtureLumen);
        Assert.Equal(e.FixtureCount, actual.FixtureCount);
        Assert.Equal(e.FixtureWatt, actual.FixtureWatt);
        Assert.Equal(e.TotalWatt, actual.TotalWatt);
        Assert.Equal(e.HeightCoeff, actual.HeightCoeff);
        Assert.Equal(e.RoomName, actual.RoomName);

        // Именно эти две строки видит пользователь — сверяем посимвольно.
        Assert.Equal(e.MonthlyKwh, actual.MonthlyKwhFormatted);
        Assert.Equal(e.MonthlyCost, actual.MonthlyCostFormatted);
    }
}
