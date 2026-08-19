using Domeo.Calculators.Electrical;
using Xunit;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Сверка порта с эталоном, снятым с оригинального кода calcal.ru.
/// </summary>
public class GoldenElectricalTests
{
    public static IEnumerable<object[]> Cases => GoldenCases.Load<ElectricalCase>("electrical.json");

    [Theory]
    [MemberData(nameof(Cases))]
    internal void Совпадает_с_оригиналом(ElectricalCase testCase)
    {
        Dictionary<string, RoomState> rooms = testCase.Input.Rooms.ToDictionary(
            kv => kv.Key,
            kv => new RoomState(
                kv.Value.Enabled, kv.Value.Area, kv.Value.Count, kv.Value.Outlets,
                kv.Value.Switches, kv.Value.Passthrough, kv.Value.Usb, kv.Value.Tv, kv.Value.Internet),
            StringComparer.Ordinal);

        ElectricalResult actual = ElectricalCalculator.Calculate(rooms, testCase.Input.Tier);
        ElectricalExpected e = testCase.Expected;

        Assert.Equal(e.TotalOutlets, actual.TotalOutlets);
        Assert.Equal(e.TotalSwitches, actual.TotalSwitches);
        Assert.Equal(e.TotalUsb, actual.TotalUsb);
        Assert.Equal(e.TotalTv, actual.TotalTv);
        Assert.Equal(e.TotalInternet, actual.TotalInternet);
        Assert.Equal(e.TotalBoxes, actual.TotalBoxes);
        Assert.Equal(e.TotalFrames, actual.TotalFrames);
        Assert.Equal(e.TotalCableMeters, actual.TotalCableMeters);
        Assert.Equal(e.TotalCost, actual.TotalCost);
        Assert.Equal(e.TierLabel, actual.TierLabel);

        // Порядок строк тоже часть контракта — оригинал обходит комнаты в порядке объявления.
        Assert.Equal(e.RoomBreakdown.Count, actual.RoomBreakdown.Count);
        for (int idx = 0; idx < e.RoomBreakdown.Count; idx++)
        {
            RoomBreakdownDto expected = e.RoomBreakdown[idx];
            RoomBreakdown row = actual.RoomBreakdown[idx];

            Assert.Equal(expected.Key, row.Key);
            Assert.Equal(expected.Label, row.Label);
            Assert.Equal(expected.Count, row.Count);
            Assert.Equal(expected.Outlets, row.Outlets);
            Assert.Equal(expected.Switches, row.Switches);
            Assert.Equal(expected.Usb, row.Usb);
            Assert.Equal(expected.Tv, row.Tv);
            Assert.Equal(expected.Internet, row.Internet);
            Assert.Equal(expected.Cable, row.Cable);
            Assert.Equal(expected.Cost, row.Cost);
        }
    }

    [Fact]
    public void Дефолтные_комнаты_совпадают_с_пресетами()
    {
        IReadOnlyDictionary<string, RoomState> rooms = ElectricalCalculator.DefaultRooms();

        Assert.Equal(7, rooms.Count);
        Assert.Equal(10, rooms["kitchen"].Outlets);    // середина 8–12
        Assert.Equal(3, rooms["bathroom"].Outlets);    // середина 2–3, половина вверх
        Assert.True(rooms["kitchen"].Enabled);
        Assert.False(rooms["office"].Enabled);
    }

    [Fact]
    public void Неизвестный_ценовой_уровень_отвергается()
    {
        Assert.Throws<ArgumentException>(() =>
            ElectricalCalculator.Calculate(ElectricalCalculator.DefaultRooms(), "nope"));
    }
}
