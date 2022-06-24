using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Screenshotr;

public record ApiGetStatusRequest();
public record ApiGetStatusResponse(string Version, int Count);

public record ApiImportScreenshotRequest(byte[] Buffer, IEnumerable<string> Tags, Custom Custom, ImportInfo ImportInfo, DateTimeOffset? Timestamp);
public record ApiImportScreenshotResponse(Screenshot Screenshot, bool IsDuplicate);

public record ApiUpdateScreenshotRequest(Screenshot Screenshot);
public record ApiUpdateScreenshotResponse(Screenshot Screenshot, bool IsSuccess);

public record ApiGetScreenshotRequest(string Id);
public record ApiGetScreenshotResponse(Screenshot Screenshot);

public record ApiGetScreenshotsSegmentedRequest(int Skip, int Take);
public record ApiGetScreenshotsSegmentedResponse(IEnumerable<Screenshot> Screenshots, int Offset, int Count);

public record ApiCreateApiKeyRequest(string Description, IEnumerable<string> Roles, DateTimeOffset ValidUntil);
public record ApiCreateApiKeyResponse(ApiKey ApiKey);

public record ApiDeleteApiKeyRequest(string ApiKeyToDelete);
public record ApiDeleteApiKeyResponse(ApiKey? DeletedApiKey);

public record ApiListApiKeysRequest(int Skip, int Take);
public record ApiListApiKeysResponse(IEnumerable<ApiKey> ApiKeys, int Offset, int Count);

public interface IScreenshotrApi
{
    Task<ApiGetStatusResponse>                  GetStatus               (ApiGetStatusRequest request                );
    Task<ApiImportScreenshotResponse>           ImportScreenshot        (ApiImportScreenshotRequest request         );
    Task<ApiUpdateScreenshotResponse>           UpdateScreenshot        (ApiUpdateScreenshotRequest request         );
    Task<ApiGetScreenshotsSegmentedResponse>    GetScreenshotsSegmented (ApiGetScreenshotsSegmentedRequest request  );
    Task<ApiGetScreenshotResponse>              GetScreenshot           (ApiGetScreenshotRequest request            );
    Task<ApiCreateApiKeyResponse>               CreateApiKey            (ApiCreateApiKeyRequest request             );
    Task<ApiDeleteApiKeyResponse>               DeleteApiKey            (ApiDeleteApiKeyRequest request             );
    Task<ApiListApiKeysResponse>                ListApiKeys             (ApiListApiKeysRequest request              );

    event Action<Screenshot>? OnScreenshotAdded;
    event Action<Screenshot>? OnScreenshotUpdated;
}

public static class IScreenshotrApiExtensions
{
    public static Task<ApiGetStatusResponse> GetStatus(this IScreenshotrApi self) => self.GetStatus(new());

    public static Task<ApiImportScreenshotResponse> ImportScreenshot(this IScreenshotrApi self,
        byte[] buffer,
        IEnumerable<string>? tags = null,
        Custom? custom = null,
        ImportInfo? importInfo = null,
        DateTimeOffset? timestamp = null
        )
        => self.ImportScreenshot(new(
            Buffer: buffer,
            Tags: tags ?? Enumerable.Empty<string>(),
            Custom: custom ?? Custom.Empty,
            ImportInfo: importInfo ?? ImportInfo.Now,
            Timestamp: timestamp ?? DateTimeOffset.Now
            ));
 
    public static Task<ApiUpdateScreenshotResponse> UpdateScreenshot(this IScreenshotrApi self, Screenshot updatedScreenshot)
        => self.UpdateScreenshot(new(updatedScreenshot));

    public static Task<ApiGetScreenshotsSegmentedResponse> GetScreenshotsSegmented(this IScreenshotrApi self, int skip, int take)
        => self.GetScreenshotsSegmented(new(Skip: skip, Take: take));

    public static Task<ApiGetScreenshotResponse> GetScreenshot(this IScreenshotrApi self, string id)
        => self.GetScreenshot(new(id));

    public static Task<ApiCreateApiKeyResponse> CreateApiKey(this IScreenshotrApi self, string description, IReadOnlyList<string> roles, DateTimeOffset validUntil)
        => self.CreateApiKey(new(description, roles, validUntil));
    
    public static Task<ApiDeleteApiKeyResponse> DeleteApiKey(this IScreenshotrApi self, string apiKeyToDelete)
        => self.DeleteApiKey(new(apiKeyToDelete));
    
    public static Task<ApiListApiKeysResponse> ListApiKeys(this IScreenshotrApi self, int skip, int take)
        => self.ListApiKeys(new(skip, take));

    public static async IAsyncEnumerable<ApiGetScreenshotsSegmentedResponse> GetAllScreenshots(this IScreenshotrApi self)
    {
        var i = 0; var segmentSize = 1024;
        while (true)
        {
            var segment = await self.GetScreenshotsSegmented(new(Skip: i, Take: segmentSize));
            if (!segment.Screenshots.Any()) yield break;
            yield return segment;
            i += segmentSize;
        }
    }
}
