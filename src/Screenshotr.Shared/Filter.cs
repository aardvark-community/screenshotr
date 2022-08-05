#undef PRINT_TIMING

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    ImmutableDictionary<int   , ImmutableDictionary<string, Screenshot>> PerYear,
    ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>> PerUser,
    ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>> PerHostname,
    ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>> PerProcess
    )
{
    public static readonly IndexedScreenshots Empty = new(
        ImmutableDictionary<string, Screenshot>.Empty,
        ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>>.Empty,
        ImmutableDictionary<int   , ImmutableDictionary<string, Screenshot>>.Empty,
        ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>>.Empty,
        ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>>.Empty,
        ImmutableDictionary<string, ImmutableDictionary<string, Screenshot>>.Empty
        );

    public static IndexedScreenshots Create(ImmutableDictionary<string, Screenshot> all)
    {
        var perTag = all.Values.SelectMany(x => x.Tags.Select(tag => (tag, x))).GroupBy(x => x.tag).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.x.Id, x => x.x));
        var perYear     = all.Values.GroupBy(x => x.Year               ).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Id));
        var perUser     = all.Values.GroupBy(x => x.ImportInfo.Username).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Id));
        var perHostname = all.Values.GroupBy(x => x.ImportInfo.Hostname).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Id));
        var perProcess  = all.Values.GroupBy(x => x.ImportInfo.Process ).ToImmutableDictionary(g => g.Key, g => g.ToImmutableDictionary(x => x.Id));
        return new(all, perTag, perYear, perUser, perHostname, perProcess);
    }

    public int Count => All.Count;

    public IndexedScreenshots UpsertScreenshot(Screenshot s)
    {
        var self = this;

        self = self with { All = self.All.SetItem(s.Id, s) };

        foreach (var tag in s.Tags)
        {
            self = UpsertPerTag(self, tag, s);
        }

        self = UpsertPerYear(self, s);
        self = UpsertPerUser(self, s);
        self = UpsertPerHostname(self, s);
        self = UpsertPerProcess(self, s);

        if (All.TryGetValue(s.Id, out var oldSelf))
        {
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
            if (s.ImportInfo.Username != oldSelf.ImportInfo.Username)
            {
                self = RemovePerUser(self, oldSelf);
            }
            if (s.ImportInfo.Hostname != oldSelf.ImportInfo.Hostname)
            {
                self = RemovePerHostname(self, oldSelf);
            }
            if (s.ImportInfo.Process != oldSelf.ImportInfo.Process)
            {
                self = RemovePerProcess(self, oldSelf);
            }
        }

        return self;

        static IndexedScreenshots UpsertPerTag(IndexedScreenshots self, string tag, Screenshot s)
        {
            var inner = self.PerTag.TryGetValue(tag, out var x0) ? x0 : ImmutableDictionary<string, Screenshot>.Empty;
            inner = inner.SetItem(s.Id, s);
            return self with { PerTag = self.PerTag.SetItem(tag, inner) };
        }
        static IndexedScreenshots UpsertPerYear(IndexedScreenshots self, Screenshot s)
        {
            var inner = self.PerYear.TryGetValue(s.Year, out var x0) ? x0 : ImmutableDictionary<string, Screenshot>.Empty;
            inner = inner.SetItem(s.Id, s);
            return self with { PerYear = self.PerYear.SetItem(s.Year, inner) };
        }
        static IndexedScreenshots UpsertPerUser(IndexedScreenshots self, Screenshot s)
        {
            var inner = self.PerUser.TryGetValue(s.ImportInfo.Username, out var x0) ? x0 : ImmutableDictionary<string, Screenshot>.Empty;
            inner = inner.SetItem(s.Id, s);
            return self with { PerUser = self.PerUser.SetItem(s.ImportInfo.Username, inner) };
        }
        static IndexedScreenshots UpsertPerHostname(IndexedScreenshots self, Screenshot s)
        {
            var inner = self.PerHostname.TryGetValue(s.ImportInfo.Hostname, out var x0) ? x0 : ImmutableDictionary<string, Screenshot>.Empty;
            inner = inner.SetItem(s.Id, s);
            return self with { PerHostname = self.PerHostname.SetItem(s.ImportInfo.Hostname, inner) };
        }
        static IndexedScreenshots UpsertPerProcess(IndexedScreenshots self, Screenshot s)
        {
            var inner = self.PerProcess.TryGetValue(s.ImportInfo.Process, out var x0) ? x0 : ImmutableDictionary<string, Screenshot>.Empty;
            inner = inner.SetItem(s.Id, s);
            return self with { PerProcess = self.PerProcess.SetItem(s.ImportInfo.Process, inner) };
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
        static IndexedScreenshots RemovePerUser(IndexedScreenshots self, Screenshot s)
        {
            if (self.PerUser.TryGetValue(s.ImportInfo.Username, out var inner))
            {
                if (inner.ContainsKey(s.Id))
                {
                    inner = inner.Remove(s.Id);
                    if (inner.Count > 0)
                    {
                        return self with { PerUser = self.PerUser.SetItem(s.ImportInfo.Username, inner) };
                    }
                    else
                    {
                        return self with { PerUser = self.PerUser.Remove(s.ImportInfo.Username) };
                    }
                }
            }
            return self;
        }
        static IndexedScreenshots RemovePerHostname(IndexedScreenshots self, Screenshot s)
        {
            if (self.PerHostname.TryGetValue(s.ImportInfo.Hostname, out var inner))
            {
                if (inner.ContainsKey(s.Id))
                {
                    inner = inner.Remove(s.Id);
                    if (inner.Count > 0)
                    {
                        return self with { PerHostname = self.PerHostname.SetItem(s.ImportInfo.Hostname, inner) };
                    }
                    else
                    {
                        return self with { PerHostname = self.PerHostname.Remove(s.ImportInfo.Hostname) };
                    }
                }
            }
            return self;
        }
        static IndexedScreenshots RemovePerProcess(IndexedScreenshots self, Screenshot s)
        {
            if (self.PerProcess.TryGetValue(s.ImportInfo.Process, out var inner))
            {
                if (inner.ContainsKey(s.Id))
                {
                    inner = inner.Remove(s.Id);
                    if (inner.Count > 0)
                    {
                        return self with { PerProcess = self.PerProcess.SetItem(s.ImportInfo.Process, inner) };
                    }
                    else
                    {
                        return self with { PerProcess = self.PerProcess.Remove(s.ImportInfo.Process) };
                    }
                }
            }
            return self;
        }
    }
}

public record Filter(
    IndexedScreenshots AllScreenshots,
    string LiveSearch,
    ImmutableHashSet<string> SelectedTags,
    ImmutableHashSet<int>    SelectedYears,
    ImmutableHashSet<string> SelectedUsers,
    ImmutableHashSet<string> SelectedHostnames,
    ImmutableHashSet<string> SelectedProcesses,
    FilterSortingMode SortingMode,
    int Skip, int Take
    )
{
    public static Filter Empty = new Filter(
        AllScreenshots   : IndexedScreenshots.Empty,
        LiveSearch       : string.Empty,
        SelectedTags     : ImmutableHashSet<string>.Empty,
        SelectedYears    : ImmutableHashSet<int   >.Empty,
        SelectedUsers    : ImmutableHashSet<string>.Empty,
        SelectedHostnames: ImmutableHashSet<string>.Empty,
        SelectedProcesses: ImmutableHashSet<string>.Empty,
        SortingMode: FilterSortingMode.CreatedDescending,
        Skip: 0, Take: 0
        )
        .ComputeCache();

    public IReadOnlyList<Screenshot>    FilteredScreenshots  => _cacheFilteredScreenshots;
    public IReadOnlyList<(string, int)> FilteredTags         => _cacheFilteredTags;
    public IReadOnlyList<(int, int)>    FilteredYears        => _cacheFilteredYears;
    public IReadOnlyList<(string, int)> FilteredUsers        => _cacheFilteredUsers;
    public IReadOnlyList<(string, int)> FilteredHostnames    => _cacheFilteredHostnames;
    public IReadOnlyList<(string, int)> FilteredProcesses    => _cacheFilteredProcesses;

    public int CountAll => AllScreenshots.Count;

    public int CountFiltered => _cacheFilteredScreenshots.Count;

    public static Filter Create(ImmutableDictionary<string, Screenshot> allScreenshots, FilterSortingMode sortingMode, int take)
    {
        var self = new Filter(
            AllScreenshots   : IndexedScreenshots.Create(allScreenshots),
            LiveSearch       : string.Empty,
            SelectedTags     : ImmutableHashSet<string>.Empty,
            SelectedYears    : ImmutableHashSet<int   >.Empty,
            SelectedUsers    : ImmutableHashSet<string>.Empty,
            SelectedHostnames: ImmutableHashSet<string>.Empty,
            SelectedProcesses: ImmutableHashSet<string>.Empty,
            SortingMode: sortingMode, Skip: 0, Take: take
            );

        return self.ComputeCache();
    }

    public Filter ResetFilter() =>
        (this with { 
            SelectedTags      = ImmutableHashSet<string>.Empty,
            SelectedYears     = ImmutableHashSet<int   >.Empty,
            SelectedUsers     = ImmutableHashSet<string>.Empty,
            SelectedHostnames = ImmutableHashSet<string>.Empty,
            SelectedProcesses = ImmutableHashSet<string>.Empty,
        })
        .ComputeCache();

    public Filter ToggleSelectedTag(string x) =>
        (this with { SelectedTags = SelectedTags.Contains(x) ? SelectedTags.Remove(x) : SelectedTags.Add(x) })
        .ComputeCache();
    public Filter ToggleSelectedYear(int x) =>
        (this with { SelectedYears = SelectedYears.Contains(x) ? SelectedYears.Remove(x) : SelectedYears.Add(x) })
        .ComputeCache();
    public Filter ToggleSelectedUser(string x) =>
        (this with { 
            SelectedUsers     = SelectedUsers.Contains(x) ? SelectedUsers.Remove(x) : SelectedUsers.Add(x),
            SelectedHostnames = SelectedHostnames.Clear(),
            SelectedProcesses = SelectedProcesses.Clear()
        })
        .ComputeCache();
    public Filter ToggleSelectedHostname(string x) =>
        (this with {
            SelectedUsers     = SelectedUsers.Clear(),
            SelectedHostnames = SelectedHostnames.Contains(x) ? SelectedHostnames.Remove(x) : SelectedHostnames.Add(x),
            SelectedProcesses = SelectedProcesses.Clear()
        })
        .ComputeCache();
    public Filter ToggleSelectedProcess(string x) =>
        (this with
        {
            SelectedUsers     = SelectedUsers.Clear(),
            SelectedHostnames = SelectedHostnames.Clear(),
            SelectedProcesses = SelectedProcesses.Contains(x) ? SelectedProcesses.Remove(x) : SelectedProcesses.Add(x) 
        })
        .ComputeCache();


    public Filter UpsertScreenshot(Screenshot screenshot) =>
        (this with { AllScreenshots = AllScreenshots.UpsertScreenshot(screenshot) })
        .ComputeCache();

    private Filter ComputeCache()
    {
#if PRINT_TIMING
        var debugGuid = Guid.NewGuid();
        var sw = new Stopwatch(); sw.Restart();
        Console.WriteLine($"[DEBUG {debugGuid}] Filter.ComputeCache");
#endif
        {
            var xs = AllScreenshots.All.Values.AsParallel();

            if (SelectedTags.Count > 0)
            {
                if (SelectedTags.Contains("!hide"))
                {
                    xs = xs.Where(x => x.Tags.Any(SelectedTags.Contains));
                }
                else
                {
                    xs = xs
                        .Where(x => !x.Tags.Contains("!hide"))
                        .Where(x => x.Tags.Any(SelectedTags.Contains));
                }
            }
            else
            {
                xs = xs.Where(x => !x.Tags.Contains("!hide"));
            }

            if (SelectedYears.Count > 0)
                xs = xs.Where(x => SelectedYears.Contains(x.Created.Year));

            if (SelectedUsers.Count > 0)
                xs = xs.Where(x => SelectedUsers.Contains(x.ImportInfo.Username));

            if (SelectedHostnames.Count > 0)
                xs = xs.Where(x => SelectedHostnames.Contains(x.ImportInfo.Hostname));

            if (SelectedProcesses.Count > 0)
                xs = xs.Where(x => SelectedProcesses.Contains(x.ImportInfo.Process));

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

#if PRINT_TIMING
            Console.WriteLine($"[DEBUG {debugGuid}] {sw.Elapsed} | A"); sw.Restart();
#endif
        }

        // tags
        {
            var tags = AllScreenshots.PerTag.Keys;

            var xs = SelectedYears.IsEmpty
                ? tags.AsParallel().Select(tag => (tag, count: AllScreenshots.PerTag[tag].Count))
                : tags.AsParallel().Select(tag => (tag, count: AllScreenshots.PerTag[tag].Count(x => SelectedYears.Contains(x.Value.Created.Year))))
                ;

            if (SelectedUsers.Count > 0)
            {
                xs = xs.AsParallel().Select(x => (x.tag, count: AllScreenshots.PerTag[x.tag].Count(x => SelectedUsers.Contains(x.Value.ImportInfo.Username))));
            }
            if (SelectedHostnames.Count > 0)
            {
                xs = xs.AsParallel().Select(x => (x.tag, count: AllScreenshots.PerTag[x.tag].Count(x => SelectedHostnames.Contains(x.Value.ImportInfo.Hostname))));
            }
            if (SelectedProcesses.Count > 0)
            {
                xs = xs.AsParallel().Select(x => (x.tag, count: AllScreenshots.PerTag[x.tag].Count(x => SelectedProcesses.Contains(x.Value.ImportInfo.Process))));
            }
            xs = xs
                .Where(x => x.count > 0 || SelectedTags.Contains(x.tag))
                .OrderBy(x => x.tag);

            _cacheFilteredTags = xs.ToImmutableList();

#if PRINT_TIMING
            Console.WriteLine($"[DEBUG {debugGuid}] {sw.Elapsed} | B"); sw.Restart();
#endif
        }

        // years
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

#if PRINT_TIMING
            Console.WriteLine($"[DEBUG {debugGuid}] {sw.Elapsed} | C"); sw.Restart();
#endif
        }

        // users
        {
            var users = AllScreenshots.PerUser.Keys;

            var xs = SelectedTags.IsEmpty
                ? users.AsParallel().Select(user => (user, count: AllScreenshots.PerUser[user].Count))
                : users.AsParallel().Select(user => (user, count: AllScreenshots.PerUser[user].Count(x => x.Value.Tags.Any(SelectedTags.Contains))))
                ;

            xs = xs
                .Where(x => x.count > 0 || SelectedUsers.Contains(x.user))
                .OrderBy(x => x.user);

            _cacheFilteredUsers = xs.ToImmutableList();

#if PRINT_TIMING
            Console.WriteLine($"[DEBUG {debugGuid}] {sw.Elapsed} | D"); sw.Restart();
#endif
        }

        // hostnames
        {
            var hostnames = AllScreenshots.PerHostname.Keys;

            var xs = SelectedTags.IsEmpty
                ? hostnames.AsParallel().Select(hostname => (hostname, count: AllScreenshots.PerHostname[hostname].Count))
                : hostnames.AsParallel().Select(hostname => (hostname, count: AllScreenshots.PerHostname[hostname].Count(x => x.Value.Tags.Any(SelectedTags.Contains))))
                ;

            xs = xs
                .Where(x => x.count > 0 || SelectedHostnames.Contains(x.hostname))
                .OrderBy(x => x.hostname);

            _cacheFilteredHostnames = xs.ToImmutableList();

#if PRINT_TIMING
            Console.WriteLine($"[DEBUG {debugGuid}] {sw.Elapsed} | E"); sw.Restart();
#endif
        }

        // processes
        {
            var processes = AllScreenshots.PerProcess.Keys;

            var xs = SelectedTags.IsEmpty
                ? processes.AsParallel().Select(process => (process, count: AllScreenshots.PerProcess[process].Count))
                : processes.AsParallel().Select(process => (process, count: AllScreenshots.PerProcess[process].Count(x => x.Value.Tags.Any(SelectedTags.Contains))))
                ;

            xs = xs
                .Where(x => x.count > 0 || SelectedProcesses.Contains(x.process))
                .OrderBy(x => x.process);

            _cacheFilteredProcesses = xs.ToImmutableList();

#if PRINT_TIMING
            Console.WriteLine($"[DEBUG {debugGuid}] {sw.Elapsed} | F"); sw.Restart();
#endif
        }

        return this;
    }

    private IReadOnlyList<Screenshot>    _cacheFilteredScreenshots = null!;
    private IReadOnlyList<(string, int)> _cacheFilteredTags        = null!;
    private IReadOnlyList<(int   , int)> _cacheFilteredYears       = null!;
    private IReadOnlyList<(string, int)> _cacheFilteredUsers       = null!;
    private IReadOnlyList<(string, int)> _cacheFilteredHostnames   = null!;
    private IReadOnlyList<(string, int)> _cacheFilteredProcesses   = null!;
}