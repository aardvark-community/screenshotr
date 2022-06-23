using System.Collections.Immutable;

namespace Screenshotr;

public class ScreenshotrRepositoryClient : IScreenshotrApi
{
    private Repository _repo;

    public ScreenshotrRepositoryClient(Repository repository)
    {
        _repo = repository; 
    }

    public static ScreenshotrRepositoryClient Connect(string baseDirectory)
    {
        var repo = Repository.Init(baseDirectory);
        return new ScreenshotrRepositoryClient(repo);
    }

    public static string Version => Global.Version;
    public int Count => _repo.Count;
    public ImmutableDictionary<string, Screenshot> Screenshots => _repo.Entries;
    public Screenshot GetScreenshot(string id) => _repo.Entries[id];

    public event Action? OnChange;
    public event Action<Screenshot>? OnScreenshotAdded;
    public event Action<Screenshot>? OnScreenshotUpdated;

    #region IScreenshotrApi

    public Task<ApiGetStatusResponse> GetStatus(ApiGetStatusRequest request)
    {
        var result = new ApiGetStatusResponse(Version: Global.Version, Count: _repo.Count);
        return Task.FromResult(result);
    }

    public Task<ApiGetScreenshotsSegmentedResponse> GetScreenshotsSegmented(ApiGetScreenshotsSegmentedRequest request)
    {
        ApiGetScreenshotsSegmentedResponse result;

        if (request.Skip >= _repo.Entries.Count)
        {
            result = new(Enumerable.Empty<Screenshot>(), Offset: _repo.Entries.Count, Count: 0);
        }
        else
        {
            var take = Math.Min(request.Take, _repo.Entries.Count - request.Skip);
            var xs = _repo.Entries.Values
                .OrderByDescending(x => x.Created)
                .Skip(request.Skip)
                .Take(take)
                ;

            result = new ApiGetScreenshotsSegmentedResponse(xs, Offset: request.Skip, Count: take);
        }

        return Task.FromResult(result);
    }

    public async Task<ApiImportScreenshotResponse> ImportScreenshot(ApiImportScreenshotRequest request)
    {
        (_repo, var screenshot, var isDuplicate) = await _repo.ImportScreenshot(
            request.Buffer, 
            timestamp: request.Timestamp, 
            tags: request.Tags, 
            custom: request.Custom, 
            importInfo: request.ImportInfo
            );

        if (screenshot == null) throw new Exception("Import failed.");
        OnScreenshotAdded?.Invoke(screenshot);
        OnChange?.Invoke();
        return new(screenshot, isDuplicate);
    }

    public async Task<ApiUpdateScreenshotResponse> UpdateScreenshot(ApiUpdateScreenshotRequest request)
    {
        ApiUpdateScreenshotResponse result;
        if (_repo.Entries.ContainsKey(request.Screenshot.Id))
        {
            _repo = await _repo.UpdateScreenshot(request.Screenshot);
            result = new(request.Screenshot, IsSuccess: true);
            OnScreenshotUpdated?.Invoke(request.Screenshot);
            OnChange?.Invoke();
        }
        else
        {
            result = new(request.Screenshot, IsSuccess: false);
        }

        return result;
    }

    public Task<ApiGetScreenshotResponse> GetScreenshot(ApiGetScreenshotRequest request)
        => Task.FromResult(new ApiGetScreenshotResponse(_repo.Entries[request.Id]));

    public Task<ApiCreateApiKeyResponse> CreateApiKey(ApiCreateApiKeyRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<ApiDeleteApiKeyResponse> DeleteApiKey(ApiDeleteApiKeyRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<ApiListApiKeysResponse> ListApiKeys(ApiListApiKeysRequest request)
    {
        throw new NotImplementedException();
    }

    #endregion
}
