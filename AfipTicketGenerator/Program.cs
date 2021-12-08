using AfipTicketGenerator;
using Microsoft.Playwright;

// Console
Console.WriteLine("Welcome to AfipTicketGenerator!");
Console.WriteLine("Write your CUIT");
var cuit = Console.ReadLine();
Console.WriteLine("Write your password");
var password = ConsoleExtensions.ReadPassword();
Console.WriteLine("How many days ago discounting from today do you want to generate?");
var daysInput = Console.ReadLine();
int.TryParse(daysInput, out var days);
Console.WriteLine("Generating...");

// Configuration
List<Product> products = new()
{
    new() { Name = "Tomate", Quantity = Random.Shared.Next(12, 15).ToString(), Price = "45" },
    new() { Name = "Banana", Quantity = Random.Shared.Next(12, 15).ToString(), Price = "40" },
    new() { Name = "Naranja", Quantity = Random.Shared.Next(12, 15).ToString(), Price = "35" },
    new() { Name = "Manzana", Quantity = Random.Shared.Next(12, 15).ToString(), Price = "35" },
    new() { Name = "Lechuga", Quantity = Random.Shared.Next(12, 15).ToString(), Price = "30" },
};
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
    SlowMo = 50,
    Timeout = 15000
});
await using var context = await browser.NewContextAsync();

// Login
var page = await context.NewPageAsync();
await page.GotoAsync("https://auth.afip.gob.ar/contribuyente_/login.xhtml");
await page.FillAsync("input[name='F1:username']", cuit);
await page.ClickAsync("input[name='F1:btnSiguiente']");
await page.FillAsync("input[name='F1:password']", password);
await page.ClickAsync("input[name='F1:btnIngresar']");

// Generate tickets
for(int day = days; day >= 0; day--)
{
    var newPage = await context.RunAndWaitForPageAsync(async () =>
    {
        await page.ClickAsync("text=Comprobantes en línea");
    });
    await newPage.WaitForLoadStateAsync();
    await newPage.ClickAsync("input[value='DE FRANCESCO LUIS']");
    await newPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
    await newPage.GotoAsync("https://serviciosjava2.afip.gob.ar/rcel/jsp/buscarPtosVtas.do");
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
        await newPage.FillAsync($"input[id='detalle_precio{i + 1}']", products[i].Quantity);
        if (i < products.Count - 1)
            await newPage.ClickAsync("input[value='Agregar línea descripción']");
    }
    await newPage.ClickAsync("input[value='Continuar >']");
    newPage.Dialog += (_, dialog) => dialog.AcceptAsync();
    await newPage.ClickAsync("input[id='btngenerar']");
    /* TODO DOWNLOAD AND PRINT
    var download = await page.RunAndWaitForDownloadAsync(async () =>
    {
        await newPage.ClickAsync("input[value='Imprimir...']");
    });
    */
}
