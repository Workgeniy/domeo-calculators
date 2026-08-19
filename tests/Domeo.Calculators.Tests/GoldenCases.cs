using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Загрузка golden-векторов, снятых с оригинального кода calcal.ru
/// (tools/golden/generate.mjs). Файлы пересобираются только при изменении data/*.json.
/// </summary>
internal static class GoldenCases
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IEnumerable<object[]> Load<T>(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "golden", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Нет golden-файла {fileName}. Собрать: node tools/golden/generate.mjs", path);

        List<T> cases = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Пустой golden-файл: {fileName}");

        return cases.Select(c => new object[] { c! });
    }
}

// ---------- освещённость ----------

internal sealed record LightingCase(string Name, LightingCaseInput Input, LightingExpected? Expected)
{
    public override string ToString() => Name;
}

internal sealed record LightingCaseInput(
    double Area, double Height, string RoomType, string WallColor,
    string LampType, int FixtureWatt, int HoursPerDay, double Tariff);

internal sealed record LightingExpected(
    int RequiredLux, int RequiredLumens, int FixtureLumen, int FixtureCount,
    int FixtureWatt, int TotalWatt, string MonthlyKwh, string MonthlyCost,
    double HeightCoeff, string RoomName);

// ---------- электрофурнитура ----------

internal sealed record ElectricalCase(string Name, ElectricalCaseInput Input, ElectricalExpected Expected)
{
    public override string ToString() => Name;
}

internal sealed record ElectricalCaseInput(Dictionary<string, RoomStateDto> Rooms, string Tier);

internal sealed record RoomStateDto(
    bool Enabled, double Area, int Count, int Outlets, int Switches,
    int Passthrough, int Usb, int Tv, int Internet);

internal sealed record ElectricalExpected(
    int TotalOutlets, int TotalSwitches, int TotalUsb, int TotalTv, int TotalInternet,
    int TotalBoxes, int TotalFrames, int TotalCableMeters, int TotalCost,
    List<RoomBreakdownDto> RoomBreakdown, string TierLabel);

internal sealed record RoomBreakdownDto(
    string Key, string Label, int Count, int Outlets, int Switches,
    int Usb, int Tv, int Internet, int Cable, int Cost);
