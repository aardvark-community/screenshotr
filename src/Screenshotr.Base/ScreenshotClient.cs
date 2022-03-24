namespace Screenshotr;

public static class ScreenshotrService
{
    public static Task<IScreenshotrApi> Connect(string endpoint)
        => Connect(new Uri(endpoint));

    public static async Task<IScreenshotrApi> Connect(Uri endpoint)
    {
        IScreenshotrApi client = endpoint.IsFile
            ? ScreenshotrRepositoryClient.Create(endpoint.AbsolutePath)
            : await ScreenshotrHttpClient.Create(endpoint.AbsoluteUri)
            ;

        return client;
    }
}
