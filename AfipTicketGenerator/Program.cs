using AfipTicketGenerator;
using Microsoft.Playwright;
using System.Diagnostics;

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
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
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
Directory.CreateDirectory($"C:\\Boletas");

// Login
var page = await context.NewPageAsync();
await page.GotoAsync("https://auth.afip.gob.ar/contribuyente_/login.xhtml");
await page.FillAsync("input[name='F1:username']", cuit);
await page.ClickAsync("input[name='F1:btnSiguiente']");
await page.FillAsync("input[name='F1:password']", password);
await page.ClickAsync("input[name='F1:btnIngresar']");

// Ticket Generating
for(int day = days; day >= 0; day--)
{
    Console.WriteLine($"Generating ticket from {DateTime.Today.AddDays(-day).ToString("dd/MM/yyyy")}...");

    var products = new List<Product>()
    {
        new() { Name = "Tomate", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "150" },
        new() { Name = "Banana", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "120" },
        new() { Name = "Naranja", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "110" },
        new() { Name = "Manzana", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "150" },
        new() { Name = "Lechuga", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "100" },
        new() { Name = "Zanahoria", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "120" },
        new() { Name = "Cebolla", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "130" },
        new() { Name = "Mandarina", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "140" },
        new() { Name = "Limón", Quantity = Random.Shared.Next(3, 4).ToString(), Price = "170" },
    };
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

    Console.WriteLine($"Ticket generated");

    var waitForDownloadTask = newPage.WaitForDownloadAsync();
    await newPage.ClickAsync("input[value='Imprimir...']");
    var file = await waitForDownloadTask;
    var filePath = $"C:\\Boletas\\{file.SuggestedFilename}";
    await file.SaveAsAsync(filePath);
    ticketsToPrint.Add(filePath);

    Console.WriteLine($"Ticket downloaded to {filePath}");
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
