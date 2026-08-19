using Domeo.Calculators.Internal;
using Domeo.Calculators.Reference;

namespace Domeo.Calculators.Lighting;

/// <param name="Area">Площадь помещения, м². В оригинале 1–500, шаг 0.5.</param>
/// <param name="Height">Высота потолков, м. В оригинале 2.0–6.0, шаг 0.1.</param>
/// <param name="RoomType">Идентификатор типа помещения, см. ReferenceData.RoomTypes.</param>
/// <param name="WallColor">Идентификатор цвета стен, см. ReferenceData.WallColors.</param>
/// <param name="LampType">Идентификатор типа лампы, см. ReferenceData.LampTypes.</param>
/// <param name="FixtureWatt">Мощность одного светильника, Вт. В оригинале 1–500.</param>
/// <param name="HoursPerDay">Часов работы в день. В оригинале 1–24.</param>
/// <param name="Tariff">Тариф, руб/кВт·ч.</param>
public sealed record LightingInput(
    double Area,
    double Height,
    string RoomType,
    string WallColor,
    string LampType,
    int FixtureWatt,
    int HoursPerDay,
    double Tariff);

/// <summary>
/// Правила ввода из оригинала. На calcal.ru они живут в обработчиках onChange,
/// то есть поле само «схлопывается» к допустимому значению прямо при наборе.
/// </summary>
/// <remarks>
/// Расчёт эти правила не применяет — там их нет и в оригинале. Нормализация
/// нужна на границе (UI и API), иначе, например, 100 часов в сутки дадут число.
/// </remarks>
public static class LightingInputRules
{
    /// <summary>Приводит ввод к допустимому виду: как onChange у оригинала.</summary>
    public static LightingInput Normalize(LightingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input with
        {
            Area = Math.Max(0, Sane(input.Area)),
            Height = Math.Max(0, Sane(input.Height)),
            // Единственное поле с верхней границей: max(0, min(24, x)).
            HoursPerDay = (int)Math.Max(0, Math.Min(24, input.HoursPerDay)),
            Tariff = Math.Max(0, Sane(input.Tariff)),
            FixtureWatt = Math.Max(1, input.FixtureWatt),
        };
    }

    // parseFloat(...)||0 в оригинале превращает NaN в 0.
    private static double Sane(double value) => double.IsNaN(value) ? 0 : value;
}

public sealed record LightingResult(
    int RequiredLux,
    int RequiredLumens,
    int FixtureLumen,
    int FixtureCount,
    int FixtureWatt,
    int TotalWatt,
    double MonthlyKwh,
    double MonthlyCost,
    string MonthlyKwhFormatted,
    string MonthlyCostFormatted,
    double HeightCoeff,
    string RoomName,
    double AreaPerFixture,
    string AreaPerFixtureFormatted);

/// <summary>
/// Расчёт освещённости помещения — точный порт калькулятора с calcal.ru.
/// </summary>
/// <remarks>
/// Поведение воспроизводится один в один, включая спорные места оригинала:
/// месяц всегда 30 дней; коэффициент высоты ступенчатый, из-за чего 2.70 м даёт 1.0,
/// а 2.71 м уже 1.2 — скачок 20% на сантиметре.
/// Расхождения с оригиналом перечислены в docs/calc-lighting.md.
/// </remarks>
public static class LightingCalculator
{
    /// <summary>
    /// Считает освещение. Возвращает null на невалидном вводе — как оригинал,
    /// который в этом случае просто не показывает результат.
    /// </summary>
    public static LightingResult? Calculate(LightingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        RoomLightingNorm? room = Find(ReferenceData.RoomTypes, r => r.Id == input.RoomType);
        WallColor? wall = Find(ReferenceData.WallColors, w => w.Id == input.WallColor);
        LampType? lamp = Find(ReferenceData.LampTypes, l => l.Id == input.LampType);

        if (room is null || wall is null || lamp is null
            || input.Area <= 0 || input.Height <= 0 || input.FixtureWatt <= 0)
        {
            return null;
        }

        double heightCoeff = ResolveHeightCoeff(input.Height);

        // Светлые стены (0.7) приняты за базу: множитель 0.7/coeff даёт 1.0 / 1.4 / 2.33.
        double requiredLumens = room.Lux * input.Area * heightCoeff * (0.7 / wall.Coeff);
        double fixtureLumen = input.FixtureWatt * (double)lamp.Efficiency;
        int fixtureCount = (int)Math.Ceiling(requiredLumens / fixtureLumen);
        int totalWatt = fixtureCount * input.FixtureWatt;

        double monthlyKwh = totalWatt / 1e3 * input.HoursPerDay * 30;
        double monthlyCost = monthlyKwh * input.Tariff;

        return new LightingResult(
            RequiredLux: room.Lux,
            RequiredLumens: (int)JsNumber.Round(requiredLumens),
            FixtureLumen: (int)JsNumber.Round(fixtureLumen),
            FixtureCount: fixtureCount,
            FixtureWatt: input.FixtureWatt,
            TotalWatt: totalWatt,
            MonthlyKwh: monthlyKwh,
            MonthlyCost: monthlyCost,
            MonthlyKwhFormatted: JsNumber.ToFixed(monthlyKwh, 1),
            MonthlyCostFormatted: JsNumber.ToFixed(monthlyCost, 0),
            HeightCoeff: heightCoeff,
            RoomName: room.Name,
            // Оригинал показывает это в блоке «Рекомендация по размещению».
            AreaPerFixture: input.Area / fixtureCount,
            AreaPerFixtureFormatted: JsNumber.ToFixed(input.Area / fixtureCount, 1));
    }

    /// <summary>Ступенчатая поправка на высоту потолка: &lt;=2.7 → 1.0, &lt;=3.0 → 1.2, &lt;=3.5 → 1.5, выше → 2.0.</summary>
    private static double ResolveHeightCoeff(double height)
    {
        foreach (HeightCoeffStep step in ReferenceData.HeightCoeffSteps)
        {
            if (step.MaxHeight is null || height <= step.MaxHeight.Value) return step.Coeff;
        }

        return 1.0;
    }

    private static T? Find<T>(IReadOnlyList<T> source, Func<T, bool> predicate) where T : class
    {
        foreach (T item in source)
        {
            if (predicate(item)) return item;
        }

        return null;
    }
}
