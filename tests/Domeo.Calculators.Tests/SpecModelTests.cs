using Domeo.Calculators.Reference;
using Domeo.Calculators.Spec;
using Xunit;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Отличия рабочей модели (профиль domeo) от исходной таблицы. Каждый тест — одно
/// осознанное расхождение из docs/calc-spec.md; всё остальное считается одинаково.
/// </summary>
public class SpecModelTests
{
    private static SpecCounts Calc(SpecRoomInput room, string profile) =>
        SpecCalculator.Calculate([room], profile).Rooms[0].Counts;

    private static SpecRoomInput Room(string type, double area) =>
        new() { RoomType = type, Area = area, Comfort = ComfortLevel.Standard };

    [Fact]
    public void Ванная_получает_влагозащищённые_розетки()
    {
        SpecRoomInput room = Room("bathroom", 4.6);

        // В таблице условие сравнивает тип помещения со строкой «Ванная», а список
        // предлагает «Ванная комната» — совпадения не бывает никогда.
        Assert.Equal(0, Calc(room, "excel").WetSockets);
        Assert.Equal(1, Calc(room, "domeo").WetSockets);
    }

    [Fact]
    public void Телевизор_даёт_одну_антенную_розетку()
    {
        SpecRoomInput room = Room("living", 13.3) with { Tv = 1 };

        // 1 * 2 + 1: множитель «розеток на ТВ-зону» уехал в антенные розетки.
        Assert.Equal(3, Calc(room, "excel").TvSockets);
        Assert.Equal(1, Calc(room, "domeo").TvSockets);
    }

    [Fact]
    public void Силовые_розетки_телевизора_остаются_на_месте()
    {
        SpecRoomInput room = Room("living", 10) with { Tv = 1 };

        // Питание телевизора и приставки — две обычные розетки; это не менялось.
        Assert.Equal(4, Calc(room, "domeo").NormalSockets);   // 10/5 + 2
    }

    [Fact]
    public void Перекрёстный_появляется_там_где_три_группы_света()
    {
        Assert.Equal(0, Calc(Room("kitchenLiving", 20), "excel").CrossSwitches);
        Assert.Equal(1, Calc(Room("kitchenLiving", 20), "domeo").CrossSwitches);
        Assert.Equal(0, Calc(Room("bedroom", 20), "domeo").CrossSwitches);
    }

    [Fact]
    public void Базовые_розетки_считаются_по_типу_помещения()
    {
        // Таблица делит площадь на 5 в любом помещении, справочник — на 6 в прихожей.
        Assert.Equal(3, Calc(Room("hallway", 12), "excel").NormalSockets);
        Assert.Equal(2, Calc(Room("hallway", 12), "domeo").NormalSockets);
    }

    [Fact]
    public void Длина_трека_и_шаг_светильников_зависят_от_помещения()
    {
        SpecRoomInput living = Room("living", 16) with { Track = true };
        SpecRoomInput kitchen = Room("kitchen", 16) with { Track = true };

        // Гостиная: две линии вместо полутора.
        Assert.Equal(6, Calc(living, "excel").TrackMeters);
        Assert.Equal(8, Calc(living, "domeo").TrackMeters);

        // Кухня: длина та же, но шаг 0.8 м — светильников больше.
        Assert.Equal(6, Calc(kitchen, "domeo").TrackMeters);
        Assert.Equal(6, Calc(kitchen, "excel").TrackFixtures);
        Assert.Equal(8, Calc(kitchen, "domeo").TrackFixtures);
    }

    [Fact]
    public void Одноклавишные_считаются_от_групп_света()
    {
        Assert.Equal(1, Calc(Room("living", 20), "excel").SingleSwitches);
        Assert.Equal(2, Calc(Room("living", 20), "domeo").SingleSwitches);
    }

    [Fact]
    public void Двухклавишный_появляется_на_кухне()
    {
        // В таблице кухни нет в списке помещений с двумя группами света, хотя в
        // справочнике их две.
        Assert.Equal(0, Calc(Room("kitchen", 12), "excel").DoubleSwitches);
        Assert.Equal(1, Calc(Room("kitchen", 12), "domeo").DoubleSwitches);
    }

    [Fact]
    public void Стиральная_машина_в_мокрой_зоне_не_считается_дважды()
    {
        SpecRoomInput room = Room("wc", 3) with { WashingMachine = true };

        Assert.Equal(2, Calc(room, "excel").NormalSockets);   // 3/5 + 1 за машину
        Assert.Equal(1, Calc(room, "domeo").NormalSockets);   // 3/3, машина ушла во влагозащищённые
        Assert.Equal(2, Calc(room, "domeo").WetSockets);      // машина + раковина
    }

    [Fact]
    public void Бра_у_кровати_включаются_двухклавишным()
    {
        SpecCounts bedroom = Calc(Room("bedroom", 17.5) with { Sconces = true }, "domeo");
        SpecCounts living = Calc(Room("living", 17.5) with { Sconces = true }, "domeo");

        Assert.Equal(2, bedroom.DoubleSwitches);              // группа света + бра у кровати
        Assert.Equal(1, living.DoubleSwitches);               // только группа света
    }

    // ---------- поведение, одинаковое в обоих профилях ----------

    [Theory]
    [InlineData("excel")]
    [InlineData("domeo")]
    public void Бра_всегда_чётные(string profile)
    {
        foreach (double area in new[] { 4.0, 8.0, 12.0, 17.5, 25.0, 40.0 })
        {
            int sconces = Calc(Room("living", area) with { Sconces = true }, profile).Sconces;

            Assert.Equal(0, sconces % 2);
            Assert.True(sconces >= 2);
        }
    }

    [Theory]
    [InlineData("excel")]
    [InlineData("domeo")]
    public void Точечных_не_меньше_двух(string profile)
    {
        Assert.Equal(2, Calc(Room("wc", 1.2) with { Downlights = true }, profile).Downlights);
        Assert.Equal(15, Calc(Room("bedroom", 17.5) with { Downlights = true }, profile).Downlights);
    }

    [Theory]
    [InlineData("excel")]
    [InlineData("domeo")]
    public void Уровень_комфорта_двигает_только_площадную_часть(string profile)
    {
        SpecRoomInput room = Room("living", 20);

        // 20/5 = 4 базовых розетки, коэффициент двигает именно их.
        Assert.Equal(4, Calc(room with { Comfort = ComfortLevel.Economy }, profile).NormalSockets);
        Assert.Equal(4, Calc(room with { Comfort = ComfortLevel.Standard }, profile).NormalSockets);
        Assert.Equal(5, Calc(room with { Comfort = ComfortLevel.Premium }, profile).NormalSockets);

        // А розетка холодильника прибавляется ровно одна на любом уровне.
        SpecRoomInput withFridge = room with { Fridge = true };
        Assert.Equal(5, Calc(withFridge with { Comfort = ComfortLevel.Standard }, profile).NormalSockets);
        Assert.Equal(6, Calc(withFridge with { Comfort = ComfortLevel.Premium }, profile).NormalSockets);
    }

    [Theory]
    [InlineData("excel")]
    [InlineData("domeo")]
    public void Незаполненное_помещение_даёт_нули(string profile)
    {
        SpecRoomInput room = Room("living", 20) with { Comfort = null, Tv = 2, Downlights = true };

        Assert.Equal(SpecCounts.Zero, Calc(room, profile));
    }

    [Theory]
    [InlineData("excel")]
    [InlineData("domeo")]
    public void Лента_считается_по_числу_окон(string profile)
    {
        SpecRoomInput room = Room("bedroom", 17.5) with { LedStrip = true };

        Assert.Equal(5, Calc(room, profile).LedMeters);                     // (2.0 + 0.4) * 2
        Assert.Equal(10, Calc(room with { Windows = 2 }, profile).LedMeters);
        Assert.Equal(0, Calc(room with { LedStrip = false }, profile).LedMeters);
    }

    [Theory]
    [InlineData("excel")]
    [InlineData("domeo")]
    public void Рамок_столько_же_сколько_механизмов(string profile)
    {
        SpecCounts counts = Calc(
            Room("bedroom", 17.5) with { Tv = 1, Computers = 1, Sconces = true, LedStrip = true },
            profile);

        Assert.Equal(
            counts.NormalSockets + counts.WetSockets + counts.TvSockets + counts.InternetSockets
            + counts.SingleSwitches + counts.DoubleSwitches + counts.PassThroughSwitches
            + counts.CrossSwitches,
            counts.Frames);
    }

    // ---------- границы ----------

    [Fact]
    public void Неизвестный_тип_помещения_отвергается()
    {
        Assert.Throws<ArgumentException>(() => Calc(Room("garage", 10), "domeo"));
    }

    [Fact]
    public void Неизвестный_профиль_отвергается()
    {
        Assert.Throws<ArgumentException>(() => Calc(Room("living", 10), "nope"));
    }

    [Fact]
    public void По_умолчанию_считаем_моделью_Domeo()
    {
        SpecResult result = SpecCalculator.Calculate([Room("bathroom", 4.6)]);

        Assert.Equal("domeo", result.Profile);
        Assert.Equal(1, result.Total.WetSockets);
    }

    [Fact]
    public void Отрицательные_значения_на_входе_обнуляются()
    {
        SpecRoomInput room = new()
        {
            RoomType = "living",
            Area = -10,
            Comfort = ComfortLevel.Standard,
            Tv = -1,
            Computers = -5,
        };

        Assert.Equal(0, Calc(room, "domeo").NormalSockets);
    }

    [Fact]
    public void Оба_профиля_знают_одни_и_те_же_помещения()
    {
        IReadOnlyList<string> excel = SpecReference.Profiles["excel"].RoomOrder;
        IReadOnlyList<string> domeo = SpecReference.Profiles["domeo"].RoomOrder;

        Assert.Equal(excel, domeo);
        Assert.Equal(10, domeo.Count);
    }

    [Fact]
    public void Гардеробная_и_лоджия_считаются_а_не_падают()
    {
        // В исходном справочнике этих строк не было, хотя в списке помещений они есть.
        Assert.True(Calc(Room("wardrobe", 4) with { Downlights = true }, "domeo").Downlights > 0);
        Assert.True(Calc(Room("balcony", 2.2) with { Downlights = true }, "domeo").Downlights > 0);
    }
}
