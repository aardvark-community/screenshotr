using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Screenshotr;

public class ScreenshotrHttpClient : IScreenshotrApi
{
    private readonly HttpClient _httpClient;
    private readonly string? _bearer;

    private ScreenshotrHttpClient(string endpoint, string? apikey)
    {
        _httpClient = new HttpClient() { BaseAddress = new Uri(endpoint) };
        _bearer = string.IsNullOrWhiteSpace(apikey) ? null : $"Bearer {apikey}";
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

        connection.On<Screenshot>("ScreenshotAdded", screenshot => client.OnScreenshotAdded?.Invoke(screenshot));
        connection.On<Screenshot>("ScreenshotUpdated", screenshot => client.OnScreenshotUpdated?.Invoke(screenshot));

        try
        {
            await connection.StartAsync();
            //Console.WriteLine("[SignalR] Connection started");
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
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request, null, JsonOptions)
        };
        if (_bearer != null) req.Headers.Add("Authorization", _bearer);
        var response = await _httpClient.SendAsync(req);

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
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new Exception($"403 Forbidden.");
            }
            else
            {
                throw new Exception($"{response.StatusCode}.\n{await response.Content.ReadAsStringAsync()}");
            }
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

    public Task<ApiGetStatusResponse              > GetStatus               (ApiGetStatusRequest request              ) => Call<ApiGetStatusResponse              > (request, Global.ApiPathStatus            );
    public Task<ApiImportScreenshotResponse       > ImportScreenshot        (ApiImportScreenshotRequest request       ) => Call<ApiImportScreenshotResponse       > (request, Global.ApiPathScreenshotsImport );
    public Task<ApiGetScreenshotResponse          > GetScreenshot           (ApiGetScreenshotRequest request          ) => Call<ApiGetScreenshotResponse          > (request, Global.ApiPathScreenshotsGet    );
    public Task<ApiGetScreenshotsSegmentedResponse> GetScreenshotsSegmented (ApiGetScreenshotsSegmentedRequest request) => Call<ApiGetScreenshotsSegmentedResponse> (request, Global.ApiPathScreenshotsSegment);
    public Task<ApiUpdateScreenshotResponse       > UpdateScreenshot        (ApiUpdateScreenshotRequest request       ) => Call<ApiUpdateScreenshotResponse       > (request, Global.ApiPathScreenshotsUpdate );
    public Task<ApiGetTagsResponse                > GetTags                 (ApiGetTagsRequest request                ) => Call<ApiGetTagsResponse                > (request, Global.ApiPathScreenshotsGetTags);
    public Task<ApiCreateApiKeyResponse           > CreateApiKey            (ApiCreateApiKeyRequest request           ) => Call<ApiCreateApiKeyResponse           > (request, Global.ApiPathApiKeysGenerate   );
    public Task<ApiDeleteApiKeyResponse           > DeleteApiKey            (ApiDeleteApiKeyRequest request           ) => Call<ApiDeleteApiKeyResponse           > (request, Global.ApiPathApiKeysDelete     );
    public Task<ApiListApiKeysResponse            > ListApiKeys             (ApiListApiKeysRequest request            ) => Call<ApiListApiKeysResponse            > (request, Global.ApiPathApiKeysList       );

    public event Action<Screenshot>? OnScreenshotAdded;
    public event Action<Screenshot>? OnScreenshotUpdated;
}
