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

    private ScreenshotrHttpClient(string endpoint)
    {
        _httpClient = new HttpClient() { BaseAddress = new Uri(endpoint) };
    }

    public static async Task<ScreenshotrHttpClient> Create(string endpoint)
    {
        var client = new ScreenshotrHttpClient(endpoint);

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

    public Task<ApiGetStatusResponse>               GetStatus               (ApiGetStatusRequest request)               => Call<ApiGetStatusResponse>               (request, Global.ApiPathGetStatus               );
    public Task<ApiGetScreenshotsSegmentedResponse> GetScreenshotsSegmented (ApiGetScreenshotsSegmentedRequest request) => Call<ApiGetScreenshotsSegmentedResponse> (request, Global.ApiPathGetScreenshotsSegmented );
    public Task<ApiImportScreenshotResponse>        ImportScreenshot        (ApiImportScreenshotRequest request)        => Call<ApiImportScreenshotResponse>        (request, Global.ApiPathImportScreenshot        );
    public Task<ApiUpdateScreenshotResponse>        UpdateScreenshot        (ApiUpdateScreenshotRequest request)        => Call<ApiUpdateScreenshotResponse>        (request, Global.ApiPathUpdateScreenshot        );
    public Task<ApiGetScreenshotResponse>           GetScreenshot           (ApiGetScreenshotRequest request)           => Call<ApiGetScreenshotResponse>           (request, Global.ApiPathGetScreenshot           );
    public async Task<ApiGetAllScreenshotsResponse> GetAllScreenshots       (ApiGetAllScreenshotsRequest request)
    {
        var result = ImmutableDictionary<string, Screenshot>.Empty;
        var i = 0; var segmentSize = 1024;
        while (true)
        {
            var xs = await GetScreenshotsSegmented(new(Skip: i, Take: segmentSize));
            if (!xs.Screenshots.Any()) break;
            result = result.AddRange(xs.Screenshots.Select(x => new KeyValuePair<string, Screenshot>(x.Id, x)));
            i += segmentSize;
        }
        return new(result);
    }

    public event Action<Screenshot> OnScreenshotAdded;
    public event Action<Screenshot> OnScreenshotUpdated;
}
