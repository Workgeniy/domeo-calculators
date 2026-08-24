using Domeo.Calculators.Spec;
using Xunit;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Сверка порта с исходной таблицей. Ожидания — значения, посчитанные самим Excel
/// и сохранённые в файле; профиль <c>excel</c> обязан повторять их до штуки.
/// </summary>
public class GoldenSpecTests
{
    private static readonly SpecGolden Golden = GoldenCases.LoadDocument<SpecGolden>("spec.json");

    public static IEnumerable<object[]> Cases => Golden.Cases.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cases))]
    internal void Совпадает_с_таблицей(SpecGoldenCase testCase)
    {
        SpecResult result = SpecCalculator.Calculate([testCase.Input], "excel");

        Assert.Equal(testCase.Expected, Assert.Single(result.Rooms).Counts);
    }

    [Fact]
    public void Итог_по_квартире_совпадает_с_таблицей()
    {
        SpecResult result = SpecCalculator.Calculate(Golden.Cases.Select(c => c.Input), "excel");

        Assert.Equal(Golden.Total, result.Total);
    }

    [Fact]
    public void Разбивка_идёт_в_порядке_входа()
    {
        SpecResult result = SpecCalculator.Calculate(Golden.Cases.Select(c => c.Input), "excel");

        Assert.Equal(
            Golden.Cases.Select(c => c.Input.RoomType),
            result.Rooms.Select(r => r.RoomType));
    }
}
