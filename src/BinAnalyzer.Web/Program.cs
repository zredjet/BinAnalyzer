using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BinAnalyzer.Web;
using BinAnalyzer.Web.Services;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<FormatService>();
builder.Services.AddScoped<IFormatCatalog, HttpFormatCatalog>();
builder.Services.AddScoped<IFileSource, BrowserFileSource>();
builder.Services.AddScoped<GuiSession>();

await builder.Build().RunAsync();
