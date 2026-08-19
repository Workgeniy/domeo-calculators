using ClosedXML.Excel;
using Domeo.Calculators.Electrical;
using Domeo.Calculators.Lighting;
using Domeo.Calculators.Reference;

namespace Domeo.Calculators.Web.Reporting;

/// <summary>
/// Отчёт в .xlsx для передачи электрику или сметчику: два листа —
/// освещение и электрофурнитура. Цифры те же, что на странице.
/// </summary>
public static class XlsxReport
{
    private static readonly XLColor Accent = XLColor.FromArgb(0xFA, 0xCC, 0x15);
    private static readonly XLColor Pale = XLColor.FromArgb(0xFE, 0xF9, 0xC3);
    private static readonly XLColor Ink = XLColor.FromArgb(0x0A, 0x0A, 0x0A);

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

        using var workbook = new XLWorkbook();
        BuildLightingSheet(workbook, lightingInput, lighting, generatedAt);
        BuildElectricalSheet(workbook, rooms, electrical, generatedAt);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildLightingSheet(
        XLWorkbook workbook, LightingInput input, LightingResult? result, DateTimeOffset at)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add("Освещение");
        int row = Title(sheet, "Расчёт освещённости помещения", "по нормам СП 52.13330.2016", at);

        row = Section(sheet, row, "Исходные данные");

        string roomName = ReferenceData.RoomTypes.FirstOrDefault(r => r.Id == input.RoomType)?.Name ?? input.RoomType;
        string wallName = ReferenceData.WallColors.FirstOrDefault(w => w.Id == input.WallColor)?.Name ?? input.WallColor;
        string lampName = ReferenceData.LampTypes.FirstOrDefault(l => l.Id == input.LampType)?.Name ?? input.LampType;

        row = Pair(sheet, row, "Тип помещения", roomName);
        row = Pair(sheet, row, "Площадь, м²", input.Area);
        row = Pair(sheet, row, "Высота потолка, м", input.Height);
        row = Pair(sheet, row, "Цвет стен", wallName);
        row = Pair(sheet, row, "Тип лампы", lampName);
        row = Pair(sheet, row, "Мощность светильника, Вт", input.FixtureWatt);
        row = Pair(sheet, row, "Часов в день", input.HoursPerDay);
        row = Pair(sheet, row, "Тариф, ₽/кВт·ч", input.Tariff);

        row++;

        if (result is null)
        {
            sheet.Cell(row, 1).Value = "Расчёт невозможен: проверьте площадь, высоту и мощность.";
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.DarkRed;
            Finish(sheet);
            return;
        }

        row = Section(sheet, row, "Результат");
        row = Pair(sheet, row, "Норма освещённости, лк", result.RequiredLux);
        row = Pair(sheet, row, "Необходимый световой поток, лм", result.RequiredLumens);
        row = Pair(sheet, row, "Поток одного светильника, лм", result.FixtureLumen);
        row = Highlight(sheet, row, "Количество светильников, шт.", result.FixtureCount);
        row = Pair(sheet, row, "Поправка на высоту", result.HeightCoeff);
        row = Pair(sheet, row, "Общая мощность, Вт", result.TotalWatt);
        row = Pair(sheet, row, "Площадь на светильник, м²", result.AreaPerFixtureFormatted);
        row = Pair(sheet, row, "Расход в месяц, кВт·ч", result.MonthlyKwhFormatted);
        row = Pair(sheet, row, "Стоимость в месяц, ₽", result.MonthlyCostFormatted);

        Finish(sheet);
    }

    private static void BuildElectricalSheet(
        XLWorkbook workbook, IReadOnlyDictionary<string, RoomState> rooms,
        ElectricalResult result, DateTimeOffset at)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add("Электрика");
        int row = Title(sheet, "Электрофурнитура на квартиру", "с учётом норм ПУЭ", at);

        row = Pair(sheet, row, "Ценовой уровень", result.TierLabel);
        row++;

        row = Section(sheet, row, "Разбивка по комнатам");

        string[] headers = ["Комната", "Площадь, м²", "Комнат", "Розетки", "Выключатели", "USB", "ТВ", "Интернет", "Кабель, м", "Стоимость, ₽"];
        for (int i = 0; i < headers.Length; i++)
        {
            IXLCell cell = sheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = Pale;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        row++;
        int firstDataRow = row;

        foreach (RoomBreakdown b in result.RoomBreakdown)
        {
            double area = rooms.TryGetValue(b.Key, out RoomState? state) ? state.Area : 0;

            sheet.Cell(row, 1).Value = b.Label;
            sheet.Cell(row, 2).Value = area;
            sheet.Cell(row, 3).Value = b.Count;
            sheet.Cell(row, 4).Value = b.Outlets;
            sheet.Cell(row, 5).Value = b.Switches;
            sheet.Cell(row, 6).Value = b.Usb;
            sheet.Cell(row, 7).Value = b.Tv;
            sheet.Cell(row, 8).Value = b.Internet;
            sheet.Cell(row, 9).Value = b.Cable;
            sheet.Cell(row, 10).Value = b.Cost;
            sheet.Cell(row, 10).Style.NumberFormat.Format = "# ##0";
            row++;
        }

        if (result.RoomBreakdown.Count == 0)
        {
            sheet.Cell(row, 1).Value = "Все комнаты выключены";
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
            row++;
        }

        // Итоги — формулами, чтобы файл оставался живым при правке позиций.
        int lastDataRow = row - 1;
        sheet.Cell(row, 1).Value = "Итого";
        if (lastDataRow >= firstDataRow)
        {
            foreach (int col in new[] { 4, 5, 6, 7, 8, 9, 10 })
            {
                string letter = XLHelper.GetColumnLetterFromNumber(col);
                sheet.Cell(row, col).FormulaA1 = $"SUM({letter}{firstDataRow}:{letter}{lastDataRow})";
            }
        }

        IXLRange totals = sheet.Range(row, 1, row, headers.Length);
        totals.Style.Font.Bold = true;
        totals.Style.Fill.BackgroundColor = Pale;
        totals.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        totals.Style.Border.TopBorderColor = Accent;
        sheet.Cell(row, 10).Style.NumberFormat.Format = "# ##0";

        row += 2;
        row = Section(sheet, row, "Комплектация");
        row = Pair(sheet, row, "Розетки 220В, шт.", result.TotalOutlets);
        row = Pair(sheet, row, "Выключатели (с проходными), шт.", result.TotalSwitches);
        row = Pair(sheet, row, "USB-розетки, шт.", result.TotalUsb);
        row = Pair(sheet, row, "ТВ-розетки, шт.", result.TotalTv);
        row = Pair(sheet, row, "Интернет-розетки, шт.", result.TotalInternet);
        row = Pair(sheet, row, "Подрозетники, шт.", result.TotalBoxes);
        row = Pair(sheet, row, "Рамки, шт.", result.TotalFrames);
        row = Pair(sheet, row, "Кабель, м", result.TotalCableMeters);
        row = Highlight(sheet, row, "Итого, ₽", result.TotalCost);

        row += 2;
        sheet.Cell(row, 1).Value =
            "Цены справочные, зафиксированы на 19.08.2026. Рамки посчитаны как одинарные: "
            + "сборка точек в блоки по 2–5 постов обойдётся дешевле.";
        sheet.Cell(row, 1).Style.Font.FontSize = 9;
        sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;

        sheet.Columns(1, headers.Length).AdjustToContents();
        sheet.Column(1).Width = Math.Max(sheet.Column(1).Width, 30);
        sheet.SheetView.FreezeRows(firstDataRow - 1);
    }

    // ---------- вспомогательное оформление ----------

    private static int Title(IXLWorksheet sheet, string title, string subtitle, DateTimeOffset at)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 15;
        sheet.Cell(1, 1).Style.Font.FontColor = Ink;

        sheet.Cell(2, 1).Value = subtitle;
        sheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        sheet.Cell(3, 1).Value = "Сформировано " + at.ToString("dd.MM.yyyy HH:mm");
        sheet.Cell(3, 1).Style.Font.FontSize = 9;
        sheet.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;

        return 5;
    }

    private static int Section(IXLWorksheet sheet, int row, string name)
    {
        sheet.Cell(row, 1).Value = name;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        sheet.Cell(row, 1).Style.Border.BottomBorderColor = Accent;
        return row + 1;
    }

    private static int Pair(IXLWorksheet sheet, int row, string label, XLCellValue value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 2).Value = value;
        return row + 1;
    }

    private static int Highlight(IXLWorksheet sheet, int row, string label, XLCellValue value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 2).Value = value;
        IXLRange range = sheet.Range(row, 1, row, 2);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = Pale;
        return row + 1;
    }

    private static void Finish(IXLWorksheet sheet)
    {
        sheet.Columns(1, 2).AdjustToContents();
        sheet.Column(1).Width = Math.Max(sheet.Column(1).Width, 32);
    }
}
