namespace Screenshotr;

public static class ScreenshotrService
{
    public static Task<IScreenshotrApi> Connect(string endpoint, string apiKey)
        => Connect(new Uri(endpoint), apiKey);

    public static async Task<IScreenshotrApi> Connect(Uri endpoint, string apiKey)
    {
        IScreenshotrApi client = endpoint.IsFile
            ? ScreenshotrRepositoryClient.Connect(endpoint.AbsolutePath)
            : await ScreenshotrHttpClient.Connect(endpoint.AbsoluteUri, apiKey)
            ;

        return client;
    }
}
