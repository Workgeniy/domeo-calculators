using System.Collections.ObjectModel;
using System.Text.Json;

namespace Domeo.Calculators.Reference;

/// <param name="SocketsPerSqm">Делитель площади: сколько м² приходится на одну базовую розетку.</param>
/// <param name="TrackLines">Множитель длины трека: сколько условных стен он проходит.</param>
/// <param name="TrackStep">Шаг трековых светильников, м.</param>
/// <param name="LightGroups">Базовое число групп света — от него зависят выключатели.</param>
/// <param name="Wet">Мокрая зона: розетки влагозащищённые (IP44).</param>
/// <param name="PassThrough">Проходные выключатели — по числу входов и точек управления.</param>
/// <param name="Pendants">Сколько подвесов вешают, если подвесное освещение выбрано.</param>
/// <param name="SconceDoubleSwitch">Бра добавляют ещё и двухклавишный — две стороны кровати.</param>
/// <param name="DefaultVacuum">Подсказка формы: обычно ли в этом помещении нужна розетка для пылесоса.</param>
/// <param name="DefaultTvSocket">Подсказка формы: ТВ-розетка по умолчанию.</param>
/// <param name="DefaultInternetSocket">Подсказка формы: интернет-розетка по умолчанию.</param>
/// <remarks>
/// Три последних поля в расчёте не участвуют — они нужны интерфейсу, чтобы предзаполнить
/// форму по типу помещения, а дизайнер правил отклонения.
/// </remarks>
public sealed record SpecRoom(
    string Label,
    double SocketsPerSqm,
    double TrackLines,
    double TrackStep,
    int LightGroups,
    bool Wet,
    int PassThrough,
    int Pendants,
    bool SconceDoubleSwitch,
    bool DefaultVacuum,
    int DefaultTvSocket,
    int DefaultInternetSocket);

/// <summary>Общие коэффициенты — одинаковы для всех профилей.</summary>
public sealed record SpecCoefficients(
    IReadOnlyDictionary<string, double> Comfort,
    int SocketsPerTv,
    int SocketsPerComputer,
    int SocketsPerFloorLamp,
    int SocketsPerDeskLamp,
    int SocketsPerAppliance,
    int SmallApplianceSockets,
    int VanityWetSockets,
    double WindowWidth,
    double LedSlack,
    int LedRuns,
    double AreaPerDownlight,
    int MinDownlights,
    double AreaPerSconce,
    int ChandeliersPerRoom);

/// <param name="TvSocketsPerTv">Антенных розеток на один телевизор.</param>
/// <param name="TvSocketsExtra">Добавка к ТВ-розеткам, если телевизор в комнате есть.</param>
/// <param name="InternetSocketsPerComputer">Интернет-розеток на один компьютер.</param>
/// <param name="InternetSocketsExtra">Добавка к интернет-розеткам, если компьютер в комнате есть.</param>
/// <param name="SingleSwitchBaseIsOne">
/// База одноклавишных — всегда 1, а не число групп света. Так считает исходная таблица:
/// обращение к справочнику там не работало.
/// </param>
/// <param name="WetAppliancesAlsoCountAsNormal">
/// Стиральная и сушильная машины в мокрой зоне попадают и в обычные розетки, и во
/// влагозащищённые. Тоже поведение исходной таблицы — прибор считается дважды.
/// </param>
public sealed record SpecProfile(
    string Key,
    string Label,
    int TvSocketsPerTv,
    int TvSocketsExtra,
    int InternetSocketsPerComputer,
    int InternetSocketsExtra,
    bool SingleSwitchBaseIsOne,
    bool WetAppliancesAlsoCountAsNormal,
    IReadOnlyList<string> RoomOrder,
    IReadOnlyDictionary<string, SpecRoom> Rooms);

/// <summary>
/// Справочник нормировочной модели: типы помещений и коэффициенты. Загружается из
/// встроенного data/spec.json — он и есть источник правды, в коде чисел нет.
/// </summary>
public static class SpecReference
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static SpecCoefficients Coefficients { get; }

    /// <summary>Профили расчёта: excel — эталон переноса, domeo — рабочая модель.</summary>
    public static IReadOnlyDictionary<string, SpecProfile> Profiles { get; }

    /// <summary>Профиль по умолчанию — тот, которым считаем, если не сказано иного.</summary>
    public static SpecProfile Default { get; }

    static SpecReference()
    {
        using JsonDocument document = JsonDocument.Parse(
            EmbeddedResources.Read("Domeo.Calculators.Data.spec.json"));
        JsonElement root = document.RootElement;

        Coefficients = root.GetProperty("coefficients").Deserialize<SpecCoefficients>(Options)!;

        var profiles = new Dictionary<string, SpecProfile>(StringComparer.Ordinal);
        foreach (JsonProperty profile in root.GetProperty("profiles").EnumerateObject())
        {
            profiles[profile.Name] = ReadProfile(profile.Name, profile.Value);
        }

        Profiles = new ReadOnlyDictionary<string, SpecProfile>(profiles);

        string defaultKey = root.GetProperty("defaultProfile").GetString()!;
        Default = profiles[defaultKey];
    }

    /// <summary>Профиль по ключу; пустой ключ означает профиль по умолчанию.</summary>
    public static SpecProfile Profile(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return Default;

        return Profiles.TryGetValue(key, out SpecProfile? profile)
            ? profile
            : throw new ArgumentException($"Неизвестный профиль расчёта: {key}", nameof(key));
    }

    private static SpecProfile ReadProfile(string key, JsonElement element)
    {
        // Порядок помещений задаётся порядком объявления в json — от него зависит
        // порядок строк в разбивке. На порядок Dictionary полагаться нельзя.
        var order = new List<string>();
        var rooms = new Dictionary<string, SpecRoom>(StringComparer.Ordinal);
        foreach (JsonProperty room in element.GetProperty("rooms").EnumerateObject())
        {
            order.Add(room.Name);
            rooms[room.Name] = room.Value.Deserialize<SpecRoom>(Options)!;
        }

        return new SpecProfile(
            Key: key,
            Label: element.GetProperty("label").GetString()!,
            TvSocketsPerTv: element.GetProperty("tvSocketsPerTv").GetInt32(),
            TvSocketsExtra: element.GetProperty("tvSocketsExtra").GetInt32(),
            InternetSocketsPerComputer: element.GetProperty("internetSocketsPerComputer").GetInt32(),
            InternetSocketsExtra: element.GetProperty("internetSocketsExtra").GetInt32(),
            SingleSwitchBaseIsOne: element.GetProperty("singleSwitchBaseIsOne").GetBoolean(),
            WetAppliancesAlsoCountAsNormal: element.GetProperty("wetAppliancesAlsoCountAsNormal").GetBoolean(),
            RoomOrder: order.AsReadOnly(),
            Rooms: new ReadOnlyDictionary<string, SpecRoom>(rooms));
    }
}
