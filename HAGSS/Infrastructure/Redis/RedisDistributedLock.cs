using StackExchange.Redis;

namespace HAGSS.Infrastructure.Redis;

public interface IRedisDistributedLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string resourceKey, TimeSpan expiry, TimeSpan wait, CancellationToken cancellationToken);
}

public sealed class RedisDistributedLock(IConnectionMultiplexer redis) : IRedisDistributedLock
{
    private const string ReleaseScript = """
        if redis.call("get", KEYS[1]) == ARGV[1] then
            return redis.call("del", KEYS[1])
        else
            return 0
        end
        """;

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resourceKey,
        TimeSpan expiry,
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var lockValue = Guid.NewGuid().ToString("N");
        var key = $"lock:{resourceKey}";
        var deadline = DateTime.UtcNow.Add(wait);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await db.StringSetAsync(key, lockValue, expiry, When.NotExists))
                return new RedisLockHandle(db, key, lockValue);

            await Task.Delay(50, cancellationToken);
        }

        return null;
    }

    private sealed class RedisLockHandle(IDatabase db, string key, string lockValue) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await db.ScriptEvaluateAsync(ReleaseScript, [key], [lockValue]);
        }
    }
}
