using System.Reflection;
using PdfSharp.Fonts;

namespace Domeo.Calculators.Web.Reporting;

/// <summary>
/// Отдаёт PDFsharp встроенные DejaVu Sans.
/// </summary>
/// <remarks>
/// В образе dotnet/aspnet системных шрифтов нет вовсе, а те, что есть на машине
/// разработчика под Windows, кириллицу в PDF кладут по-разному. Поэтому шрифт
/// свой и одинаковый везде — иначе отчёт со стенда и локальный разъедутся.
/// </remarks>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string Family = "DejaVu Sans";

    private const string Regular = "Domeo.Calculators.Web.Fonts.DejaVuSans.ttf";
    private const string Bold = "Domeo.Calculators.Web.Fonts.DejaVuSans-Bold.ttf";

    public static EmbeddedFontResolver Instance { get; } = new();

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (!string.Equals(familyName, Family, StringComparison.OrdinalIgnoreCase)) return null;
        return new FontResolverInfo(isBold ? Bold : Regular);
    }

    public byte[]? GetFont(string faceName)
    {
        Assembly assembly = typeof(EmbeddedFontResolver).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(faceName);
        if (stream is null) return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
