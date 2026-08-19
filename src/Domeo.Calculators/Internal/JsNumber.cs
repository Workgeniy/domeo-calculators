using System.Globalization;
using System.Numerics;

namespace Domeo.Calculators.Internal;

/// <summary>
/// Точная реализация Number.prototype.toFixed из ECMAScript.
/// </summary>
/// <remarks>
/// Нужна ради побитового совпадения с оригиналом. Наивное округление здесь не годится:
/// toFixed округляет половину ВВЕРХ, но от точного двоичного значения double, а не от
/// его десятичного вида. Отсюда результаты, которые выглядят непоследовательно:
/// <code>
///   (1.05).toFixed(1) === "1.1"   // double чуть больше 1.05
///   (1.65).toFixed(1) === "1.6"   // double на самом деле 1.64999999999999991118...
///   (1.35).toFixed(1) === "1.3"   // double чуть меньше 1.35
/// </code>
/// Math.Round(..., MidpointRounding.AwayFromZero) дал бы 1.7 и 1.4 — расхождение с сайтом.
/// Поэтому считаем в BigInteger на точном значении мантиссы.
/// </remarks>
internal static class JsNumber
{
    /// <summary>Аналог value.toFixed(digits).</summary>
    public static string ToFixed(double value, int digits)
    {
        if (digits is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(digits), digits, "digits должен быть в диапазоне 0..100.");

        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        if (value < 0) return "-" + ToFixed(-value, digits);

        // Спец случай из спеки: очень большие числа печатаются обычным ToString.
        if (value >= 1e21) return value.ToString("R", CultureInfo.InvariantCulture);

        // n — целое, минимизирующее |n / 10^digits - value|; при равенстве берётся большее n.
        BigInteger n = ScaleAndRound(value, digits, roundHalfUp: true);

        string s = n.ToString(CultureInfo.InvariantCulture);
        if (digits == 0) return s;

        if (s.Length <= digits) s = s.PadLeft(digits + 1, '0');
        return s[..^digits] + "." + s[^digits..];
    }

    /// <summary>
    /// Аналог Math.round: ближайшее целое, половина уходит в сторону +∞.
    /// </summary>
    /// <remarks>
    /// Math.Round(x, MidpointRounding.AwayFromZero) не подходит по двум причинам: он
    /// округляет от нуля (для отрицательных это другая сторона) и работает не по точному
    /// значению double. Здесь — ровно семантика спеки.
    /// </remarks>
    public static double Round(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return value;

        // Для положительных «половина вверх» и есть «в сторону +∞».
        if (value >= 0) return (double)ScaleAndRound(value, 0, roundHalfUp: true);

        // Для отрицательных в сторону +∞ означает «половина к нулю», то есть вниз по модулю.
        return -(double)ScaleAndRound(-value, 0, roundHalfUp: false);
    }

    /// <summary>Округляет value * 10^digits до целого на точном значении double.</summary>
    /// <param name="roundHalfUp">true — ровная половина идёт вверх, false — вниз.</param>
    private static BigInteger ScaleAndRound(double value, int digits, bool roundHalfUp = true)
    {
        // Раскладываем double на точное m * 2^e.
        (BigInteger mantissa, int exponent) = Decompose(value);

        BigInteger pow10 = BigInteger.Pow(10, digits);

        if (exponent >= 0)
        {
            // Значение целое — умножение точное, округлять нечего.
            return mantissa * BigInteger.Pow(2, exponent) * pow10;
        }

        BigInteger numerator = mantissa * pow10;
        BigInteger denominator = BigInteger.Pow(2, -exponent);

        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);

        BigInteger twiceRemainder = remainder * 2;
        bool roundUp = roundHalfUp
            ? twiceRemainder >= denominator   // «If there are two such n, pick the larger n»
            : twiceRemainder > denominator;

        if (roundUp) quotient += 1;

        return quotient;
    }

    /// <summary>Точное представление неотрицательного конечного double как mantissa * 2^exponent.</summary>
    private static (BigInteger Mantissa, int Exponent) Decompose(double value)
    {
        if (value == 0) return (BigInteger.Zero, 0);

        long bits = BitConverter.DoubleToInt64Bits(value);
        int biasedExponent = (int)((bits >> 52) & 0x7FF);
        long fraction = bits & 0xF_FFFF_FFFF_FFFFL;

        if (biasedExponent == 0)
        {
            // Субнормальное: показатель фиксирован, неявной единицы нет.
            return (fraction, -1074);
        }

        return (fraction | (1L << 52), biasedExponent - 1075);
    }
}
