namespace Screenshotr;

public static class Global
{
    /// <summary>
    /// Current version string.
    /// </summary>
    public const string Version = "1.3.0";

    public const string ApiPathStatus                   = "/api/1.0/status";
    public const string ApiPathScreenshotsSegment       = "/api/1.0/screenshots/segment";
    public const string ApiPathScreenshotsImport        = "/api/1.0/screenshots/import";
    public const string ApiPathScreenshotsUpdate        = "/api/1.0/screenshots/update";
    public const string ApiPathScreenshotsGet           = "/api/1.0/screenshots/get";

    public const string ApiPathApiKeysGenerate          = "/api/1.0/apikeys/generate";
    public const string ApiPathApiKeysDelete            = "/api/1.0/apikeys/delete";
    public const string ApiPathApiKeysList              = "/api/1.0/apikeys/list";
}
