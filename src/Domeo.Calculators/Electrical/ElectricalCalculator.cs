using Domeo.Calculators.Reference;

namespace Domeo.Calculators.Electrical;

/// <param name="Enabled">Учитывать ли комнату в расчёте.</param>
/// <param name="Area">Площадь, м². В оригинале 1–200.</param>
/// <param name="Count">Сколько таких комнат. В оригинале 1–10; 0 трактуется как 1.</param>
/// <param name="Outlets">Силовые розетки 220В, 0–20.</param>
/// <param name="Switches">Выключатели, 0–10.</param>
/// <param name="Passthrough">Проходные выключатели, 0–10.</param>
/// <param name="Usb">USB-розетки, 0–10.</param>
/// <param name="Tv">TV-розетки, 0–5.</param>
/// <param name="Internet">Интернет-розетки, 0–5.</param>
public sealed record RoomState(
    bool Enabled,
    double Area,
    int Count,
    int Outlets,
    int Switches,
    int Passthrough,
    int Usb,
    int Tv,
    int Internet)
{
    /// <summary>Начальное состояние комнаты — как в оригинальном useState.</summary>
    public static RoomState FromPreset(RoomPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return new RoomState(
            Enabled: preset.EnabledByDefault,
            Area: preset.DefaultArea,
            Count: 1,
            // Оригинал берёт середину рекомендованного диапазона.
            Outlets: (int)Math.Round((preset.OutletsMin + preset.OutletsMax) / 2.0, MidpointRounding.AwayFromZero),
            Switches: preset.Switches,
            Passthrough: preset.Passthrough,
            Usb: preset.Usb,
            Tv: preset.Tv,
            Internet: preset.Internet);
    }
}

public sealed record RoomBreakdown(
    string Key,
    string Label,
    int Count,
    int Outlets,
    int Switches,
    int Usb,
    int Tv,
    int Internet,
    int Cable,
    int Cost);

public sealed record ElectricalResult(
    int TotalOutlets,
    int TotalSwitches,
    int TotalUsb,
    int TotalTv,
    int TotalInternet,
    int TotalBoxes,
    int TotalFrames,
    int TotalCableMeters,
    int TotalCost,
    IReadOnlyList<RoomBreakdown> RoomBreakdown,
    string TierLabel);

/// <summary>
/// Расчёт комплектации электрофурнитуры — точный порт калькулятора с calcal.ru.
/// </summary>
/// <remarks>
/// Порт намеренно повторяет оригинал вместе с его упрощениями:
/// рамки всегда одинарные (frame1 на каждую точку, рамки на 2–5 постов не используются);
/// проходные выключатели считаются по цене обычных; наценки за IP44 нет;
/// TotalBoxes и TotalFrames всегда равны. Разбор — в docs/calc-outlets.md.
/// </remarks>
public static class ElectricalCalculator
{
    /// <summary>Состояние всех комнат по умолчанию — как при первом открытии страницы.</summary>
    public static IReadOnlyDictionary<string, RoomState> DefaultRooms() =>
        ReferenceData.RoomOrder.ToDictionary(
            key => key,
            key => RoomState.FromPreset(ReferenceData.RoomPresets[key]),
            StringComparer.Ordinal);

    /// <summary>
    /// Считает комплектацию и смету по всем включённым комнатам.
    /// </summary>
    /// <param name="rooms">Состояние комнат; ключи — из ReferenceData.RoomOrder.</param>
    /// <param name="tier">Ценовой уровень: budget, medium или premium.</param>
    public static ElectricalResult Calculate(IReadOnlyDictionary<string, RoomState> rooms, string tier)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentException.ThrowIfNullOrEmpty(tier);

        if (!ReferenceData.PriceTiers.TryGetValue(tier, out PriceTier? price))
            throw new ArgumentException($"Неизвестный ценовой уровень: {tier}", nameof(tier));

        int totalOutlets = 0, totalSwitches = 0, totalUsb = 0, totalTv = 0, totalInternet = 0;
        int totalPoints = 0, totalCable = 0, totalCost = 0;
        var breakdown = new List<RoomBreakdown>();

        // Обход строго в порядке справочника — от него зависит порядок строк в разбивке.
        foreach (string key in ReferenceData.RoomOrder)
        {
            if (!rooms.TryGetValue(key, out RoomState? room) || !room.Enabled) continue;

            int count = room.Count != 0 ? room.Count : 1;   // оригинальное count || 1

            int outlets = room.Outlets * count;
            int switches = room.Switches * count;
            int passthrough = room.Passthrough * count;
            int usb = room.Usb * count;
            int tv = room.Tv * count;
            int internet = room.Internet * count;

            int points = outlets + switches + passthrough + usb + tv + internet;

            // В оригинале записано как points * (4*sqrt(area)/4 + 1.5) — умножение и деление
            // на 4 взаимно уничтожаются, это остаток чьей-то правки. Оставлено как sqrt(area).
            int cable = (int)Math.Ceiling(points * (Math.Sqrt(room.Area) + 1.5));

            int cost = outlets * price.Outlet
                     + (switches + passthrough) * price.Switch
                     + usb * price.UsbOutlet
                     + tv * price.TvOutlet
                     + internet * price.InternetOutlet
                     + points * price.Frame1
                     + points * price.Box;

            totalOutlets += outlets;
            totalSwitches += switches + passthrough;
            totalUsb += usb;
            totalTv += tv;
            totalInternet += internet;
            totalPoints += points;
            totalCable += cable;
            totalCost += cost;

            breakdown.Add(new RoomBreakdown(
                Key: key,
                Label: ReferenceData.RoomPresets[key].Label,
                Count: count,
                Outlets: outlets,
                Switches: switches + passthrough,
                Usb: usb,
                Tv: tv,
                Internet: internet,
                Cable: cable,
                Cost: cost));
        }

        return new ElectricalResult(
            TotalOutlets: totalOutlets,
            TotalSwitches: totalSwitches,
            TotalUsb: totalUsb,
            TotalTv: totalTv,
            TotalInternet: totalInternet,
            // Подрозетники и рамки в оригинале — это просто число точек, потому и совпадают.
            TotalBoxes: totalPoints,
            TotalFrames: totalPoints,
            TotalCableMeters: totalCable,
            TotalCost: totalCost,
            RoomBreakdown: breakdown.AsReadOnly(),
            TierLabel: price.Label);
    }
}
