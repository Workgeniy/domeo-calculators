using System.Text.Json;
using System.Text.Json.Serialization;
using Domeo.Calculators.Electrical;
using Domeo.Calculators.Lighting;
using Domeo.Calculators.Reference;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Локально порт фиксируем, чтобы ссылка была предсказуемой между запусками.
// В контейнере адрес задаёт ASPNETCORE_URLS — тогда не перебиваем.
if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://localhost:5099");
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

WebApplication app = builder.Build();

// За обратным прокси приложение живёт на префиксе (например /calc).
// Локально PathBase пуст — адреса остаются от корня.
string pathBase = (builder.Configuration["PathBase"] ?? string.Empty).Trim().TrimEnd('/');
if (pathBase.Length > 0)
{
    // Ведущий слеш обязателен, иначе PathString падает. Дописываем сами:
    // значение легко приезжает без него из окружения.
    if (!pathBase.StartsWith('/')) pathBase = "/" + pathBase;
    app.UsePathBase(pathBase);
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Справочники отдаём целиком — страница не хранит своей копии данных.
app.MapGet("/api/reference", () => Results.Ok(new
{
    lighting = new
    {
        roomTypes = ReferenceData.RoomTypes,
        wallColors = ReferenceData.WallColors,
        lampTypes = ReferenceData.LampTypes,
        fixturePresets = ReferenceData.FixturePresets,
    },
    electrical = new
    {
        roomOrder = ReferenceData.RoomOrder,
        roomPresets = ReferenceData.RoomOrder.Select(key => new
        {
            key,
            preset = ReferenceData.RoomPresets[key],
        }),
        priceTiers = ReferenceData.PriceTiers.Select(kv => new { key = kv.Key, tier = kv.Value }),
        defaultRooms = ElectricalCalculator.DefaultRooms(),
    },
}));

// Считает библиотека — та же, что покрыта golden-тестами.
app.MapPost("/api/lighting", (LightingInput input) =>
{
    LightingResult? result = LightingCalculator.Calculate(input);
    return result is null
        ? Results.Ok(new { ok = false, reason = "Площадь, высота и мощность должны быть больше нуля." })
        : Results.Ok(new { ok = true, result });
});

app.MapPost("/api/electrical", (ElectricalRequest request) =>
{
    if (request.Rooms is null || string.IsNullOrEmpty(request.Tier))
        return Results.BadRequest(new { ok = false, reason = "Нужны комнаты и ценовой уровень." });

    if (!ReferenceData.PriceTiers.ContainsKey(request.Tier))
        return Results.BadRequest(new { ok = false, reason = $"Неизвестный ценовой уровень: {request.Tier}" });

    ElectricalResult result = ElectricalCalculator.Calculate(request.Rooms, request.Tier);
    return Results.Ok(new { ok = true, result });
});

app.Run();

internal sealed record ElectricalRequest(Dictionary<string, RoomState> Rooms, string Tier);
