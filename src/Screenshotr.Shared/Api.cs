using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
public record ApiGetScreenshotsSegmentedResponse(IEnumerable<Screenshot> Screenshots);

public record ApiGetAllScreenshotsRequest();
public record ApiGetAllScreenshotsResponse(ImmutableDictionary<string, Screenshot> Screenshots);

public interface IScreenshotrApi
{
    Task<ApiGetStatusResponse>                  GetStatus               (ApiGetStatusRequest request                );
    Task<ApiImportScreenshotResponse>           ImportScreenshot        (ApiImportScreenshotRequest request         );
    Task<ApiUpdateScreenshotResponse>           UpdateScreenshot        (ApiUpdateScreenshotRequest request         );
    Task<ApiGetScreenshotsSegmentedResponse>    GetScreenshotsSegmented (ApiGetScreenshotsSegmentedRequest request  );
    Task<ApiGetScreenshotResponse>              GetScreenshot           (ApiGetScreenshotRequest request            );
    Task<ApiGetAllScreenshotsResponse>          GetAllScreenshots       (ApiGetAllScreenshotsRequest request        );

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

    public static Task<ApiGetAllScreenshotsResponse> GetAllScreenshots(this IScreenshotrApi self)
        => self.GetAllScreenshots(new());
}
