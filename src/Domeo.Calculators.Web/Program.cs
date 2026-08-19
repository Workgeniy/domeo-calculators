using System.Text.Json;
using System.Text.Json.Serialization;
using Domeo.Calculators.Electrical;
using Domeo.Calculators.Lighting;
using Domeo.Calculators.Reference;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Порт фиксируем, чтобы ссылка была предсказуемой между запусками.
builder.WebHost.UseUrls("http://localhost:5099");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

WebApplication app = builder.Build();

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
