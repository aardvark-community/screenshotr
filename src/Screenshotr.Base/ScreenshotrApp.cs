using System.Collections.Immutable;

namespace Screenshotr;

public record ScreenshotrModel(
    string Version,
    string? ActiveTagEditScreenshotId, 
    Filter Filter,
    IScreenshotrApi Service
    )
{
    public Screenshot GetScreenshot(string id)
        => Filter.AllScreenshots.All[id];
}

public class ScreenshotrApp : ElmApp<ScreenshotrModel, ScreenshotrApp.MessageType>, IDisposable
{
    public ScreenshotrApp(IScreenshotrApi service) : base(
        initialModel: new(
            Global.Version,
            ActiveTagEditScreenshotId: null,
            Filter: Filter.Empty,
            Service: service
            ),
        update: Update
        )
    {
        //Model.Service.OnChange += OnRepositoryUpdated;
        Model.Service.OnScreenshotAdded += OnScreenshotAdded;
        Model.Service.OnScreenshotUpdated += OnScreenshotUpdated;
        Dispatch(MessageType.InitScreenshotsFromRepo);
    }

    public void Dispose()
    {
        //Model.Service.OnChange -= OnRepositoryUpdated;
        Model.Service.OnScreenshotAdded -= OnScreenshotAdded;
        Model.Service.OnScreenshotUpdated -= OnScreenshotUpdated;
        GC.SuppressFinalize(this);
    }

    //private void OnRepositoryUpdated() => Dispatch(MessageType.OnRepositoryUpdated);
    private void OnScreenshotAdded(Screenshot x) => Dispatch(MessageType.OnScreenshotAdded, x);
    private void OnScreenshotUpdated(Screenshot x) => Dispatch(MessageType.OnScreenshotUpdated, x);

    public enum MessageType
    {
        InitScreenshotsFromRepo,

        OnRepositoryUpdated,
        OnScreenshotAdded,
        OnScreenshotUpdated,

        ToggleSelectedTag,
        ToggleSelectedYear,
        ToggleSelectedUser,
        ToggleSelectedHostname,
        ToggleSelectedProcess,

        RemoveScreenshotTag,
        EditScreenshotTagStart,
        EditScreenshotTagSubmit,
        EditScreenshotTagCancel,
    };

    private static async Task<ScreenshotrModel> Update(IElmApp<ScreenshotrModel, MessageType> app, IMessage message, ScreenshotrModel m)
    {
        switch (message.MessageType)
        {
            case MessageType.InitScreenshotsFromRepo:
                {
                    var all = ImmutableDictionary<string ,Screenshot>.Empty;
                    await foreach (var segment in m.Service.GetAllScreenshots())
                    {
                        all = all.AddRange(segment.Screenshots.Select(x => new KeyValuePair<string, Screenshot>(x.Id, x)));
                    }
                    m = m with
                    {
                        Filter = Filter.Create(all, FilterSortingMode.CreatedDescending, 128)
                    };
                    break;
                }

            case MessageType.OnRepositoryUpdated:
                {
                    // nop (this message is sent on *every* change to the underlying repository
                    // we use the more specific update messages below
                    break;
                }

            case MessageType.OnScreenshotAdded:
            case MessageType.OnScreenshotUpdated:
                {
                    var s = message.GetArgument<Screenshot>();
                    m = m with
                    {
                        Filter = m.Filter.UpsertScreenshot(s),
                    };
                    break;
                }


            case MessageType.ToggleSelectedTag:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedTag(x) };
                    break;
                }
            case MessageType.ToggleSelectedYear:
                {
                    var x = message.GetArgument<int>();
                    m = m with { Filter = m.Filter.ToggleSelectedYear(x) };
                    break;
                }
            case MessageType.ToggleSelectedUser:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedUser(x) };
                    break;
                }
            case MessageType.ToggleSelectedHostname:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedHostname(x) };
                    break;
                }
            case MessageType.ToggleSelectedProcess:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedProcess(x) };
                    break;
                }


            case MessageType.EditScreenshotTagStart:
                {
                    var screenshotId = message.GetArgument<string>();
                    m = m with { ActiveTagEditScreenshotId = screenshotId };
                    break;
                }

            case MessageType.EditScreenshotTagCancel:
                {
                    m = m with { ActiveTagEditScreenshotId = null };
                    break;
                }

            case MessageType.EditScreenshotTagSubmit:
                {
                    if (m.ActiveTagEditScreenshotId != null)
                    {
                        var newTag = message.GetArgument<string>();
                        var screenshot = m.GetScreenshot(m.ActiveTagEditScreenshotId);
                        screenshot = screenshot.AddTag(newTag);
                        await m.Service.UpdateScreenshot(screenshot);
                        m = m with { ActiveTagEditScreenshotId = null };
                    }
                    break;
                }

            case MessageType.RemoveScreenshotTag:
                {
                    var (id, tag) = message.GetArgument<(string, string)>();
                    var screenshot = m.GetScreenshot(id);
                    screenshot = screenshot with { Tags = screenshot.Tags.Remove(tag) };
                    await m.Service.UpdateScreenshot(screenshot);
                    break;
                }

            /*
                forgotten/todo ...
            */
            default:
                Console.WriteLine($"[TODO] {message.MessageType} is not implemented");
                break;
        }

        return m;
    }
}