using System.Text;
using Domeo.Calculators.Electrical;
using Domeo.Calculators.Lighting;
using Domeo.Calculators.Reference;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("Калькуляторы calcal.ru — порт на C#");
Console.WriteLine("Справочники сняты 19.08.2026");
Console.WriteLine();

// ---------- освещённость ----------
Header("ОСВЕЩЁННОСТЬ ПОМЕЩЕНИЯ");

var baseline = new LightingInput(
    Area: 18, Height: 2.7, RoomType: "living", WallColor: "light",
    LampType: "led", FixtureWatt: 12, HoursPerDay: 6, Tariff: 6.5);

Console.WriteLine("Дефолтный сценарий — те же значения, что показывает сайт при открытии:");
Console.WriteLine("  гостиная 18 м², потолок 2.7 м, светлые стены, LED 12 Вт, 6 ч/сут, 6.5 руб/кВт·ч");
Console.WriteLine();
PrintLighting(baseline);

Console.WriteLine();
Console.WriteLine("Тот же расчёт при других вводных:");
Console.WriteLine();

PrintLightingRow("тёмные стены", baseline with { WallColor = "dark" });
PrintLightingRow("потолок 3.2 м", baseline with { Height = 3.2 });
PrintLightingRow("кабинет 300 лк", baseline with { RoomType = "office" });
PrintLightingRow("лампы накаливания", baseline with { LampType = "incandescent" });
PrintLightingRow("всё сразу, 40 м²", baseline with
{
    Area = 40, Height = 3.2, RoomType = "office", WallColor = "dark", LampType = "incandescent",
});

// ---------- электрофурнитура ----------
Console.WriteLine();
Header("ЭЛЕКТРОФУРНИТУРА НА КВАРТИРУ");

IReadOnlyDictionary<string, RoomState> rooms = ElectricalCalculator.DefaultRooms();
Console.WriteLine("Дефолтный набор комнат (кухня, гостиная, спальня, ванная, прихожая):");
Console.WriteLine();

foreach (string tier in ReferenceData.PriceTiers.Keys)
{
    ElectricalResult result = ElectricalCalculator.Calculate(rooms, tier);
    Console.WriteLine($"  {result.TierLabel,-28} {result.TotalCost,10:N0} ₽");
}

Console.WriteLine();
ElectricalResult budget = ElectricalCalculator.Calculate(rooms, "budget");

Console.WriteLine($"Разбивка по комнатам ({budget.TierLabel}):");
Console.WriteLine();
Console.WriteLine($"  {"Комната",-12}{"Розетки",9}{"Выкл.",7}{"USB",5}{"ТВ",4}{"Инет",6}{"Кабель",8}{"Стоимость",12}");
Console.WriteLine("  " + new string('-', 61));

foreach (RoomBreakdown room in budget.RoomBreakdown)
{
    Console.WriteLine($"  {room.Label,-12}{room.Outlets,9}{room.Switches,7}{room.Usb,5}" +
                      $"{room.Tv,4}{room.Internet,6}{room.Cable + " м",8}{room.Cost,12:N0}");
}

Console.WriteLine("  " + new string('-', 61));
Console.WriteLine($"  {"Итого",-12}{budget.TotalOutlets,9}{budget.TotalSwitches,7}{budget.TotalUsb,5}" +
                  $"{budget.TotalTv,4}{budget.TotalInternet,6}{budget.TotalCableMeters + " м",8}{budget.TotalCost,12:N0}");
Console.WriteLine();
Console.WriteLine($"  Подрозетников: {budget.TotalBoxes} шт., рамок: {budget.TotalFrames} шт.");
Console.WriteLine("  (в оригинале это одна и та же величина — число точек)");

Console.WriteLine();
Header("СПРАВОЧНИКИ");
Console.WriteLine($"  типов помещений (освещение): {ReferenceData.RoomTypes.Count}");
Console.WriteLine($"  цветов стен: {ReferenceData.WallColors.Count}, типов ламп: {ReferenceData.LampTypes.Count}");
Console.WriteLine($"  пресетов светильников: {ReferenceData.FixturePresets.Count}");
Console.WriteLine($"  комнат (электрика): {ReferenceData.RoomPresets.Count}, ценовых уровней: {ReferenceData.PriceTiers.Count}");

return 0;

void Header(string title)
{
    Console.WriteLine(new string('=', 63));
    Console.WriteLine("  " + title);
    Console.WriteLine(new string('=', 63));
    Console.WriteLine();
}

void PrintLighting(LightingInput input)
{
    LightingResult? r = LightingCalculator.Calculate(input);
    if (r is null)
    {
        Console.WriteLine("  расчёт невозможен при таких вводных");
        return;
    }

    Console.WriteLine($"  Норма для «{r.RoomName}»      {r.RequiredLux} лк");
    Console.WriteLine($"  Нужный световой поток       {r.RequiredLumens} лм");
    Console.WriteLine($"  Поток одного светильника    {r.FixtureLumen} лм");
    Console.WriteLine($"  Светильников                {r.FixtureCount} шт. по {r.FixtureWatt} Вт");
    Console.WriteLine($"  Суммарная мощность          {r.TotalWatt} Вт");
    Console.WriteLine($"  Расход                      {r.MonthlyKwhFormatted} кВт·ч/мес");
    Console.WriteLine($"  Стоимость                   {r.MonthlyCostFormatted} ₽/мес");
    Console.WriteLine($"  Коэффициент высоты          {r.HeightCoeff}");
}

void PrintLightingRow(string label, LightingInput input)
{
    LightingResult? r = LightingCalculator.Calculate(input);
    if (r is null)
    {
        Console.WriteLine($"  {label,-22} расчёт невозможен");
        return;
    }

    Console.WriteLine($"  {label,-22}{r.RequiredLumens,7} лм  →{r.FixtureCount,3} шт." +
                      $"{r.TotalWatt,6} Вт{r.MonthlyCostFormatted,7} ₽/мес");
}
