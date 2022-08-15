using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Screenshotr;

var builder = WebApplication.CreateBuilder(args);

#region init env

Console.WriteLine($"[ScreenshotrService] init ...");

var screenshotrBaseDir = builder.Configuration["Screenshotr:Data"] ?? "./data";
var corsOrigins = builder.Configuration["Screenshotr:CorsOrigins"];
var enableHttpLogging = bool.TryParse(builder.Configuration["Screenshotr:EnableHttpLogging"], out var x) && x;
ScreenshotrApp.HttpHeaderUserId = builder.Configuration["Screenshotr:HttpHeaderUserId"];
ScreenshotrApp.HttpHeaderUserName = builder.Configuration["Screenshotr:HttpHeaderUserName"];
ScreenshotrApp.HttpHeaderUserDisplayName = builder.Configuration["Screenshotr:HttpHeaderDisplayName"];

Console.WriteLine($"[ScreenshotrService]     Screenshotr:Data={screenshotrBaseDir}");
Console.WriteLine($"[ScreenshotrService]     Screenshotr:CorsOrigins={corsOrigins}");
Console.WriteLine($"[ScreenshotrService]     Screenshotr:EnableHttpLogging={enableHttpLogging}");
Console.WriteLine($"[ScreenshotrService]     Screenshotr:HttpHeaderUserId={ScreenshotrApp.HttpHeaderUserId}");
Console.WriteLine($"[ScreenshotrService]     Screenshotr:HttpHeaderUserName={ScreenshotrApp.HttpHeaderUserName}");
Console.WriteLine($"[ScreenshotrService]     Screenshotr:HttpHeaderDisplayName={ScreenshotrApp.HttpHeaderUserDisplayName}");

screenshotrBaseDir = Path.GetFullPath(screenshotrBaseDir);
var repo = Repository.Init(screenshotrBaseDir);
var screenshotrService = new ScreenshotrRepositoryClient(repo);
var status = await screenshotrService.GetStatus();
Console.WriteLine($"[ScreenshotrService]     version {status.Version}");
Console.WriteLine($"[ScreenshotrService]     base directory is {screenshotrBaseDir}");
Console.WriteLine($"[ScreenshotrService]     found {status.Count} screenshots");
Console.WriteLine($"[ScreenshotrService]     done");

#endregion

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<IScreenshotrApi>(screenshotrService);
builder.Services.AddScoped<ScreenshotrApp>();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

if (!string.IsNullOrWhiteSpace(corsOrigins))
{
    var corsOriginsSplit = corsOrigins.Split(";", StringSplitOptions.TrimEntries);
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
            //builder.WithOrigins("https://*.aardworx.net")
            builder.WithOrigins(corsOriginsSplit)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders("*")
                );
    });
}

if (enableHttpLogging)
{
    builder.Services.AddHttpLogging(options =>
    {
        options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
        if (!string.IsNullOrWhiteSpace(ScreenshotrApp.HttpHeaderUserId)) options.RequestHeaders.Add(ScreenshotrApp.HttpHeaderUserId);
        if (!string.IsNullOrWhiteSpace(ScreenshotrApp.HttpHeaderUserName)) options.RequestHeaders.Add(ScreenshotrApp.HttpHeaderUserName);
        if (!string.IsNullOrWhiteSpace(ScreenshotrApp.HttpHeaderUserDisplayName)) options.RequestHeaders.Add(ScreenshotrApp.HttpHeaderUserDisplayName);
        //options.RequestHeaders.Add("X-Display-Name");
        //options.RequestHeaders.Add("X-Forwarded-User");
        //options.RequestHeaders.Add("Cookie");
    });
}
builder.Services.AddHttpContextAccessor();

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
if (corsOrigins != null) app.UseCors();
if (enableHttpLogging) app.UseHttpLogging();

void RegisterApi<T, R>(string path, Func<T, Task<R>> handler, string? role = default)
{
    app.MapPost(path, async ([FromHeader(Name = "Authorization")] string? authHeader, [FromBody] T req) =>
    {
        return (screenshotrService.Repository.ApiKeys.HasRole(authHeader, role))
            ? Results.Ok(await handler(req))
            : Results.StatusCode(StatusCodes.Status403Forbidden)
            ;
    });
}

RegisterApi<ApiGetStatusRequest              , ApiGetStatusResponse              >(Global.ApiPathStatus             , screenshotrService.GetStatus                                  );
RegisterApi<ApiImportScreenshotRequest       , ApiImportScreenshotResponse       >(Global.ApiPathScreenshotsImport  , screenshotrService.ImportScreenshot       , Roles.Importer    );
RegisterApi<ApiGetScreenshotRequest          , ApiGetScreenshotResponse          >(Global.ApiPathScreenshotsGet     , screenshotrService.GetScreenshot                              );
RegisterApi<ApiGetScreenshotsSegmentedRequest, ApiGetScreenshotsSegmentedResponse>(Global.ApiPathScreenshotsSegment , screenshotrService.GetScreenshotsSegmented                    );
RegisterApi<ApiUpdateScreenshotRequest       , ApiUpdateScreenshotResponse       >(Global.ApiPathScreenshotsUpdate  , screenshotrService.UpdateScreenshot                           );
RegisterApi<ApiGetTagsRequest                , ApiGetTagsResponse                >(Global.ApiPathScreenshotsGetTags , screenshotrService.GetTags                                    );
RegisterApi<ApiCreateApiKeyRequest           , ApiCreateApiKeyResponse           >(Global.ApiPathApiKeysGenerate    , screenshotrService.CreateApiKey           , Roles.Admin       );
RegisterApi<ApiDeleteApiKeyRequest           , ApiDeleteApiKeyResponse           >(Global.ApiPathApiKeysDelete      , screenshotrService.DeleteApiKey           , Roles.Admin       );
RegisterApi<ApiListApiKeysRequest            , ApiListApiKeysResponse            >(Global.ApiPathApiKeysList        , screenshotrService.ListApiKeys            , Roles.Admin       );

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