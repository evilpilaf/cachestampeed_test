using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Caching.Memory;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace cache_stampeed;

public class UnitTest1
{
    [Fact]
    public async Task CacheStampeed_Polly()
    {
        int externalCalls = 0;
        int concurrency = 100;

        using MemoryCache memCache = new(new MemoryCacheOptions());

        var cacheProvider = new MemoryCacheProvider(memCache);

        var policy = Policy.CacheAsync(
            cacheProvider,
            TimeSpan.FromHours(1)
        );

        await Parallel.ForEachAsync(Enumerable.Range(1, 1_000),
            new ParallelOptions() { MaxDegreeOfParallelism = concurrency }, async (i, token) =>
            {
                _ = await policy.ExecuteAsync(async ctx =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    Interlocked.Increment(ref externalCalls);

                    return "MyValue";
                }, new Context("MyKey"));
            });

        externalCalls.Should().Be(1);
    }

    [Fact]
    public async Task CacheStampeed_Fusion()
    {
        int externalCalls = 0;
        int concurrency = 100;

        var cache = new FusionCache(new FusionCacheOptions()
        {
            DefaultEntryOptions = new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.Low
            }
        });

        await Parallel.ForEachAsync(Enumerable.Range(1, 1_000),
            new ParallelOptions() { MaxDegreeOfParallelism = concurrency }, async (i, token) =>
            {
                _ = await cache.GetOrSetAsync("MyKey", async _ =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

                        Interlocked.Increment(ref externalCalls);

                        return "MyValue";
                    },
                    token: CancellationToken.None);
            });

        externalCalls.Should().Be(1);
    }
}