using System.Diagnostics;
using System.Text;

namespace Domeo.Calculators.Web.Reporting;

/// <summary>
/// Печатает саму страницу калькулятора в PDF headless-браузером.
/// </summary>
/// <remarks>
/// Отчёт должен быть той же вёрсткой, что и на экране, — рисовать его руками
/// значит поддерживать вторую разметку и всё равно расходиться с оригиналом.
/// Поэтому PDF снимает Chromium с той же HTML и той же CSS: состояние
/// калькулятора передаётся странице в адресе, а печатные правила лишь
/// подгоняют ширину под лист.
/// </remarks>
public sealed class PagePdfRenderer(IConfiguration configuration, ILogger<PagePdfRenderer> logger)
{
    private static readonly string[] BrowserCandidates =
    [
        "chromium", "chromium-browser", "google-chrome", "chrome",
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files\Chromium\Application\chrome.exe",
    ];

    /// <param name="pagePath">Путь страницы внутри сайта: пусто — корневой бланк, иначе «lead».</param>
    public async Task<byte[]> RenderAsync(string stateJson, string pagePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateJson);

        string browser = ResolveBrowser();
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

        // Путь приходит снаружи, поэтому берём только имя из белого списка —
        // иначе браузер можно было бы отправить на произвольный адрес.
        string page = string.IsNullOrEmpty(pagePath) ? string.Empty : pagePath.Trim('/') + "/";
        string url = $"{SelfUrl()}/{page}?print={Uri.EscapeDataString(token)}";

        string workDir = Path.Combine(Path.GetTempPath(), "calc-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        string output = Path.Combine(workDir, "report.pdf");

        try
        {
            var startInfo = new ProcessStartInfo(browser)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            foreach (string argument in new[]
            {
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",                 // контейнер и так без привилегий
                "--disable-dev-shm-usage",      // /dev/shm в контейнере крохотный
                "--hide-scrollbars",
                "--no-pdf-header-footer",       // без колонтитулов с адресом и датой
                "--virtual-time-budget=15000",  // дождаться расчётов, не завися от реального времени
                "--run-all-compositor-stages-before-draw",
                $"--user-data-dir={Path.Combine(workDir, "profile")}",
                $"--print-to-pdf={output}",
                url,
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Не удалось запустить браузер для печати.");

            Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw new TimeoutException("Браузер не успел напечатать страницу за 60 секунд.");
            }

            if (!File.Exists(output))
            {
                logger.LogError("Печать не дала файла. Вывод браузера: {Error}", await stderr);
                throw new InvalidOperationException("Браузер не создал PDF.");
            }

            return await File.ReadAllBytesAsync(output, cancellationToken);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); }
            catch (IOException) { /* временный каталог подчистит система */ }
        }
    }

    /// <summary>Адрес, по которому приложение доступно самому себе.</summary>
    private string SelfUrl()
    {
        string? configured = configuration["Report:SelfUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');

        // ASPNETCORE_URLS вида http://+:8080 — подставляем петлевой адрес.
        string urls = configuration["ASPNETCORE_URLS"] ?? "http://localhost:5099";
        string first = urls.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        first = first.Replace("+", "127.0.0.1").Replace("*", "127.0.0.1").Replace("[::]", "127.0.0.1");

        string pathBase = (configuration["PathBase"] ?? string.Empty).Trim().TrimEnd('/');
        if (pathBase.Length > 0 && !pathBase.StartsWith('/')) pathBase = "/" + pathBase;

        return first.TrimEnd('/') + pathBase;
    }

    private string ResolveBrowser()
    {
        string? configured = configuration["Report:BrowserPath"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        foreach (string candidate in BrowserCandidates)
        {
            if (Path.IsPathRooted(candidate))
            {
                if (File.Exists(candidate)) return candidate;
                continue;
            }

            if (WhichExists(candidate)) return candidate;
        }

        throw new InvalidOperationException(
            "Не найден Chromium для печати PDF. Укажите путь в Report:BrowserPath.");
    }

    private static bool WhichExists(string command)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return false;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(Path.Combine(directory, command))) return true;
            if (OperatingSystem.IsWindows() && File.Exists(Path.Combine(directory, command + ".exe"))) return true;
        }

        return false;
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* уже завершился */ }
    }
}
