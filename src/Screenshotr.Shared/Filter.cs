using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Screenshotr;

public enum FilterSortingMode
{
    Created,
    CreatedDescending,
    Bytes,
    BytesDescending,
    TagsCount,
    TagsCountDescending,
}

public record IndexedScreenshots(
    ImmutableDictionary<string, Screenshot> All,
    ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>> PerTag,
    ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>> PerUser,
    ImmutableDictionary<int, ImmutableDictionary<string, Screenshot>> PerYear
    )
{
    public static readonly IndexedScreenshots Empty = new(
        ImmutableDictionary<string, Screenshot>.Empty,
        ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>>.Empty,
        ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>>.Empty,
        ImmutableDictionary<int, ImmutableDictionary<string, Screenshot>>.Empty
        );

    public static IndexedScreenshots Create(ImmutableDictionary<string, Screenshot> all)
    {
        var perTag = all.Values.SelectMany(x => x.Tags.Select(tag => (tag, x))).GroupBy(x => x.tag).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.x.Id, x => x.x));
        var perUser = all.Values.GroupBy(x => x.ImportInfo.Username).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Id));
        var perYear = all.Values.GroupBy(x => x.Year).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Id));
        return new(all, perTag, perUser, perYear);
    }

    public int Count => All.Count;

    public IndexedScreenshots UpsertScreenshot(Screenshot s)
    {
        var self = this;

        if (All.TryGetValue(s.Id, out var oldSelf))
        {
            self = self with { All = self.All.SetItem(s.Id, s) };

            foreach (var tag in s.Tags)
            {
                self = UpsertPerTag(self, tag, s);
            }

            foreach (var tag in oldSelf.Tags)
            {
                if (s.Tags.Contains(tag)) continue;
                self = RemovePerTag(self, tag, s);
            }

            if (s.Year != oldSelf.Year)
            {
                self = RemovePerYear(self, oldSelf);
            }

            self = UpsertPerYear(self, s);
        }
        else
        {
            self = self with { All = self.All.Add(s.Id, s) };

            foreach (var tag in s.Tags)
            {
                self = UpsertPerTag(self, tag, s);
            }

            self = UpsertPerYear(self, s);
        }

        return self;

        static IndexedScreenshots UpsertPerTag(IndexedScreenshots self, string tag, Screenshot s)
        {
            var perTagInner = self.PerTag.TryGetValue(tag, out var x0) ? x0 : ImmutableDictionary<string, Screenshot>.Empty;
            perTagInner = perTagInner.SetItem(s.Id, s);
            return self with { PerTag = self.PerTag.SetItem(tag, perTagInner) };
        }
        static IndexedScreenshots UpsertPerYear(IndexedScreenshots self, Screenshot s)
        {
            var perYearInner = self.PerYear.TryGetValue(s.Year, out var x0) ? x0 : ImmutableDictionary<string, Screenshot>.Empty;
            perYearInner = perYearInner.SetItem(s.Id, s);
            return self with { PerYear = self.PerYear.SetItem(s.Year, perYearInner) };
        }
        static IndexedScreenshots RemovePerTag(IndexedScreenshots self, string tag, Screenshot s)
        {
            if (self.PerTag.TryGetValue(tag, out var inner))
            {
                if (inner.ContainsKey(s.Id))
                {
                    inner = inner.Remove(s.Id);
                    if (inner.Count > 0)
                    {
                        return self with { PerTag = self.PerTag.SetItem(tag, inner) };
                    }
                    else
                    {
                        return self with { PerTag = self.PerTag.Remove(tag) };
                    }
                }
            }
            return self;
        }
        static IndexedScreenshots RemovePerYear(IndexedScreenshots self, Screenshot s)
        {
            if (self.PerYear.TryGetValue(s.Year, out var inner))
            {
                if (inner.ContainsKey(s.Id))
                {
                    inner = inner.Remove(s.Id);
                    if (inner.Count > 0)
                    {
                        return self with { PerYear = self.PerYear.SetItem(s.Year, inner) };
                    }
                    else
                    {
                        return self with { PerYear = self.PerYear.Remove(s.Year) };
                    }
                }
            }
            return self;
        }
    }
}

public record Filter(
    IndexedScreenshots AllScreenshots,
    ImmutableHashSet<string> SelectedUsers,
    ImmutableHashSet<string> SelectedTags,
    ImmutableHashSet<int> SelectedYears,
    FilterSortingMode SortingMode,
    int Skip, int Take
    )
{
    public static Filter Empty = new Filter(
        AllScreenshots: IndexedScreenshots.Empty,
        SelectedUsers: ImmutableHashSet<string>.Empty,
        SelectedTags: ImmutableHashSet<string>.Empty,
        SelectedYears: ImmutableHashSet<int>.Empty,
        SortingMode: FilterSortingMode.CreatedDescending,
        Skip: 0, Take: 0
        )
        .ComputeCache();

    public IEnumerable<Screenshot> FilteredScreenshots => _cacheFilteredScreenshots;

    public IEnumerable<(string, int)> FilteredTags => _cacheFilteredTags;

    public IEnumerable<(string, int)> FilteredUsers => _cacheFilteredUsers;

    public ImmutableList<(int, int)> FilteredYears => _cacheFilteredYears;

    public int CountAll => AllScreenshots.Count;

    public int CountFiltered => _cacheFilteredScreenshots.Count;

    public static Filter Create(ImmutableDictionary<string, Screenshot> allScreenshots, FilterSortingMode sortingMode, int take)
    {
        //var allTags = allScreenshots.Values
        //    .AsParallel()
        //    .SelectMany(x => x.Tags.Select(tag => (Tag: tag, Screenshot: x)))
        //    .GroupBy(x => x.Tag)
        //    .ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Screenshot.Id, x => x.Screenshot))
        //    ;

        //var allYears = allScreenshots.Values
        //    .AsParallel()
        //    .Select(x => (x.Created.Year, Screenshot: x))
        //    .GroupBy(x => x.Year)
        //    .ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Screenshot.Id, x => x.Screenshot))
        //    ;

        var self = new Filter(
            AllScreenshots: IndexedScreenshots.Create(allScreenshots),
            SelectedUsers: ImmutableHashSet<string>.Empty,
            SelectedTags: ImmutableHashSet<string>.Empty,
            SelectedYears: ImmutableHashSet<int>.Empty,
            SortingMode: sortingMode,
            Skip: 0, Take: take
            );

        return self.ComputeCache();
    }

    public Filter ToggleSelectedYear(int x) =>
        (this with { SelectedYears = SelectedYears.Contains(x) ? SelectedYears.Remove(x) : SelectedYears.Add(x) })
        .ComputeCache();

    public Filter ToggleSelectedTag(string x) =>
        (this with { SelectedTags = SelectedTags.Contains(x) ? SelectedTags.Remove(x) : SelectedTags.Add(x) })
        .ComputeCache();

    public Filter ToggleSelectedUser(string x) =>
        (this with { SelectedUsers = SelectedUsers.Contains(x) ? SelectedUsers.Remove(x) : SelectedUsers.Add(x) })
        .ComputeCache();

    public Filter UpsertScreenshot(Screenshot screenshot) =>
        (this with { AllScreenshots = AllScreenshots.UpsertScreenshot(screenshot) })
        .ComputeCache();

    private Filter ComputeCache()
    {
        {
            var xs = AllScreenshots.All.Values.AsParallel();

            if (SelectedYears.Count > 0)
                xs = xs.Where(x => SelectedYears.Contains(x.Created.Year));

            if (SelectedUsers.Count > 0)
                xs = xs.Where(x => SelectedUsers.Contains(x.ImportInfo.Username));

            if (SelectedTags.Count > 0)
                xs = xs.Where(x => x.Tags.Any(SelectedTags.Contains));

            xs = SortingMode switch
            {
                FilterSortingMode.Created             => xs.OrderBy          (x => x.Created   ),
                FilterSortingMode.CreatedDescending   => xs.OrderByDescending(x => x.Created   ),

                FilterSortingMode.Bytes               => xs.OrderBy          (x => x.Bytes     ),
                FilterSortingMode.BytesDescending     => xs.OrderByDescending(x => x.Bytes     ),

                FilterSortingMode.TagsCount           => xs.OrderBy          (x => x.Tags.Count),
                FilterSortingMode.TagsCountDescending => xs.OrderByDescending(x => x.Tags.Count),

                _ => throw new NotImplementedException($"{SortingMode}")
            };

            //xs = xs.Skip(Skip).Take(Take);

            _cacheFilteredScreenshots = xs.ToImmutableList();
        }

        {
            var tags = AllScreenshots.PerTag.Keys;

            var xs = SelectedYears.IsEmpty
                ? tags.AsParallel().Select(tag => (tag, count: AllScreenshots.PerTag[tag].Count))
                : tags.AsParallel().Select(tag => (tag, count: AllScreenshots.PerTag[tag].Count(x => SelectedYears.Contains(x.Value.Created.Year))))
                ;

            xs = xs
                .Where(x => x.count > 0 || SelectedTags.Contains(x.tag))
                .OrderBy(x => x.tag);

            _cacheFilteredTags = xs.ToImmutableList();
        }

        {
            var users = AllScreenshots.PerUser.Keys;

            var xs = SelectedYears.IsEmpty
                ? users.AsParallel().Select(user => (user, count: AllScreenshots.PerUser[user].Count))
                : users.AsParallel().Select(user => (user, count: AllScreenshots.PerUser[user].Count(x => SelectedUsers.Contains(x.Value.ImportInfo.Username))))
                ;

            xs = xs
                .Where(x => x.count > 0 || SelectedTags.Contains(x.user))
                .OrderBy(x => x.user);

            _cacheFilteredUsers = xs.ToImmutableList();
        }

        {
            var years = AllScreenshots.PerYear.Keys;

            var xs = SelectedTags.IsEmpty
                ? years.AsParallel().Select(year => (year, count: AllScreenshots.PerYear[year].Count))
                : years.AsParallel().Select(year => (year, count: AllScreenshots.PerYear[year].Count(x => x.Value.Tags.Any(SelectedTags.Contains))))
                ;

            xs = xs
                .Where(x => x.count > 0 || SelectedYears.Contains(x.year))
                .OrderBy(x => x.year);

            _cacheFilteredYears = xs.ToImmutableList();
        }

        return this;
    }

    private ImmutableList<Screenshot> _cacheFilteredScreenshots = null!;
    private ImmutableList<(string, int)> _cacheFilteredTags = null!;
    private ImmutableList<(string, int)> _cacheFilteredUsers = null!;
    private ImmutableList<(int, int)> _cacheFilteredYears = null!;
}