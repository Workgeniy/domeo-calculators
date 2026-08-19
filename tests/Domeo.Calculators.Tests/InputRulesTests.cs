using Domeo.Calculators.Electrical;
using Domeo.Calculators.Lighting;
using Xunit;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Правила ввода из обработчиков onChange оригинала. Расчёт их не применяет —
/// они действуют на границе, поэтому проверяются отдельно от golden-векторов.
/// </summary>
public class InputRulesTests
{
    private static LightingInput Base() =>
        new(Area: 18, Height: 2.7, RoomType: "living", WallColor: "light",
            LampType: "led", FixtureWatt: 12, HoursPerDay: 6, Tariff: 6.5);

    [Theory]
    [InlineData(25, 24)]     // max(0, min(24, x)) — единственное поле с потолком
    [InlineData(100, 24)]
    [InlineData(24, 24)]
    [InlineData(-5, 0)]
    [InlineData(6, 6)]
    public void Часы_ограничены_сверху_сутками(int given, int expected)
    {
        Assert.Equal(expected, LightingInputRules.Normalize(Base() with { HoursPerDay = given }).HoursPerDay);
    }

    [Fact]
    public void Отрицательные_значения_поднимаются_до_минимума()
    {
        LightingInput r = LightingInputRules.Normalize(
            Base() with { Area = -10, Height = -1, Tariff = -3, FixtureWatt = 0 });

        Assert.Equal(0, r.Area);
        Assert.Equal(0, r.Height);
        Assert.Equal(0, r.Tariff);
        Assert.Equal(1, r.FixtureWatt);   // мощность в оригинале не ниже 1
    }

    [Fact]
    public void Площадь_и_высота_сверху_не_ограничены()
    {
        // В разметке стоит max="500", но обработчик его не применяет.
        LightingInput r = LightingInputRules.Normalize(Base() with { Area = 9000, Height = 40 });

        Assert.Equal(9000, r.Area);
        Assert.Equal(40, r.Height);
    }

    [Fact]
    public void Площадь_комнаты_округляется_вниз_до_целого()
    {
        // Оригинал читает поле через parseInt, поэтому дробная часть отбрасывается.
        RoomState r = ElectricalInputRules.Normalize(Room() with { Area = 12.9 });

        Assert.Equal(12, r.Area);
    }

    [Fact]
    public void Площадь_и_количество_комнат_не_ниже_единицы()
    {
        RoomState r = ElectricalInputRules.Normalize(Room() with { Area = 0, Count = 0 });

        Assert.Equal(1, r.Area);
        Assert.Equal(1, r.Count);
    }

    [Fact]
    public void Количество_точек_не_отрицательное()
    {
        RoomState r = ElectricalInputRules.Normalize(
            Room() with { Switches = -2, Passthrough = -1, Usb = -3, Tv = -1, Internet = -1 });

        Assert.Equal(0, r.Switches);
        Assert.Equal(0, r.Passthrough);
        Assert.Equal(0, r.Usb);
        Assert.Equal(0, r.Tv);
        Assert.Equal(0, r.Internet);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(50, 20)]     // ползунок в оригинале ограничен двадцатью
    [InlineData(10, 10)]
    public void Розетки_ограничены_диапазоном_ползунка(int given, int expected)
    {
        Assert.Equal(expected, ElectricalInputRules.Normalize(Room() with { Outlets = given }).Outlets);
    }

    [Fact]
    public void Нормализация_словаря_не_теряет_комнаты()
    {
        IReadOnlyDictionary<string, RoomState> normalized =
            ElectricalInputRules.Normalize(ElectricalCalculator.DefaultRooms());

        Assert.Equal(7, normalized.Count);
        Assert.True(normalized.ContainsKey("kitchen"));
    }

    private static RoomState Room() =>
        new(Enabled: true, Area: 10, Count: 1, Outlets: 10,
            Switches: 2, Passthrough: 0, Usb: 2, Tv: 1, Internet: 0);
}
