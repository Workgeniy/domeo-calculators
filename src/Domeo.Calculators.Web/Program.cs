using System.Text.Json;
using System.Text.Json.Serialization;
using Domeo.Calculators.Reference;
using Domeo.Calculators.Spec;
using Domeo.Calculators.Web.Reporting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Локально порт фиксируем, чтобы ссылка была предсказуемой между запусками.
// В контейнере адрес задаёт ASPNETCORE_URLS — тогда не перебиваем.
if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://localhost:5099");
}

builder.Services.AddSingleton<PagePdfRenderer>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    // Уровень комфорта ездит строкой: "Standard", а не 1.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

WebApplication app = builder.Build();

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

// Отчёт — это сама страница опросника, снятая в PDF headless-браузером.
// Ответы приходят как есть: их состав задаёт wwwroot/form.json, а не сервер.
app.MapPost("/api/report/pdf", async (JsonElement answers, PagePdfRenderer renderer, CancellationToken token) =>
{
    if (answers.ValueKind != JsonValueKind.Object)
        return Results.BadRequest(new { ok = false, reason = "Ожидался объект с ответами." });

    string state = answers.GetRawText();

    // Состояние едет в адресе, поэтому ограничиваем размер.
    const int limit = 64 * 1024;
    if (state.Length > limit)
        return Results.BadRequest(new { ok = false, reason = "Слишком много данных в опроснике." });

    byte[] file = await renderer.RenderAsync(state, token);
    string name = $"peredacha-v-komplektaciyu-{DateTimeOffset.Now:yyyy-MM-dd-HHmm}.pdf";
    return Results.File(file, "application/pdf", name);
});

// Справочник нормировочной модели: типы помещений и коэффициенты. Нужен интерфейсу,
// чтобы построить форму и предзаполнить её по типу помещения.
app.MapGet("/api/spec/reference", () => new
{
    defaultProfile = SpecReference.Default.Key,
    coefficients = SpecReference.Coefficients,
    profiles = SpecReference.Profiles.Values.Select(profile => new
    {
        key = profile.Key,
        label = profile.Label,
        rooms = profile.RoomOrder.Select(key => new { key, room = profile.Rooms[key] }),
    }),
});

// Расчёт спецификации по помещениям. Цен не считает — это количества.
app.MapPost("/api/spec", (SpecRequest request) =>
{
    if (request.Rooms is not { Count: > 0 })
        return Results.BadRequest(new { ok = false, reason = "Нужно хотя бы одно помещение." });

    const int limit = 100;
    if (request.Rooms.Count > limit)
        return Results.BadRequest(new { ok = false, reason = $"Не больше {limit} помещений за раз." });

    try
    {
        return Results.Ok(SpecCalculator.Calculate(request.Rooms, request.Profile));
    }
    catch (ArgumentException error)
    {
        // Неизвестный тип помещения или профиль — это ошибка запроса, а не сервера.
        // Хвост «(Parameter '…')» из ArgumentException наружу не отдаём.
        string reason = error.Message.Split(" (Parameter")[0];
        return Results.BadRequest(new { ok = false, reason });
    }
});

app.Run();

/// <param name="Profile">Профиль расчёта; пусто — модель Domeo.</param>
/// <param name="Rooms">Помещения проекта.</param>
internal sealed record SpecRequest(string? Profile, List<SpecRoomInput>? Rooms);
