using System.Globalization;
using Domeo.Calculators.Electrical;
using Domeo.Calculators.Lighting;
using Domeo.Calculators.Reference;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Domeo.Calculators.Web.Reporting;

/// <summary>
/// Отчёт в PDF: одна страница A4 с обоими расчётами, пригодная для передачи.
/// </summary>
public static class PdfReport
{
    private const double PageWidth = 595.28;   // A4 в пунктах
    private const double PageHeight = 841.89;
    private const double Margin = 42;
    private const double ContentWidth = PageWidth - Margin * 2;
    private const double BottomLimit = PageHeight - Margin;

    private static readonly XColor Ink = XColor.FromArgb(0x0A, 0x0A, 0x0A);
    private static readonly XColor Muted = XColor.FromArgb(0x6B, 0x6B, 0x72);
    private static readonly XColor Line = XColor.FromArgb(0xDD, 0xDD, 0xE0);
    private static readonly XColor Yellow = XColor.FromArgb(0xFA, 0xCC, 0x15);
    private static readonly XColor Pale = XColor.FromArgb(0xFE, 0xF9, 0xC3);

    private static readonly object FontGate = new();
    private static bool _fontsReady;

    public static byte[] Build(
        LightingInput lightingInput,
        LightingResult? lighting,
        IReadOnlyDictionary<string, RoomState> rooms,
        ElectricalResult electrical,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(lightingInput);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(electrical);

        EnsureFonts();

        using var document = new PdfDocument();
        document.Info.Title = "Расчёт освещения и электрики";
        document.Info.Creator = "Domeo";

        var canvas = new Canvas(document);

        canvas.Title("Расчёт освещения и электрики");
        canvas.Caption("Сформировано " + generatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU")));
        canvas.Gap(14);

        DrawLighting(canvas, lightingInput, lighting);
        canvas.Gap(18);
        DrawElectrical(canvas, rooms, electrical);

        canvas.Gap(16);
        canvas.Note("Цены справочные, зафиксированы на 19.08.2026. Рамки посчитаны как одинарные — "
                  + "сборка точек в блоки по 2–5 постов обойдётся дешевле. Нормы освещённости — СП 52.13330.2016.");

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static void EnsureFonts()
    {
        if (_fontsReady) return;
        lock (FontGate)
        {
            if (_fontsReady) return;
            GlobalFontSettings.FontResolver ??= EmbeddedFontResolver.Instance;
            _fontsReady = true;
        }
    }

    private static void DrawLighting(Canvas canvas, LightingInput input, LightingResult? result)
    {
        canvas.Section("Освещённость помещения");

        string room = ReferenceData.RoomTypes.FirstOrDefault(r => r.Id == input.RoomType)?.Name ?? input.RoomType;
        string wall = ReferenceData.WallColors.FirstOrDefault(w => w.Id == input.WallColor)?.Name ?? input.WallColor;
        string lamp = ReferenceData.LampTypes.FirstOrDefault(l => l.Id == input.LampType)?.Name ?? input.LampType;

        canvas.SubHeading("Исходные данные");
        canvas.Columns(
            [("Тип помещения", room), ("Площадь", Num(input.Area) + " м²"),
             ("Высота потолка", Num(input.Height) + " м"), ("Цвет стен", wall)],
            [("Тип лампы", lamp), ("Мощность светильника", input.FixtureWatt + " Вт"),
             ("Часов в день", input.HoursPerDay.ToString(CultureInfo.InvariantCulture)),
             ("Тариф", Num(input.Tariff) + " ₽/кВт·ч")]);

        if (result is null)
        {
            canvas.Gap(6);
            canvas.Note("Расчёт невозможен: площадь, высота и мощность должны быть больше нуля.");
            return;
        }

        canvas.Gap(10);
        canvas.Banner(result.FixtureCount + " " + Plural(result.FixtureCount, "светильник", "светильника", "светильников")
                    + " по " + result.FixtureWatt + " Вт");
        canvas.Gap(8);

        canvas.SubHeading("Результат");
        canvas.Columns(
            [("Норма освещённости", result.RequiredLux + " лк"),
             ("Необходимый поток", result.RequiredLumens + " лм"),
             ("Поток светильника", result.FixtureLumen + " лм"),
             ("Поправка на высоту", "×" + Num(result.HeightCoeff))],
            [("Общая мощность", result.TotalWatt + " Вт"),
             ("Площадь на светильник", result.AreaPerFixtureFormatted + " м²"),
             ("Расход в месяц", result.MonthlyKwhFormatted + " кВт·ч"),
             ("Стоимость в месяц", result.MonthlyCostFormatted + " ₽")]);
    }

    private static void DrawElectrical(Canvas canvas, IReadOnlyDictionary<string, RoomState> rooms, ElectricalResult result)
    {
        canvas.Section("Электрофурнитура на квартиру");
        canvas.SubHeading("Ценовой уровень: " + result.TierLabel);

        double[] widths = [120, 52, 52, 60, 40, 34, 42, 52, 59];
        string[] headers = ["Комната", "Площадь", "Розетки", "Выключ.", "USB", "ТВ", "Инет", "Кабель", "Сумма, ₽"];
        canvas.TableHeader(headers, widths);

        foreach (RoomBreakdown b in result.RoomBreakdown)
        {
            double area = rooms.TryGetValue(b.Key, out RoomState? state) ? state.Area : 0;
            canvas.TableRow(
                [b.Label, Num(area) + " м²", b.Outlets.ToString(CultureInfo.InvariantCulture),
                 b.Switches.ToString(CultureInfo.InvariantCulture), b.Usb.ToString(CultureInfo.InvariantCulture),
                 b.Tv.ToString(CultureInfo.InvariantCulture), b.Internet.ToString(CultureInfo.InvariantCulture),
                 b.Cable + " м", Money(b.Cost)],
                widths, bold: false);
        }

        if (result.RoomBreakdown.Count == 0)
        {
            canvas.Note("Все комнаты выключены.");
            return;
        }

        canvas.TableRow(
            ["Итого", "", result.TotalOutlets.ToString(CultureInfo.InvariantCulture),
             result.TotalSwitches.ToString(CultureInfo.InvariantCulture),
             result.TotalUsb.ToString(CultureInfo.InvariantCulture),
             result.TotalTv.ToString(CultureInfo.InvariantCulture),
             result.TotalInternet.ToString(CultureInfo.InvariantCulture),
             result.TotalCableMeters + " м", Money(result.TotalCost)],
            widths, bold: true);

        canvas.Gap(12);
        canvas.Banner("Итого " + Money(result.TotalCost) + " ₽  ·  " + result.TierLabel);
        canvas.Gap(8);

        canvas.SubHeading("Комплектация");
        canvas.Columns(
            [("Розетки 220В", result.TotalOutlets + " шт."),
             ("Выключатели с проходными", result.TotalSwitches + " шт."),
             ("USB-розетки", result.TotalUsb + " шт."),
             ("ТВ-розетки", result.TotalTv + " шт.")],
            [("Интернет-розетки", result.TotalInternet + " шт."),
             ("Подрозетники", result.TotalBoxes + " шт."),
             ("Рамки", result.TotalFrames + " шт."),
             ("Кабель", result.TotalCableMeters + " м")]);
    }

    private static string Num(double value) =>
        value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Money(int value) => value.ToString("#,0", CultureInfo.GetCultureInfo("ru-RU"));

    /// <summary>Русское склонение по числу: 1 светильник, 2 светильника, 5 светильников.</summary>
    private static string Plural(int count, string one, string few, string many)
    {
        int lastTwo = count % 100;
        int last = count % 10;

        if (last == 1 && lastTwo != 11) return one;
        if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return few;
        return many;
    }

    /// <summary>Курсор по странице: рисует блоки сверху вниз и сам заводит новую страницу.</summary>
    private sealed class Canvas
    {
        private readonly PdfDocument _document;
        private XGraphics _gfx;
        private double _y;

        private readonly XFont _title = new(EmbeddedFontResolver.Family, 18, XFontStyleEx.Bold);
        private readonly XFont _section = new(EmbeddedFontResolver.Family, 13, XFontStyleEx.Bold);
        private readonly XFont _sub = new(EmbeddedFontResolver.Family, 10, XFontStyleEx.Bold);
        private readonly XFont _body = new(EmbeddedFontResolver.Family, 9, XFontStyleEx.Regular);
        private readonly XFont _bodyBold = new(EmbeddedFontResolver.Family, 9, XFontStyleEx.Bold);
        private readonly XFont _small = new(EmbeddedFontResolver.Family, 7.5, XFontStyleEx.Regular);
        private readonly XFont _banner = new(EmbeddedFontResolver.Family, 15, XFontStyleEx.Bold);

        public Canvas(PdfDocument document)
        {
            _document = document;
            _gfx = NewPage();
            _y = Margin;
        }

        private XGraphics NewPage()
        {
            PdfPage page = _document.AddPage();
            page.Width = XUnit.FromPoint(PageWidth);
            page.Height = XUnit.FromPoint(PageHeight);
            return XGraphics.FromPdfPage(page);
        }

        private void Reserve(double height)
        {
            if (_y + height <= BottomLimit) return;
            _gfx.Dispose();
            _gfx = NewPage();
            _y = Margin;
        }

        public void Gap(double height) => _y += height;

        public void Title(string text)
        {
            Reserve(26);
            _gfx.DrawString(text, _title, new XSolidBrush(Ink), new XRect(Margin, _y, ContentWidth, 24), XStringFormats.TopLeft);
            _y += 24;
        }

        public void Caption(string text)
        {
            Reserve(14);
            _gfx.DrawString(text, _small, new XSolidBrush(Muted), new XRect(Margin, _y, ContentWidth, 12), XStringFormats.TopLeft);
            _y += 12;
        }

        public void Section(string text)
        {
            Reserve(30);
            _gfx.DrawString(text, _section, new XSolidBrush(Ink), new XRect(Margin, _y, ContentWidth, 18), XStringFormats.TopLeft);
            _y += 18;
            _gfx.DrawLine(new XPen(Yellow, 1.6), Margin, _y, Margin + ContentWidth, _y);
            _y += 8;
        }

        public void SubHeading(string text)
        {
            Reserve(18);
            _gfx.DrawString(text, _sub, new XSolidBrush(Ink), new XRect(Margin, _y, ContentWidth, 14), XStringFormats.TopLeft);
            _y += 15;
        }

        /// <summary>Пары «подпись — значение» в две колонки.</summary>
        public void Columns((string Label, string Value)[] left, (string Label, string Value)[] right)
        {
            int rows = Math.Max(left.Length, right.Length);
            double columnWidth = (ContentWidth - 18) / 2;

            for (int i = 0; i < rows; i++)
            {
                Reserve(15);
                if (i < left.Length) Pair(left[i], Margin, columnWidth);
                if (i < right.Length) Pair(right[i], Margin + columnWidth + 18, columnWidth);
                _y += 14;
            }
        }

        private void Pair((string Label, string Value) pair, double x, double width)
        {
            _gfx.DrawString(pair.Label, _body, new XSolidBrush(Muted), new XRect(x, _y, width, 12), XStringFormats.TopLeft);
            _gfx.DrawString(pair.Value, _bodyBold, new XSolidBrush(Ink), new XRect(x, _y, width, 12), XStringFormats.TopRight);
            _gfx.DrawLine(new XPen(Line, 0.5), x, _y + 12.5, x + width, _y + 12.5);
        }

        /// <summary>Жёлтая плашка с главной цифрой.</summary>
        public void Banner(string text)
        {
            Reserve(34);
            _gfx.DrawRectangle(new XSolidBrush(Yellow), new XRect(Margin, _y, ContentWidth, 30));
            _gfx.DrawString(text, _banner, new XSolidBrush(Ink), new XRect(Margin, _y, ContentWidth, 30), XStringFormats.Center);
            _y += 30;
        }

        public void TableHeader(string[] cells, double[] widths)
        {
            Reserve(20);
            _gfx.DrawRectangle(new XSolidBrush(Pale), new XRect(Margin, _y, ContentWidth, 16));
            DrawCells(cells, widths, _bodyBold, Ink, 16);
            _y += 16;
        }

        public void TableRow(string[] cells, double[] widths, bool bold)
        {
            Reserve(18);
            if (bold)
            {
                _gfx.DrawLine(new XPen(Yellow, 1.2), Margin, _y, Margin + ContentWidth, _y);
                _gfx.DrawRectangle(new XSolidBrush(Pale), new XRect(Margin, _y, ContentWidth, 15));
            }

            DrawCells(cells, widths, bold ? _bodyBold : _body, Ink, 15);
            if (!bold) _gfx.DrawLine(new XPen(Line, 0.5), Margin, _y + 15, Margin + ContentWidth, _y + 15);
            _y += 15;
        }

        private void DrawCells(string[] cells, double[] widths, XFont font, XColor color, double height)
        {
            double x = Margin;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                // Первая колонка по левому краю, числовые — по правому.
                XStringFormat format = i == 0 ? XStringFormats.CenterLeft : XStringFormats.CenterRight;
                var rect = new XRect(x + 4, _y, widths[i] - 8, height);
                _gfx.DrawString(cells[i], font, new XSolidBrush(color), rect, format);
                x += widths[i];
            }
        }

        public void Note(string text)
        {
            // Простой перенос по словам: строк мало, полноценная типографика тут не нужна.
            var words = text.Split(' ');
            var line = new System.Text.StringBuilder();

            foreach (string word in words)
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (_gfx.MeasureString(candidate, _small).Width > ContentWidth)
                {
                    Flush(line.ToString());
                    line.Clear().Append(word);
                }
                else
                {
                    line.Clear().Append(candidate);
                }
            }

            if (line.Length > 0) Flush(line.ToString());

            void Flush(string value)
            {
                Reserve(12);
                _gfx.DrawString(value, _small, new XSolidBrush(Muted), new XRect(Margin, _y, ContentWidth, 10), XStringFormats.TopLeft);
                _y += 10;
            }
        }
    }
}
