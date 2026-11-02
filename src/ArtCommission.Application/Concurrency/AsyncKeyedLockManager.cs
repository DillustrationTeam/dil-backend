using System.Collections.Concurrent;

namespace ArtCommission.Application.Concurrency;

/// <summary>
/// Quản lý async lock theo khoá chuỗi (string key) với cơ chế đếm tham chiếu (ref-counted)
/// để tự giải phóng SemaphoreSlim khi không còn request nào chờ, tránh rò rỉ bộ nhớ.
/// Dùng để tuần tự hoá các request đồng thời trên cùng một tài nguyên (ví dụ: cùng một voter trong cùng một event).
/// </summary>
public static class AsyncKeyedLockManager
{
    private sealed class RefCountedSemaphore
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount = 1;
    }

    private static readonly ConcurrentDictionary<string, RefCountedSemaphore> Locks = new();

    public static async Task<IDisposable> LockAsync(string key, CancellationToken cancellationToken = default)
    {
        RefCountedSemaphore item;
        lock (Locks)
        {
            if (Locks.TryGetValue(key, out var existing))
            {
                existing.RefCount++;
                item = existing;
            }
            else
            {
                item = new RefCountedSemaphore();
                Locks[key] = item;
            }
        }

        await item.Semaphore.WaitAsync(cancellationToken);
        return new Releaser(key, item);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly string _key;
        private readonly RefCountedSemaphore _item;
        private bool _disposed;

        public Releaser(string key, RefCountedSemaphore item)
        {
            _key = key;
            _item = item;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _item.Semaphore.Release();

            lock (Locks)
            {
                _item.RefCount--;
                if (_item.RefCount == 0)
                {
                    Locks.TryRemove(_key, out _);
                    _item.Semaphore.Dispose();
                }
            }
        }
    }
}
