namespace Screenshotr;

public static class ScreenshotrService
{
    public static Task<IScreenshotrApi> Connect(string endpoint)
        => Connect(new Uri(endpoint));

    public static async Task<IScreenshotrApi> Connect(Uri endpoint)
    {
        IScreenshotrApi client = endpoint.IsFile
            ? ScreenshotrRepositoryClient.Connect(endpoint.AbsolutePath)
            : await ScreenshotrHttpClient.Connect(endpoint.AbsoluteUri)
            ;

        return client;
    }
}
