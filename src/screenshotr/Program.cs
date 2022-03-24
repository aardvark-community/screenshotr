﻿/*
 * Screenshotr CLI
 */

using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;


IConfiguration Configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var random = new Random();
string[] randomLabels = File.ReadAllLines("words.txt");

if (args.Length == 0) Usage();


var endpointString = Configuration["Screenshotr:ServiceEndpoint"];
var endpoint = endpointString != null ? new Uri(endpointString) : null;

try
{
    switch (args[0])
    {
        case "import": await Import(args.Tail()); break;
        case "list": await List(args.Tail()); break;
        case "tail": await Tail(args.Tail()); break;
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
    WriteLine("Usage: screenshotr <command> <args*> ");
    WriteLine("import -e <endpoint> [-t <tags>] <file|folder>* [-x <exclude>] [--addRandomlabels]");
    WriteLine("list -e <endpoint> [--skip <int>] [--tag <int>]");
    WriteLine("tail -e <endpoint>");
    WriteLine();
    WriteLine("examples: ");
    WriteLine("screenshotr import -e https://localhost:5001 -t \"mytag some-other-tag\" img.jpg /data/pictures/");
    Environment.Exit(0);
}

async Task Tail(string[] args)
{
    args = args.Where(x =>
        !x.Contains("Screenshotr:ServiceEndpoint")
        )
        .ToArray();

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "-e": endpoint = new(args[++i]); break;
            default: { Usage(); return; }
        }
    }

    if (endpoint == null) { Usage(); return; }

    var repo = await ScreenshotrService.Connect(endpoint);

    repo.OnScreenshotAdded += screenshot =>
    {
        WriteLine($"[ScreenshotAdded  ] {screenshot.Id}");
    };

    repo.OnScreenshotUpdated += screenshot =>
    {
        WriteLine($"[ScreenshotUpdated] {screenshot.Id}");
    };

    ReadLine();
}

async Task List(string[] args)
{
    var skip = 0;
    var take = int.MaxValue;

    args = args.Where(x =>
        !x.Contains("Screenshotr:ServiceEndpoint") 
        )
        .ToArray();

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "-e": endpoint = new(args[++i]); break;
            case "--skip": skip = int.Parse(args[++i]); break;
            case "--take": take = int.Parse(args[++i]); break;
            default: { Usage(); return; }
        }
    }

    if (endpoint == null) { Usage(); return; }

    var repo = await ScreenshotrService.Connect(endpoint);

    var reply = await repo.GetScreenshotsSegmented(skip: skip, take: take);
    foreach (var s in reply.Screenshots)
    {
        WriteLine(JsonSerializer.Serialize(s, Utils.JsonOptions));
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
            case "-e": endpoint = new(args[++i]); break;
            case "-t": tags.AddRange(Utils.ParseTags(args[++i])); break;
            case "-x": excludes.Add(args[++i]); break;
            case "--addrandomlabels": addRandomLabels = true; break;
            default: filesAndFolders.Add(args[i]); break;
        }
    }

    if (endpoint == null) { Usage(); return; }
    if (filesAndFolders.Count == 0) { Usage(); return; }

    IScreenshotrApi repo = endpoint.IsFile
        ? ScreenshotrRepositoryClient.Connect(endpoint.AbsolutePath)
        : await ScreenshotrHttpClient.Connect(endpoint.AbsoluteUri)
        ;

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

record Foo(string Title, object Custom);
record Bla(int Year, int Month, int Day);