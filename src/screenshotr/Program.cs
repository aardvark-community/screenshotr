/*
 * Screenshotr CLI
 */

using Microsoft.Extensions.Configuration;
using Screenshotr;
using SixLabors.ImageSharp;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

string GetLocalCacheDir()
{
    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
    var dir = Path.Combine(local, "screenshotr");
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    return dir;
}
string cachedCredentialsFileName = Path.Combine(GetLocalCacheDir(), "cache.json");

IConfiguration Configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var random = new Random();
string[] randomLabels = File.ReadAllLines("words.txt");

if (args.Length == 0) Usage();


// extract endpoint and apikey args
var endpoint = default(Uri);
var apikey = "";
{
    if (TryGetCachedCrendentials(out var cc))
    {
        endpoint = new Uri(cc.Endpoint);
        apikey = cc.Apikey;
    }

    var rest = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "-k": apikey = new(args[++i]); break;

            case "-e": endpoint = new(args[++i]); break;

            case "Screenshotr:ServiceEndpoint":
                {
                    var endpointString = Configuration["Screenshotr:ServiceEndpoint"];
                    endpoint = endpointString != null ? new Uri(endpointString) : null;
                    break;
                }

            default: rest.Add(args[i]); break;
        }
    }
    args = rest.ToArray();
}
if (endpoint == null) { Usage(); return; }
IScreenshotrApi repo = endpoint.IsFile
    ? ScreenshotrRepositoryClient.Connect(endpoint.AbsolutePath)
    : await ScreenshotrHttpClient.Connect(endpoint.AbsoluteUri, apikey)
    ;


try
{
    if (args.Contains("--version"))
    {
        WriteLine(Global.Version);
        return;
    }

    if (args.Contains("--help"))
    {
        Usage();
        return;
    }

    switch (args[0])
    {
        case "import"    : await Import    (args.Tail()); break;
        case "list"      : await List      (args.Tail()); break;
        case "tail"      : await Tail      (args.Tail()); break;
        case "apikeys"   : await ApiKeys   (args.Tail()); break;
        case "connect"   : await Connect   (args.Tail()); break;
        case "disconnect": await Disconnect(args.Tail()); break;
        default: Usage(); break;
    };
}
catch (Exception e)
{
    WriteLine($"[ERROR] {e.Message}");
    Usage();
}

void Usage()
{
    WriteLine(
$@"Usage:

  screenshotr <command> <args*> [-e <endpoint> -k <apikey>]
    You can either specify endpoint (-e) and apikey (-k) each
    time you run the screenshotr command, or you can use the
    connect command, which will remember the values for all subsequent
    runs (until you disconnect).

  screenshotr --version
    Print version.

  screenshotr --help
    Print usage message.

  Commands:
    import [-t <tags>] <file|folder>* [-x <exclude>] [--addRandomLabels]
    list [--skip <int>] [--take <int>]
    tail
    apikeys
      create -d <description> [-r <role>]+ [--days <float>]
             Available roles are: {Roles.Admin}, {Roles.Importer}
      delete <apikey>
      list
    connect -e <endpoint> -k <apikey>
    disconnect
  
  Examples:
    screenshotr connect -e ""https://localhost"" -k ""7d10785f41e8...""
    screenshotr disconnect
    screenshotr import -t ""mytag some-other-tag"" img.jpg /data/pictures/
    screenshotr list --skip 10 --take 5
    screenshotr tail
    screenshotr apikeys create -d ""alice's import key"" -r ""{Roles.Importer}""
    screenshotr apikeys delete ""2442d075d2f3888...""
    screenshotr apikeys list");

    Environment.Exit(0);
}

Task Connect(string[] args)
{
    var cc = new CachedCredentials(endpoint.ToString(), apikey);
    File.WriteAllText(cachedCredentialsFileName, JsonSerializer.Serialize(cc, new JsonSerializerOptions { WriteIndented = true }));
    return Task.CompletedTask;
}

bool TryGetCachedCrendentials([NotNullWhen(true)] out CachedCredentials? credentials)
{
    credentials = File.Exists(cachedCredentialsFileName)
        ? JsonSerializer.Deserialize<CachedCredentials>(File.ReadAllText(cachedCredentialsFileName))
        : null
        ;
    return credentials != null;
}

Task Disconnect(string[] args)
{
    File.Delete(cachedCredentialsFileName);
    return Task.CompletedTask;
}

async Task ApiKeys(string[] args)
{
    switch (args[0].ToLower())
    {
        case "create": await ApiKeysCreate(args.Tail()); break;
        case "delete": await ApiKeysDelete(args.Tail()); break;
        case "list"  : await ApiKeysList  (args.Tail()); break;
        default: Usage(); break;
    }
}

async Task ApiKeysCreate(string[] args)
{
    var description = "";
    var roles = new List<string>();
    var days = 3650.0;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "-d": description = args[++i]; break;
            case "-r": roles.Add(args[++i]); break;
            case "--days": days = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
            default: { Usage(); return; }
        }
    }

    var reply = await repo.CreateApiKey(description, roles, DateTimeOffset.UtcNow.AddDays(days));
    WriteLine(JsonSerializer.Serialize(reply, Utils.JsonOptions));
}
async Task ApiKeysDelete(string[] args)
{
    foreach (var k in args)
    {
        var r = await repo.DeleteApiKey(k);
        if (r.DeletedApiKey != null)
        {
            WriteLine(JsonSerializer.Serialize(r.DeletedApiKey, Utils.JsonOptions));
        }
        else
        {
            WriteLine("{}");
        }
    }
}
async Task ApiKeysList(string[] args)
{
    var skip = 0;
    var take = int.MaxValue;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "--skip": skip = int.Parse(args[++i]); break;
            case "--take": take = int.Parse(args[++i]); break;
            default: { Usage(); return; }
        }
    }

    var reply = await repo.ListApiKeys(skip: skip, take: take);
    foreach (var x in reply.ApiKeys)
    {
        WriteLine(JsonSerializer.Serialize(x, Utils.JsonOptions));
    }
}

Task Tail(string[] args)
{
    if (args.Length > 0) { Usage(); return Task.CompletedTask; }

    repo.OnScreenshotAdded += screenshot =>
    {
        WriteLine($"[ScreenshotAdded  ] {screenshot.Id}");
    };

    repo.OnScreenshotUpdated += screenshot =>
    {
        WriteLine($"[ScreenshotUpdated] {screenshot.Id}");
    };

    ReadLine();
    return Task.CompletedTask;
}

async Task List(string[] args)
{
    var skip = 0;
    var take = int.MaxValue;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "--skip": skip = int.Parse(args[++i]); break;
            case "--take": take = int.Parse(args[++i]); break;
            default: { Usage(); return; }
        }
    }

    var reply = await repo.GetScreenshotsSegmented(skip: skip, take: take);
    foreach (var x in reply.Screenshots)
    {
        WriteLine(JsonSerializer.Serialize(x, Utils.JsonOptions));
    }
}

async Task Import(string[] args)
{
    var tags = new List<string>();
    var filesAndFolders = new List<string>();
    var excludes = new List<string>();
    var addRandomLabels = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "-t": tags.AddRange(Utils.ParseTags(args[++i])); break;
            case "-x": excludes.Add(args[++i]); break;
            case "--addrandomlabels": addRandomLabels = true; break;
            default: filesAndFolders.Add(args[i]); break;
        }
    }

    if (filesAndFolders.Count == 0) { Usage(); return; }

    var countFilenames = 0;
    var countSuccess = 0;
    var countDuplicate = 0;
    var countExclude = 0;
    var countNoImageFile = 0;
    var countDontExist = 0;

    var maxMsgLength = 0;
    void PrintStatsLine(string filename)
    {
        var msgSuccess = countSuccess > 0 ? $" imported {countSuccess} |" : "";
        var msgDuplicate = countDuplicate > 0 ? $" duplicate {countDuplicate} |" : "";
        var msgExclude = countExclude > 0 ? $" excluded {countExclude} |" : "";
        var msgNoImage = countNoImageFile > 0 ? $" other {countNoImageFile} |" : "";
        var msgDontExist = countDontExist > 0 ? $" don't exist {countDontExist} |" : "";

        var msg = $"\rfiles {countFilenames} |{msgSuccess}{msgDuplicate}{msgExclude}{msgNoImage}{msgDontExist} {filename}";
        if (msg.Length > maxMsgLength) maxMsgLength = msg.Length;
        Write(msg.PadRight(maxMsgLength));
    }

    async Task ImportFile(string filename)
    {
        try
        {
            var finalTags = new List<string>(tags);
            if (addRandomLabels)
            {
                var count = (int)((random.NextDouble() * random.NextDouble()) * 10);
                for (var i0 = 0; i0 < count; i0++) finalTags.Add(randomLabels[random.Next(randomLabels.Length)]);
            }

            var timestamp = new FileInfo(filename).LastWriteTime;
            var buffer = await File.ReadAllBytesAsync(filename);
            var res = await repo.ImportScreenshot(buffer, finalTags, Custom.Empty, ImportInfo.Now, timestamp);
            if (res.Screenshot != null)
            {
                if (res.IsDuplicate) countDuplicate++; else countSuccess++;
            }
        }
        catch (Exception e)
        {
            WriteLine();
            ForegroundColor = ConsoleColor.Red;
            WriteLine($"[screenshotr]     {e.Message}");
            ResetColor();
        }
    }

    async Task ProcessFile(string filename)
    {
        if (excludes.Any(x => filename.Contains(x)))
        {
            countExclude++;
            return;
        }

        countFilenames++;
        if (File.Exists(filename))
        {
            try
            {
                var info = Image.Identify(filename);
                if (info != null)
                {
                    await ImportFile(filename);
                }
                else
                {
                    countNoImageFile++;
                }
            }
            catch
            {
                countNoImageFile++;
            }
        }
        else
        {
            countDontExist++;
        }
    }

    static IEnumerable<string> EnumerateAllFiles(IEnumerable<string> filesAndFolders)
    {
        foreach (var x in filesAndFolders)
        {
            if (Directory.Exists(x))
            {
                foreach (var f in Directory.EnumerateFiles(x, "*", SearchOption.AllDirectories))
                {
                    yield return f;
                }
            }
            else
            {
                yield return x;
            }
        }
    }

    if (filesAndFolders.Count > 1)
    {
        WriteLine("importing");
        foreach (var x in filesAndFolders) WriteLine($"  {x}");
    }
    else
    {
        WriteLine($"importing {filesAndFolders[0]}");
    }

    WriteLine($"starting at {DateTimeOffset.Now}");

    var t0 = DateTimeOffset.Now;
    foreach (var (filename, i) in EnumerateAllFiles(filesAndFolders).Select((x, i) => (x, i)))
    {
        await ProcessFile(filename);
        PrintStatsLine(filename);
    }
    var t1 = DateTimeOffset.Now;
    var dt = t1 - t0;

    PrintStatsLine("");
    WriteLine($"\r\nfinished at {t1}");
    WriteLine($"{dt.TotalSeconds:N0} seconds");
}
