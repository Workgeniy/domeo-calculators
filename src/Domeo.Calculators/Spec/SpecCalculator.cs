using Domeo.Calculators.Reference;

namespace Domeo.Calculators.Spec;

/// <summary>Уровень комфорта: множитель площадной части розеток и базовых групп света.</summary>
public enum ComfortLevel
{
    Economy,
    Standard,
    Premium,
}

/// <summary>
/// Параметры одного помещения. Всё, что дизайнер знает на этапе планировки:
/// тип, площадь, техника, пожелания по свету.
/// </summary>
/// <remarks>
/// <see cref="Comfort"/> = null означает «помещение не заполнено» — тогда весь расчёт
/// по нему нулевой. Так ведёт себя и исходная таблица: пустой уровень комфорта гасит
/// все формулы столбца.
/// </remarks>
public sealed record SpecRoomInput
{
    /// <summary>Ключ типа помещения из справочника: hallway, kitchen, bedroom…</summary>
    public required string RoomType { get; init; }

    /// <summary>Площадь, м².</summary>
    public double Area { get; init; }

    /// <summary>Как помещение названо в проекте. На расчёт не влияет, едет в результат.</summary>
    public string? Name { get; init; }

    public ComfortLevel? Comfort { get; init; } = ComfortLevel.Standard;

    public int Tv { get; init; }
    public int Computers { get; init; }
    public int FloorLamps { get; init; }
    public int DeskLamps { get; init; }

    public bool Vacuum { get; init; }
    public bool WashingMachine { get; init; }
    public bool Dryer { get; init; }
    public bool Oven { get; init; }
    public bool Hob { get; init; }
    public bool Hood { get; init; }
    public bool Dishwasher { get; init; }
    public bool Fridge { get; init; }
    public bool Microwave { get; init; }

    /// <summary>Запас на мелкую технику — чайник, тостер, мультиварка.</summary>
    public bool SmallAppliances { get; init; }

    public bool Track { get; init; }
    public bool Downlights { get; init; }
    public bool Pendants { get; init; }
    public bool Chandelier { get; init; }
    public bool Sconces { get; init; }
    public bool LedStrip { get; init; }

    /// <summary>Окон с нишей под ленту. Считается, только если выбрана лента.</summary>
    public int Windows { get; init; } = 1;
}

/// <summary>
/// Приведение входа к допустимому виду. В исходной таблице проверок нет — ячейку
/// можно заполнить чем угодно; здесь это граница API.
/// </summary>
public static class SpecInputRules
{
    public static SpecRoomInput Normalize(SpecRoomInput room)
    {
        ArgumentNullException.ThrowIfNull(room);

        return room with
        {
            Area = double.IsFinite(room.Area) ? Math.Max(0, room.Area) : 0,
            Tv = Math.Max(0, room.Tv),
            Computers = Math.Max(0, room.Computers),
            FloorLamps = Math.Max(0, room.FloorLamps),
            DeskLamps = Math.Max(0, room.DeskLamps),
            Windows = Math.Max(0, room.Windows),
        };
    }

    public static IReadOnlyList<SpecRoomInput> Normalize(IEnumerable<SpecRoomInput> rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        return rooms.Select(Normalize).ToList().AsReadOnly();
    }
}

/// <summary>Спецификация: сколько чего нужно. Метраж — в метрах, остальное — в штуках.</summary>
public sealed record SpecCounts(
    int NormalSockets,
    int WetSockets,
    int TvSockets,
    int InternetSockets,
    int SingleSwitches,
    int DoubleSwitches,
    int PassThroughSwitches,
    int CrossSwitches,
    int Frames,
    int TrackMeters,
    int TrackFixtures,
    int Downlights,
    int LedMeters,
    int Chandeliers,
    int Pendants,
    int Sconces)
{
    public static readonly SpecCounts Zero = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static SpecCounts operator +(SpecCounts a, SpecCounts b) => new(
        a.NormalSockets + b.NormalSockets,
        a.WetSockets + b.WetSockets,
        a.TvSockets + b.TvSockets,
        a.InternetSockets + b.InternetSockets,
        a.SingleSwitches + b.SingleSwitches,
        a.DoubleSwitches + b.DoubleSwitches,
        a.PassThroughSwitches + b.PassThroughSwitches,
        a.CrossSwitches + b.CrossSwitches,
        a.Frames + b.Frames,
        a.TrackMeters + b.TrackMeters,
        a.TrackFixtures + b.TrackFixtures,
        a.Downlights + b.Downlights,
        a.LedMeters + b.LedMeters,
        a.Chandeliers + b.Chandeliers,
        a.Pendants + b.Pendants,
        a.Sconces + b.Sconces);
}

public sealed record SpecRoomResult(string RoomType, string Label, string? Name, SpecCounts Counts);

public sealed record SpecResult(string Profile, IReadOnlyList<SpecRoomResult> Rooms, SpecCounts Total);

/// <summary>
/// Нормировочный расчёт электрики и света по помещениям: из площади и параметров
/// выводит количества. Цен не считает — это спецификация, а не смета.
/// </summary>
/// <remarks>
/// Перенос модели из data/source/lighting-electrical.xlsx. Профиль <c>excel</c> повторяет
/// таблицу дословно и служит эталоном переноса, профиль <c>domeo</c> — рабочая модель
/// с восстановленным справочником по типам помещений. Разбор и список отличий —
/// в docs/calc-spec.md.
/// </remarks>
public static class SpecCalculator
{
    public static SpecResult Calculate(IEnumerable<SpecRoomInput> rooms, string? profileKey = null)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        SpecProfile profile = SpecReference.Profile(profileKey);
        SpecCoefficients coefficients = SpecReference.Coefficients;

        var results = new List<SpecRoomResult>();
        SpecCounts total = SpecCounts.Zero;

        foreach (SpecRoomInput input in rooms)
        {
            SpecRoomInput room = SpecInputRules.Normalize(input);

            if (!profile.Rooms.TryGetValue(room.RoomType, out SpecRoom? reference))
                throw new ArgumentException($"Неизвестный тип помещения: {room.RoomType}", nameof(rooms));

            SpecCounts counts = room.Comfort is null
                ? SpecCounts.Zero
                : Calculate(room, reference, profile, coefficients);

            results.Add(new SpecRoomResult(room.RoomType, reference.Label, room.Name, counts));
            total += counts;
        }

        return new SpecResult(profile.Key, results.AsReadOnly(), total);
    }

    private static SpecCounts Calculate(
        SpecRoomInput room, SpecRoom reference, SpecProfile profile, SpecCoefficients coefficients)
    {
        double comfort = coefficients.Comfort[ComfortKey(room.Comfort!.Value)];

        // --- розетки ---

        // Стиральная и сушильная машины в мокрой зоне идут во влагозащищённые. Исходная
        // таблица считает их в обоих местах — поведение сохранено флагом профиля.
        bool wetTakesAppliances = reference.Wet && !profile.WetAppliancesAlsoCountAsNormal;

        int appliances = Count(room.Vacuum)
            + Count(room.WashingMachine && !wetTakesAppliances)
            + Count(room.Dryer && !wetTakesAppliances)
            + Count(room.Oven)
            + Count(room.Hob)
            + Count(room.Hood)
            + Count(room.Dishwasher)
            + Count(room.Fridge)
            + Count(room.Microwave);

        int scenario = room.Tv * coefficients.SocketsPerTv
            + room.Computers * coefficients.SocketsPerComputer
            + room.FloorLamps * coefficients.SocketsPerFloorLamp
            + room.DeskLamps * coefficients.SocketsPerDeskLamp;

        int small = room.SmallAppliances ? coefficients.SmallApplianceSockets : 0;

        // Комфорт умножает только площадную часть: розетка холодильника от него не зависит.
        int normalSockets = Ceil(
            room.Area / reference.SocketsPerSqm * comfort
            + appliances * coefficients.SocketsPerAppliance
            + small
            + scenario);

        int wetSockets = reference.Wet
            ? Ceil((Count(room.WashingMachine) + Count(room.Dryer) + coefficients.VanityWetSockets) * comfort)
            : 0;

        int tvSockets = room.Tv * profile.TvSocketsPerTv
            + (room.Tv > 0 ? profile.TvSocketsExtra : 0);

        int internetSockets = room.Computers * profile.InternetSocketsPerComputer
            + (room.Computers > 0 ? profile.InternetSocketsExtra : 0);

        // --- выключатели ---

        int switchBase = profile.SingleSwitchBaseIsOne ? 1 : reference.LightGroups;

        int singleSwitches = Ceil(switchBase * comfort)
            + Count(room.Sconces)
            + Count(room.LedStrip);

        int doubleSwitches = Count(reference.LightGroups >= 2)
            + Count(room.Sconces && reference.SconceDoubleSwitch);

        int passThrough = reference.PassThrough;

        // Перекрёстный нужен там, где одним светом управляют больше чем из двух мест;
        // на этапе планировки это помещения с тремя группами света.
        int crossSwitches = Count(reference.LightGroups >= 3);

        // Один механизм — одна рамка. Сборка в блоки по 2–4 поста здесь не считается.
        int frames = normalSockets + wetSockets + tvSockets + internetSockets
            + singleSwitches + doubleSwitches + passThrough + crossSwitches;

        // --- свет ---

        int trackMeters = room.Track ? Ceil(reference.TrackLines * Math.Sqrt(room.Area)) : 0;
        int trackFixtures = room.Track ? Ceil(trackMeters / reference.TrackStep) : 0;

        int downlights = room.Downlights
            ? Math.Max(coefficients.MinDownlights, Ceil(room.Area / coefficients.AreaPerDownlight))
            : 0;

        // Лента идёт по нише в две нитки, плюс запас на подвороты и подключение.
        int ledMeters = room.LedStrip
            ? Ceil((coefficients.WindowWidth + coefficients.LedSlack) * coefficients.LedRuns * room.Windows)
            : 0;

        int chandeliers = room.Chandelier ? coefficients.ChandeliersPerRoom : 0;
        int pendants = room.Pendants ? reference.Pendants : 0;

        // Бра вешают симметрично, поэтому округляем до чётного.
        int sconces = room.Sconces ? Ceil(room.Area / coefficients.AreaPerSconce / 2) * 2 : 0;

        return new SpecCounts(
            NormalSockets: normalSockets,
            WetSockets: wetSockets,
            TvSockets: tvSockets,
            InternetSockets: internetSockets,
            SingleSwitches: singleSwitches,
            DoubleSwitches: doubleSwitches,
            PassThroughSwitches: passThrough,
            CrossSwitches: crossSwitches,
            Frames: frames,
            TrackMeters: trackMeters,
            TrackFixtures: trackFixtures,
            Downlights: downlights,
            LedMeters: ledMeters,
            Chandeliers: chandeliers,
            Pendants: pendants,
            Sconces: sconces);
    }

    private static string ComfortKey(ComfortLevel level) => level switch
    {
        ComfortLevel.Economy => "economy",
        ComfortLevel.Standard => "standard",
        ComfortLevel.Premium => "premium",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    private static int Count(bool value) => value ? 1 : 0;

    private static int Ceil(double value) => (int)Math.Ceiling(value);
}
