namespace Screenshotr;

public static class Global
{
    /// <summary>
    /// Current version string.
    /// </summary>
    public const string Version = "1.0.2";

    public const string ApiPathGetStatus                = "/api/1.0/status";
    public const string ApiPathGetScreenshotsSegmented  = "/api/1.0/screenshots/segment";
    public const string ApiPathImportScreenshot         = "/api/1.0/screenshots/import";
    public const string ApiPathUpdateScreenshot         = "/api/1.0/screenshots/update";
    public const string ApiPathGetScreenshot            = "/api/1.0/screenshots/get";
}
