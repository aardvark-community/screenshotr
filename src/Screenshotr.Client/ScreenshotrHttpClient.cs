using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Screenshotr;

public class ScreenshotrHttpClient : IScreenshotrApi
{
    private readonly HttpClient _httpClient;
    private readonly string _apikey;

    private ScreenshotrHttpClient(string endpoint, string apikey)
    {
        _httpClient = new HttpClient() { BaseAddress = new Uri(endpoint) };
        _apikey = apikey;
    }

    public static async Task<ScreenshotrHttpClient> Connect(string endpoint, string apikey)
    {
        var client = new ScreenshotrHttpClient(endpoint, apikey);

        var connection = new HubConnectionBuilder()
            .WithUrl(client._httpClient.BaseAddress.AbsoluteUri + "screenshotrhub")
            .Build();

        connection.Closed += async (error) =>
        {
            await Task.Delay(new Random().Next(0, 5) * 1000);
            await connection.StartAsync();
        };

        connection.On<Screenshot>("ScreenshotAdded", screenshot => client.OnScreenshotAdded(screenshot));
        connection.On<Screenshot>("ScreenshotUpdated", screenshot => client.OnScreenshotUpdated(screenshot));

        try
        {
            await connection.StartAsync();
            Console.WriteLine("[SignalR] Connection started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] {ex.Message}");
        }

        return client;
    }

    private async Task<T> Call<T>(object request, string path)
    {
        var url = path;
        var response = await _httpClient.PostAsync(url, JsonContent.Create(request, null, JsonOptions));
        if (response.IsSuccessStatusCode)
        {
            try
            {
                return await JsonSerializer.DeserializeAsync<T>(await response.Content.ReadAsStreamAsync(), JsonOptions)
                    ?? throw new Exception($"Failed to parse response {await response.Content.ReadAsStringAsync()}.");
            }
            catch (JsonException)
            {
                throw new Exception($"Failed to parse response {await response.Content.ReadAsStringAsync()}.");
            }
        }
        else
        {
            throw new Exception($"Request failed {response.StatusCode},  {await response.Content.ReadAsStringAsync()}");
        }
    }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        IgnoreReadOnlyFields = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    public Task<ApiGetStatusResponse>               GetStatus               (ApiGetStatusRequest request)               => Call<ApiGetStatusResponse>               (request, Global.ApiPathStatus              );
    public Task<ApiGetScreenshotsSegmentedResponse> GetScreenshotsSegmented (ApiGetScreenshotsSegmentedRequest request) => Call<ApiGetScreenshotsSegmentedResponse> (request, Global.ApiPathScreenshotsSegment  );
    public Task<ApiImportScreenshotResponse>        ImportScreenshot        (ApiImportScreenshotRequest request)        => Call<ApiImportScreenshotResponse>        (request, Global.ApiPathScreenshotsImport   );
    public Task<ApiUpdateScreenshotResponse>        UpdateScreenshot        (ApiUpdateScreenshotRequest request)        => Call<ApiUpdateScreenshotResponse>        (request, Global.ApiPathScreenshotsUpdate   );
    public Task<ApiGetScreenshotResponse>           GetScreenshot           (ApiGetScreenshotRequest request)           => Call<ApiGetScreenshotResponse>           (request, Global.ApiPathScreenshotsGet      );
    //public async Task<ApiGetAllScreenshotsResponse> GetAllScreenshots       (ApiGetAllScreenshotsRequest request)
    //{
    //    var result = ImmutableDictionary<string, Screenshot>.Empty;
    //    var i = 0; var segmentSize = 1024;
    //    while (true)
    //    {
    //        var xs = await GetScreenshotsSegmented(new(request.ApiKey, Skip: i, Take: segmentSize));
    //        if (!xs.Screenshots.Any()) break;
    //        result = result.AddRange(xs.Screenshots.Select(x => new KeyValuePair<string, Screenshot>(x.Id, x)));
    //        i += segmentSize;
    //    }
    //    return new(result);
    //}

    public Task<ApiGenerateApiKeyResponse>          GenerateApiKey          (ApiGenerateApiKeyRequest request)          => Call<ApiGenerateApiKeyResponse>          (request, Global.ApiPathApiKeysGenerate     );
    public Task<ApiDeleteApiKeyResponse>            DeleteApiKey            (ApiDeleteApiKeyRequest request)            => Call<ApiDeleteApiKeyResponse>            (request, Global.ApiPathApiKeysDelete       );
    public Task<ApiListApiKeysResponse>             ListApiKeys             (ApiListApiKeysRequest request)             => Call<ApiListApiKeysResponse>             (request, Global.ApiPathApiKeysList         );

    public event Action<Screenshot> OnScreenshotAdded;
    public event Action<Screenshot> OnScreenshotUpdated;
}
