using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Immutable;
using System.Text.Json;

namespace Screenshotr;

public record ScreenshotrModel(
    string Version,
    string? UserId,
    string? UserName,
    string? UserDisplayName,
    
    // gallery
    bool ShowGallery,
    string? ActiveTagEditScreenshotId,
    Filter Filter,

    // slideshow
    bool ShowSlideshow,
    int CurrentSlideshowIndex,

    IScreenshotrApi Service
    )
{
    public Screenshot GetScreenshot(string id)
        => Filter.AllScreenshots.All[id];
}

public class ScreenshotrApp : ElmApp<ScreenshotrModel, ScreenshotrApp.MessageType>, IDisposable
{
    public static string? HttpHeaderUserId { get; set; } = null;
    public static string? HttpHeaderUserName { get; set; } = null;
    public static string? HttpHeaderUserDisplayName { get; set; } = null;

    public ScreenshotrApp(IScreenshotrApi service) : base(
        initialModel: new(
            Global.Version,
            UserId: null,
            UserName: null,
            UserDisplayName: null,

            // gallery
            ShowGallery: true,
            ActiveTagEditScreenshotId: null,
            Filter: Filter.Empty,

            // slideshow
            ShowSlideshow: false,
            CurrentSlideshowIndex: -1,

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
        SetUserId,
        SetUserName,
        SetUserDisplayName,

        OnKeyDown,
        OnKeyUp,
        OnKeyPress,

        InitScreenshotsFromRepo,

        OnRepositoryUpdated,
        OnScreenshotAdded,
        OnScreenshotUpdated,

        OnClickGalleryImage,
        SetSlideshowIndex,

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
            case MessageType.SetUserId:
                {
                    var x = message.GetArgument<string?>();
                    if (!string.IsNullOrWhiteSpace(x)) m = m with { UserId = x };
                    break;
                }

            case MessageType.SetUserName:
                {
                    var x = message.GetArgument<string?>();
                    if (!string.IsNullOrWhiteSpace(x)) m = m with { UserName = x };
                    break;
                }

            case MessageType.SetUserDisplayName:
                {
                    var x = message.GetArgument<string?>();
                    if (!string.IsNullOrWhiteSpace(x)) m = m with { UserDisplayName = x };
                    break;
                }

            case MessageType.OnKeyPress:
                {
                    var x = message.GetArgument<KeyboardEventArgs>();
                    //Console.WriteLine(JsonSerializer.Serialize(x));
                    break;
                }

            case MessageType.OnKeyDown:
                {
                    var x = message.GetArgument<KeyboardEventArgs>();
                    switch (x.Key)
                    {
                        case "ArrowLeft": 
                            app.Dispatch(MessageType.SetSlideshowIndex, m.CurrentSlideshowIndex - 1); 
                            break;
                        case "ArrowRight":
                            app.Dispatch(MessageType.SetSlideshowIndex, m.CurrentSlideshowIndex + 1);
                            break;
                        case "Escape":
                            if (m.ShowSlideshow)
                            {
                                m = m with
                                {
                                    ShowGallery = true,
                                    ShowSlideshow = false,
                                    CurrentSlideshowIndex = -1
                                };
                            }
                            break;
                    }
                    //Console.WriteLine(JsonSerializer.Serialize(x));
                    break;
                }

            case MessageType.OnKeyUp:
                {
                    var x = message.GetArgument<KeyboardEventArgs>();
                    //Console.WriteLine(JsonSerializer.Serialize(x));
                    break;
                }

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


            case MessageType.OnClickGalleryImage:
                {
                    var index = message.GetArgument<int>();
                    m = m with
                    {
                        ShowGallery = false,
                        ShowSlideshow = true,
                        CurrentSlideshowIndex = index
                    };
                    break;
                }

            case MessageType.SetSlideshowIndex:
                {
                    if (m.ShowSlideshow == false) break;

                    var i = message.GetArgument<int>();
                    if (i < 0) i = 0;
                    if (i >= m.Filter.FilteredScreenshots.Count) i = m.Filter.FilteredScreenshots.Count - 1;
                    if (i == m.CurrentSlideshowIndex) break;

                    m = m with { CurrentSlideshowIndex = i };
                    
                    break;
                }


            case MessageType.ToggleSelectedTag:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedTag(x) };
                    if (m.ShowSlideshow) m = m with { CurrentSlideshowIndex = 0 };
                    break;
                }
            case MessageType.ToggleSelectedYear:
                {
                    var x = message.GetArgument<int>();
                    m = m with { Filter = m.Filter.ToggleSelectedYear(x) };
                    if (m.ShowSlideshow) m = m with { CurrentSlideshowIndex = 0 };
                    break;
                }
            case MessageType.ToggleSelectedUser:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedUser(x) };
                    if (m.ShowSlideshow) m = m with { CurrentSlideshowIndex = 0 };
                    break;
                }
            case MessageType.ToggleSelectedHostname:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedHostname(x) };
                    if (m.ShowSlideshow) m = m with { CurrentSlideshowIndex = 0 };
                    break;
                }
            case MessageType.ToggleSelectedProcess:
                {
                    var x = message.GetArgument<string>();
                    m = m with { Filter = m.Filter.ToggleSelectedProcess(x) };
                    if (m.ShowSlideshow) m = m with { CurrentSlideshowIndex = 0 };
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