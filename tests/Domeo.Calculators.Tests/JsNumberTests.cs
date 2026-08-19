using System.Text.Json;
using Domeo.Calculators.Internal;
using Xunit;

namespace Domeo.Calculators.Tests;

/// <summary>
/// Сверка числового слоя с реальным движком JS. Эталон снят через node,
/// а не выписан по памяти — расхождения здесь неочевидны и легко пропустить.
/// </summary>
public class JsNumberTests
{
    private static readonly JsNumberGolden Golden = Load();

    public static IEnumerable<object[]> ToFixedCases =>
        Golden.ToFixed.Select(c => new object[] { c.Value, c.Digits, c.Expected });

    public static IEnumerable<object[]> RoundCases =>
        Golden.Round.Select(c => new object[] { c.Value, c.Expected });

    [Theory]
    [MemberData(nameof(ToFixedCases))]
    public void ToFixed_совпадает_с_JS(double value, int digits, string expected)
    {
        Assert.Equal(expected, JsNumber.ToFixed(value, digits));
    }

    [Theory]
    [MemberData(nameof(RoundCases))]
    public void Round_совпадает_с_JS(double value, double expected)
    {
        Assert.Equal(expected, JsNumber.Round(value));
    }

    /// <summary>
    /// Отдельно фиксируем случаи, ради которых весь этот слой и написан:
    /// наивное округление здесь даёт другой ответ.
    /// </summary>
    [Fact]
    public void Половина_округляется_по_точному_значению_double()
    {
        // 1.65 в double — это 1.64999999999999991118..., поэтому вниз.
        Assert.Equal("1.6", JsNumber.ToFixed(1.65, 1));
        // 1.05 в double чуть больше 1.05, поэтому вверх.
        Assert.Equal("1.1", JsNumber.ToFixed(1.05, 1));

        // Для сравнения: привычный способ округления дал бы 1.7 и 1.1.
        Assert.Equal(1.7, Math.Round(1.65, 1, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Отрицательный_ноль_печатается_без_знака()
    {
        Assert.Equal("0.0", JsNumber.ToFixed(-0.0, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Недопустимое_число_знаков_отвергается(int digits)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JsNumber.ToFixed(1.0, digits));
    }

    private static JsNumberGolden Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "golden", "jsnumber.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Нет golden-файла jsnumber.json. Собрать: node tools/golden/generate.mjs", path);

        return JsonSerializer.Deserialize<JsNumberGolden>(
            File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Пустой golden-файл jsnumber.json");
    }

    internal sealed record JsNumberGolden(List<ToFixedCase> ToFixed, List<RoundCase> Round);
    internal sealed record ToFixedCase(double Value, int Digits, string Expected);
    internal sealed record RoundCase(double Value, double Expected);
}
