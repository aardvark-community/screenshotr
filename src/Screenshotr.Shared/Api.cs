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

public record ApiImportScreenshotRequest(string ApiKey, byte[] Buffer, IEnumerable<string> Tags, Custom Custom, ImportInfo ImportInfo, DateTimeOffset? Timestamp);
public record ApiImportScreenshotResponse(Screenshot Screenshot, bool IsDuplicate);

public record ApiUpdateScreenshotRequest(string ApiKey, Screenshot Screenshot);
public record ApiUpdateScreenshotResponse(Screenshot Screenshot, bool IsSuccess);

public record ApiGetScreenshotRequest(string ApiKey, string Id);
public record ApiGetScreenshotResponse(Screenshot Screenshot);

public record ApiGetScreenshotsSegmentedRequest(string ApiKey, int Skip, int Take);
public record ApiGetScreenshotsSegmentedResponse(IEnumerable<Screenshot> Screenshots);

//public record ApiGetAllScreenshotsRequest(string ApiKey);
//public record ApiGetAllScreenshotsResponse(ImmutableDictionary<string, Screenshot> Screenshots);

public record ApiGenerateApiKeyRequest(string ApiKey, IEnumerable<string> Roles, string Description, DateTimeOffset ValidUntil);
public record ApiGenerateApiKeyResponse(ApiKey ApiKey);

public record ApiDeleteApiKeyRequest(string ApiKey, string ApiKeyToDelete);
public record ApiDeleteApiKeyResponse();

public record ApiListApiKeysRequest(string ApiKey, int Skip, int Take);
public record ApiListApiKeysResponse(IEnumerable<ApiKey> ApiKeys, int Skip, int Take);

public interface IScreenshotrApi
{
    Task<ApiGetStatusResponse>                  GetStatus               (ApiGetStatusRequest request                );
    Task<ApiImportScreenshotResponse>           ImportScreenshot        (ApiImportScreenshotRequest request         );
    Task<ApiUpdateScreenshotResponse>           UpdateScreenshot        (ApiUpdateScreenshotRequest request         );
    Task<ApiGetScreenshotsSegmentedResponse>    GetScreenshotsSegmented (ApiGetScreenshotsSegmentedRequest request  );
    Task<ApiGetScreenshotResponse>              GetScreenshot           (ApiGetScreenshotRequest request            );
    //Task<ApiGetAllScreenshotsResponse>          GetAllScreenshots       (ApiGetAllScreenshotsRequest request        );

    Task<ApiGenerateApiKeyResponse>             GenerateApiKey          (ApiGenerateApiKeyRequest request           );
    Task<ApiDeleteApiKeyResponse>               DeleteApiKey            (ApiDeleteApiKeyRequest request             );
    Task<ApiListApiKeysResponse>                ListApiKeys             (ApiListApiKeysRequest request              );


    event Action<Screenshot>? OnScreenshotAdded;
    event Action<Screenshot>? OnScreenshotUpdated;
}

public static class IScreenshotrApiExtensions
{
    public static Task<ApiGetStatusResponse> GetStatus(this IScreenshotrApi self) => self.GetStatus(new());

    public static Task<ApiImportScreenshotResponse> ImportScreenshot(this IScreenshotrApi self,
        string apiKey,
        byte[] buffer,
        IEnumerable<string>? tags = null,
        Custom? custom = null,
        ImportInfo? importInfo = null,
        DateTimeOffset? timestamp = null
        )
        => self.ImportScreenshot(new(
            ApiKey: apiKey,
            Buffer: buffer,
            Tags: tags ?? Enumerable.Empty<string>(),
            Custom: custom ?? Custom.Empty,
            ImportInfo: importInfo ?? ImportInfo.Now,
            Timestamp: timestamp ?? DateTimeOffset.Now
            ));
 
    public static Task<ApiUpdateScreenshotResponse> UpdateScreenshot(this IScreenshotrApi self, string apiKey, Screenshot updatedScreenshot)
        => self.UpdateScreenshot(new(apiKey, updatedScreenshot));

    public static Task<ApiGetScreenshotsSegmentedResponse> GetScreenshotsSegmented(this IScreenshotrApi self, string apiKey, int skip, int take)
        => self.GetScreenshotsSegmented(new(apiKey, Skip: skip, Take: take));

    public static Task<ApiGetScreenshotResponse> GetScreenshot(this IScreenshotrApi self, string apiKey, string id)
        => self.GetScreenshot(new(apiKey, id));

    //public static Task<ApiGetAllScreenshotsResponse> GetAllScreenshots(this IScreenshotrApi self, string apiKey)
    //    => self.GetAllScreenshots(new(apiKey));

    public static Task<ApiGenerateApiKeyResponse> GenerateApiKey(this IScreenshotrApi self, string apiKey, IEnumerable<string> roles, string description, DateTimeOffset validUntil)
        => self.GenerateApiKey(new(apiKey, roles, description, validUntil));
    
    public static Task<ApiDeleteApiKeyResponse> DeleteApiKey(this IScreenshotrApi self, string apiKey, string apiKeyToDelete)
        => self.DeleteApiKey(new(apiKey, apiKeyToDelete));
    
    public static Task<ApiListApiKeysResponse> ListApiKeys(this IScreenshotrApi self, string apiKey, int skip, int take)
        => self.ListApiKeys(new(apiKey, skip, take));
}
