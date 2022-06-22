using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Screenshotr;

var builder = WebApplication.CreateBuilder(args);


Console.WriteLine($"[ScreenshotrService] init ...");
var screenshotrBaseDir = builder.Configuration["Screenshotr:Data"];
Console.WriteLine($"[ScreenshotrService]     Screenshotr:Data={screenshotrBaseDir}");
screenshotrBaseDir = Path.GetFullPath(screenshotrBaseDir);
var repo = Repository.Init(screenshotrBaseDir);
var screenshotrService = new ScreenshotrRepositoryClient(repo);
var status = await screenshotrService.GetStatus();
Console.WriteLine($"[ScreenshotrService]     version {status.Version}");
Console.WriteLine($"[ScreenshotrService]     base directory is {screenshotrBaseDir}");
Console.WriteLine($"[ScreenshotrService]     found {status.Count} screenshots");
Console.WriteLine($"[ScreenshotrService]     done");


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<IScreenshotrApi>(screenshotrService);
builder.Services.AddScoped<ScreenshotrApp>(_ => new(screenshotrService, repo.ApiKeys.AdminKey));
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(screenshotrBaseDir, "data")),
    RequestPath = "/data"
});

app.UseRouting();

app.MapPost(Global.ApiPathStatus,                   ([FromBody] ApiGetStatusRequest req)                => screenshotrService.GetStatus(req)                );
app.MapPost(Global.ApiPathScreenshotsSegment,       ([FromBody] ApiGetScreenshotsSegmentedRequest req)  => screenshotrService.GetScreenshotsSegmented(req)  );
app.MapPost(Global.ApiPathScreenshotsImport,        ([FromBody] ApiImportScreenshotRequest req)         => screenshotrService.ImportScreenshot(req)         );
app.MapPost(Global.ApiPathScreenshotsUpdate,        ([FromBody] ApiUpdateScreenshotRequest req)         => screenshotrService.UpdateScreenshot(req)         );
app.MapPost(Global.ApiPathScreenshotsGet,           ([FromBody] ApiGetScreenshotRequest req)            => screenshotrService.GetScreenshot(req)            );

app.MapBlazorHub();
app.MapHub<ScreenshotrHub>("/screenshotrhub");
app.MapFallbackToPage("/_Host");

{
    var service = app.Services.GetService<IScreenshotrApi>();
    if (service == null) throw new NotImplementedException();

    var hubContext = app.Services.GetService<IHubContext<ScreenshotrHub, IScreenshotrHubClient>>();
    if (hubContext == null) throw new NotImplementedException();

    service.OnScreenshotAdded += async s => await hubContext.Clients.All.ScreenshotAdded(s);
    service.OnScreenshotUpdated += async s => await hubContext.Clients.All.ScreenshotUpdated(s);
}

app.Run();


namespace Screenshotr
{
    /// <summary>
    /// CLIENTS can call methods that are defined as public.
    /// Currently we only want to SEND FROM SERVER to clients, so we define no functions.
    /// </summary>
    public class ScreenshotrHub : Hub<IScreenshotrHubClient>
    {
        //public async Task SendScreenshotAdded(Screenshot x)
        //    => await Clients.All.SendAsync("ScreenshotAdded", x);

        //public async Task SendScreenshotUpdated(Screenshot x)
        //    => await Clients.All.SendAsync("ScreenshotUpdated", x);
    }

    public interface IScreenshotrHubClient
    {
        Task ScreenshotAdded(Screenshot x);
        Task ScreenshotUpdated(Screenshot x);
    }
}