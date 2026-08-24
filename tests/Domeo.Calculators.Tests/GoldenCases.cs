using System.Text.Json;
using System.Text.Json.Serialization;
using Domeo.Calculators.Spec;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Загрузка эталонов: для калькуляторов calcal.ru они сняты с оригинального кода
/// (tools/golden/generate.mjs), для нормировочной модели — с исходной таблицы
/// (tools/golden/generate-spec.py). Пересобираются только при изменении исходников.
/// </summary>
internal static class GoldenCases
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Эталон, у которого в корне объект, а не массив случаев.</summary>
    public static T LoadDocument<T>(string fileName) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(Path(fileName)), Options)
            ?? throw new InvalidOperationException($"Пустой golden-файл: {fileName}");

    public static IEnumerable<object[]> Load<T>(string fileName)
    {
        List<T> cases = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path(fileName)), Options)
            ?? throw new InvalidOperationException($"Пустой golden-файл: {fileName}");

        return cases.Select(c => new object[] { c! });
    }

    private static string Path(string fileName)
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "golden", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Нет golden-файла {fileName}. Как собрать — в README.", path);

        return path;
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
    double HeightCoeff, string RoomName, string AreaPerFixture);

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

// ---------- нормировочная модель (spec) ----------

internal sealed record SpecGolden(string Source, string Note, List<SpecGoldenCase> Cases, SpecCounts Total);

internal sealed record SpecGoldenCase(string Name, SpecRoomInput Input, SpecCounts Expected)
{
    public override string ToString() => Name;
}
