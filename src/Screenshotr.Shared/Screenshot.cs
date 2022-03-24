using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screenshotr;

public record Custom(ImmutableList<Custom.Entry> Entries)
{
    public static readonly Custom Empty = new(ImmutableList<Entry>.Empty);

    public static Custom Create(IEnumerable<Entry>? entries)
        => new(entries?.ToImmutableList() ?? ImmutableList<Entry>.Empty);

    public static Custom Create(IEnumerable<(string Key, object Value)>? entries)
        => new(entries?.Select(Entry.Create)?.ToImmutableList() ?? ImmutableList<Entry>.Empty);

    public Custom Add(Entry e) => this with { Entries = Entries.Add(e) };
    public Custom Add(string key, object value) => this with { Entries = Entries.Add(Entry.Create(key, value)) };

    [JsonIgnore]
    public int Count => Entries.Count;

    public record Entry(string Key, object Value)
    {
        public static Entry Create((string Key, object Value) x) => new(x.Key, x.Value); 
        public static Entry Create(string key, object value) => new(key, value);

        public T GetAs<T>() where T : class
        {
            if (Value is T x) return x;
            if (Value is JsonElement j) return JsonSerializer.Deserialize<T>(j, Utils.JsonOptions) ?? throw new Exception($"Failed to deserialize {j}.");
            throw new Exception($"Failed to convert {Value} to {typeof(T)}.");
        }
    }
}

public record Screenshot(
    string Id,
    DateTimeOffset Created,
    long Bytes,
    V2i Size,
    ImmutableHashSet<string> Tags,
    Custom Custom,
    ImportInfo ImportInfo
    )
{
    [JsonIgnore]
    public int Year => Created.Year;

    [JsonIgnore]
    public string RelPathFullRes => $"{RelPath}/{Filename}.jpg";

    [JsonIgnore]
    public string RelPathThumb => $"{RelPath}/{Filename}.thumb.png";

    [JsonIgnore]
    public string RelPathMeta => $"{RelPath}/{Filename}.json";

    private string RelPath => $"data/{Created.Year:0000}/{Created.Month:00}/{Created.Day:00}";

    private string Filename
    {
        get 
        {
            var ts = Created;
            return $"{ts.Year:0000}{ts.Month:00}{ts.Day:00}-{ts.Hour:00}{ts.Minute:00}{ts.Second:00}-{Id}";
        }
    }

    public Screenshot AddTag(string tag)
    {
        if (Tags.Contains(tag)) return this;
        return this with { Tags = Tags.Add(tag) };
    }

    public string ToJson() => JsonSerializer.Serialize(this, Utils.JsonOptions);

    public static Screenshot ParseJson(string json) => JsonSerializer
        .Deserialize<Screenshot>(json, Utils.JsonOptions)
        ?? throw new Exception($"Failed to parse JSON {json}.")
        ;
}