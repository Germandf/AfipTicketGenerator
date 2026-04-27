using System.Diagnostics;
using AfipTicketGenerator;
using Microsoft.Playwright;

// Console
Console.WriteLine("Welcome to AfipTicketGenerator!");
Console.WriteLine("Write your CUIT");
var cuit = Console.ReadLine();
Console.WriteLine("Write your password");
var password = ConsoleExtensions.ReadPassword();
Console.WriteLine("How many days ago discounting from today do you want to generate? (default 5)");
var daysInput = Console.ReadLine();
if (string.IsNullOrWhiteSpace(daysInput)) daysInput = "5";
int.TryParse(daysInput, out var days);
Console.WriteLine("Show the generating process? y/n");
var headless = Console.ReadLine() == "n" ? true : false;

// Configuration
using var playwright = await Playwright.CreateAsync();
await using var browser = await LaunchChromiumAsync(playwright.Chromium, new BrowserTypeLaunchOptions
{
    Headless = headless,
    SlowMo = 50,
    Timeout = 15000,
});
await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
{
    AcceptDownloads = true,
});
var ticketsToPrint = new List<string>();
// Directory.CreateDirectory($"C:\\Boletas");

// Login
var page = await context.NewPageAsync();
await page.GotoAsync("https://auth.afip.gob.ar/contribuyente_/login.xhtml");
await page.FillAsync("input[name='F1:username']", cuit!);
await page.ClickAsync("input[name='F1:btnSiguiente']");
await page.FillAsync("input[name='F1:password']", password);
await page.ClickAsync("input[name='F1:btnIngresar']");

// Ticket Generating
for (int day = days; day >= 0; day--)
{
    // min 37600 - max 42300 - avg 39950 (~1.198.500 mensual)
    const int minValue = 8;
    const int maxValue = 10; // so its 9
    var products = new List<Product>()
    {
        new() { Name = "Tomate", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "600" },
        new() { Name = "Banana", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "480" },
        new() { Name = "Naranja", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "440" },
        new() { Name = "Manzana", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "600" },
        new() { Name = "Lechuga", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "400" },
        new() { Name = "Zanahoria", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "480" },
        new() { Name = "Cebolla", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "520" },
        new() { Name = "Mandarina", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "560" },
        new() { Name = "Limón", Quantity = Random.Shared.Next(minValue, maxValue).ToString(), Price = "620" },
    };
    var total = products.Sum(x => int.Parse(x.Quantity) * int.Parse(x.Price));

    Console.WriteLine($"Generating ticket for day {DateTime.Today.AddDays(-day).ToString("dd/MM/yyyy")} with a total of ${total}...");

    var newPage = await context.RunAndWaitForPageAsync(async () =>
    {
        await page.ClickAsync("text=Comprobantes en línea");
    });
    await newPage.WaitForLoadStateAsync();
    await newPage.ClickAsync("input[value='DE FRANCESCO LUIS']");
    await newPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
    await newPage.GotoAsync("https://fe.afip.gob.ar/rcel/jsp/buscarPtosVtas.do");
    await newPage.SelectOptionAsync("select[id='puntodeventa']", "2");
    await newPage.SelectOptionAsync("select[id='universocomprobante']", "2");
    await newPage.ClickAsync("input[value='Continuar >']");
    await newPage.FillAsync("input[name='fechaEmisionComprobante']", DateTime.Today.AddDays(-day).ToString("dd/MM/yyyy"));
    await newPage.SelectOptionAsync("select[id='idconcepto']", "1");
    await newPage.ClickAsync("input[value='Continuar >']");
    await newPage.SelectOptionAsync("select[id='idivareceptor']", "5");
    await newPage.SetCheckedAsync("input[id='formadepago1']", true);
    await newPage.ClickAsync("input[value='Continuar >']");
    for (int i = 0; i < products.Count; i++)
    {
        await newPage.FillAsync($"textarea[id='detalle_descripcion{i + 1}']", products[i].Name);
        await newPage.FillAsync($"input[id='detalle_cantidad{i + 1}']", products[i].Quantity);
        await newPage.SelectOptionAsync($"select[id='detalle_medida{i + 1}']", "1");
        await newPage.FillAsync($"input[id='detalle_precio{i + 1}']", products[i].Price);
        if (i < products.Count - 1)
            await newPage.ClickAsync("input[value='Agregar línea descripción']");
    }
    await newPage.ClickAsync("input[value='Continuar >']");
    newPage.Dialog += (_, dialog) => dialog.AcceptAsync();
    await newPage.ClickAsync("input[id='btngenerar']");
    await newPage.GetByRole(AriaRole.Button, new() { Name = "Confirmar", Exact = true }).ClickAsync();
    await newPage.WaitForSelectorAsync("b:text('Comprobante Generado')");

    Console.WriteLine($"Ticket generated");

    // No need to print pdfs no more
    /*
    var waitForDownloadTask = newPage.WaitForDownloadAsync();
    await newPage.ClickAsync("input[value='Imprimir...']");
    var file = await waitForDownloadTask;
    var filePath = $"C:\\Boletas\\{file.SuggestedFilename}";
    await file.SaveAsAsync(filePath);
    ticketsToPrint.Add(filePath);

    Console.WriteLine($"Ticket downloaded to {filePath}");
    */
}

/*
Console.WriteLine($"Starting printing process...");

// Ticket Printing
foreach (var ticketToPrintPath in ticketsToPrint)
{
    var arguments = @"/C pdfcmd command=""printpdf"" input=""" + ticketToPrintPath + @""" firstpage=""2"" lastpage=""2""";
    var process = Process.Start("CMD.exe", arguments);
    process.WaitForExit();
}
*/
Console.WriteLine($"Done!");

static async Task<IBrowser> LaunchChromiumAsync(IBrowserType chromium, BrowserTypeLaunchOptions options)
{
    try
    {
        return await chromium.LaunchAsync(options);
    }
    catch (PlaywrightException ex) when (ShouldInstallChromium(ex))
    {
        Console.WriteLine("Playwright Chromium is not installed. Installing it automatically...");
        await InstallChromiumAsync();
        return await chromium.LaunchAsync(options);
    }
}

static bool ShouldInstallChromium(PlaywrightException ex)
{
    return ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Please run the following command", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("playwright install", StringComparison.OrdinalIgnoreCase);
}

static async Task InstallChromiumAsync()
{
    var scriptPath = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");

    if (!File.Exists(scriptPath))
    {
        throw new InvalidOperationException($"The local Playwright installer was not found at '{scriptPath}'.");
    }

    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" install chromium",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        }
    };

    if (!process.Start())
    {
        throw new InvalidOperationException("Could not start the automatic Playwright Chromium installation.");
    }

    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"The automatic Playwright Chromium installation failed with exit code {process.ExitCode}.");
    }
}
