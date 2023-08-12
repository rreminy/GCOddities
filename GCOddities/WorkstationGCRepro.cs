using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace GCOddities
{
    [Config(typeof(Config))]
    [MemoryDiagnoser]
    public class WorkstationGCRepro
    {
        private class Config : ManualConfig
        {
            public Config()
            {
                this.AddJob(Job.Default
                    .WithGcServer(false));

                this.AddJob(Job.Default
                    .WithGcServer(true));
            }
        }

        private static ParallelOptions GetParallelOptions(bool parallel = false)
        {
            var options = new ParallelOptions();
            if (!parallel) options.MaxDegreeOfParallelism = 1;
            return options;
        }

        #region Custom string interner
        private static readonly ConcurrentDictionary<string, string> s_strings = new();

        // Custom interner used to reduce memory usage
        public static string Intern(string str) => s_strings.GetOrAdd(str, str);
        #endregion

        #region Test item
        public sealed class TestItem
        {
            private int _a;
            private int _b;
            private int _c;
            private int _d;
            private string[]? _indexKeys;

            // There was a bug in the workload causing "Key" to generate the string on each call.
            // This has been corrected by caching properly.
            // Simulate this bug by using =>, along with the fact that it was regenerated anyway after interning!
            // Note: All the GC issue in my workload was resolved by fixing this, along with a custom version of the ConcurrentHashSet to avoid allocations.
            public string Key => $"{Intern($"{this._a}_{this._b}_{this._c}_{this._d}")}";

            // Indexes were cached properly
            public IEnumerable<string> IndexKeys => this._indexKeys ??= this.GenerateIndexKeys();

            // Optimization buster, this is just in case compiler is optimizing Key with the fact that they're only read.
            // This obviously did not exist on my workload.
            public void Set(int a, int b, int c, int d) { this._a = a; this._b = b; this._c = c; this._d = d; }

            // Indexes were more elaborate than this, however for this workload we'll unly do permutations
            private string[] GenerateIndexKeys()
            {
                return new string[]
                {
                    Intern($"a{this._a}"), Intern($"b{this._b}"), Intern($"c{this._c}"), Intern($"a{this._a}_b{this._b}"), Intern($"a{this._a}_c{this._c}"), Intern($"b{this._b}_a{this._a}"), Intern($"b{this._b}_c{this._c}"), Intern($"c{this._c}_a{this._a}"), Intern($"b{this._c}_b{this._b}"),
                    Intern($"a{this._a}_b{this._b}_c{this._c}"), Intern($"a{this._a}_c{this._c}_b{this._b}"), Intern($"b{this._b}_a{this._a}_c{this._c}"), Intern($"b{this._b}_c{this._c}_a{this._a}"), Intern($"c{this._c}_a{this._a}_b{this._b}"), Intern($"c{this._c}_b{this._b}_a{this._a}"),
                    // this._d intentionally left out
                };
            }

            public TestItem(int a, int b, int c, int d)
            {
                this._a = a;
                this._b = b;
                this._c = c;
                this._d = d;
            }

            public unsafe override int GetHashCode()
            {
                fixed (char* ptr = this.Key)
                {
                    // There is an actual fixed inside the GetHashCode call in my workload
                    // This is an attempt to simulate it. No clue if its being optimized out.
                    return this.Key.GetHashCode();
                }
            }

            // Equals is based on its key. Note the calls to .Key
            public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is TestItem item && this.Key.Equals(item.Key));
        }
        #endregion

        #region Actual workload
        private IReadOnlyDictionary<string, TestItem> _items = default!;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<TestItem, TestItem>> _index = new();

        // They're not exactly random and is actually based on whatever data is fed
        // For this reproduction however, they're random
        private static IEnumerable<TestItem> GenerateItems(int count)
        {
            var random = new Random(42); // Reproducible random
            for (var i = 0; i < count; i++)
            {
                yield return new(random.Next(20) + 15, random.Next(100) + 1000, random.Next(5), random.Next(100) + 100);
            }
        }

        [Params(250000)]
        public int ItemCount
        {
            get => this._items.Count; // Can be less but due to duplicates lets not worry about this
            set
            {
                // This is also the constructor in this case.

                var items = new Dictionary<string, TestItem>();
                Console.WriteLine("Generating items");
                var sw = Stopwatch.StartNew();
                foreach (var item in GenerateItems(value)) items[item.Key] = item;
                sw.Stop();
                Console.WriteLine($"Generated {items.Count} items in ({sw.ElapsedMilliseconds}ms)");
                this._items = items;

                Console.WriteLine("Building indexes");
                sw.Restart();
                this.RebuildIndexes(false);
                sw.Stop();
                Console.WriteLine($"Index building complete: {this._index.Count} (in ({sw.ElapsedMilliseconds}ms)");
            }
        }

        [Benchmark]
        [Arguments(true)]
        [Arguments(false)]
        // Rebuild indexes workload.
        // This doesn't actually run all the time, however the issues was experienced here.
        public void RebuildIndexes(bool parallel)
        {
            var dict = this._index;
            this._index.Clear();

            Parallel.ForEach(this._items.Values, GetParallelOptions(parallel), item =>
            {
                foreach (var indexKey in item.IndexKeys)
                {
                    var set = dict.GetOrAdd(indexKey, _ => new());
                    set.TryAdd(item, item); // Assert: true
                }
            });
        }
        #endregion
    }
}
