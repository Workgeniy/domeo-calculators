using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;

namespace Domeo.Calculators.Reference;

public sealed record RoomLightingNorm(string Id, string Name, int Lux, string Icon);
public sealed record WallColor(string Id, string Name, double Coeff, string Desc);
public sealed record LampType(string Id, string Name, int Efficiency, string Icon);
public sealed record FixturePreset(string Name, int Watt, int Lumen);
public sealed record HeightCoeffStep(double? MaxHeight, double Coeff);

public sealed record RoomPreset(
    string Label, int OutletsMin, int OutletsMax, int Switches, int Passthrough,
    int Usb, int Tv, int Internet, double DefaultArea, bool EnabledByDefault, string Icon);

public sealed record PriceTier(
    string Label, string Tier, int Outlet, int Switch, int UsbOutlet, int TvOutlet,
    int InternetOutlet, int Frame1, int Frame2, int Frame3, int Frame4, int Frame5, int Box);

/// <summary>
/// Справочники, снятые с calcal.ru 19.08.2026. Загружаются из встроенных data/*.json.
/// </summary>
public static class ReferenceData
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<RoomLightingNorm> RoomTypes { get; }
    public static IReadOnlyList<WallColor> WallColors { get; }
    public static IReadOnlyList<LampType> LampTypes { get; }
    public static IReadOnlyList<FixturePreset> FixturePresets { get; }
    public static IReadOnlyList<HeightCoeffStep> HeightCoeffSteps { get; }

    public static IReadOnlyDictionary<string, RoomPreset> RoomPresets { get; }
    public static IReadOnlyDictionary<string, PriceTier> PriceTiers { get; }

    /// <summary>
    /// Порядок комнат из data/outlets.json. Оригинал обходит комнаты через Object.entries,
    /// то есть в порядке объявления — от него зависит порядок строк в разбивке по комнатам.
    /// Полагаться на порядок Dictionary нельзя, поэтому он зафиксирован явно.
    /// </summary>
    public static IReadOnlyList<string> RoomOrder { get; }

    static ReferenceData()
    {
        using JsonDocument lighting = JsonDocument.Parse(ReadResource("Domeo.Calculators.Data.lighting.json"));
        JsonElement lightRoot = lighting.RootElement;

        RoomTypes = Deserialize<List<RoomLightingNorm>>(lightRoot, "roomTypes").AsReadOnly();
        WallColors = Deserialize<List<WallColor>>(lightRoot, "wallColors").AsReadOnly();
        LampTypes = Deserialize<List<LampType>>(lightRoot, "lampTypes").AsReadOnly();
        FixturePresets = Deserialize<List<FixturePreset>>(lightRoot, "fixturePresets").AsReadOnly();
        HeightCoeffSteps = Deserialize<List<HeightCoeffStep>>(lightRoot, "heightCoeff").AsReadOnly();

        using JsonDocument outlets = JsonDocument.Parse(ReadResource("Domeo.Calculators.Data.outlets.json"));
        JsonElement outletsRoot = outlets.RootElement;

        JsonElement presets = outletsRoot.GetProperty("roomPresets");
        var order = new List<string>();
        var presetMap = new Dictionary<string, RoomPreset>(StringComparer.Ordinal);
        foreach (JsonProperty property in presets.EnumerateObject())
        {
            order.Add(property.Name);
            presetMap[property.Name] = property.Value.Deserialize<RoomPreset>(Options)!;
        }

        RoomOrder = order.AsReadOnly();
        RoomPresets = new ReadOnlyDictionary<string, RoomPreset>(presetMap);

        JsonElement tiers = outletsRoot.GetProperty("priceTiers");
        var tierMap = new Dictionary<string, PriceTier>(StringComparer.Ordinal);
        foreach (JsonProperty property in tiers.EnumerateObject())
        {
            tierMap[property.Name] = property.Value.Deserialize<PriceTier>(Options)!;
        }

        PriceTiers = new ReadOnlyDictionary<string, PriceTier>(tierMap);
    }

    private static T Deserialize<T>(JsonElement root, string property) =>
        root.GetProperty(property).Deserialize<T>(Options)!;

    private static string ReadResource(string name)
    {
        Assembly assembly = typeof(ReferenceData).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Встроенный ресурс не найден: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
