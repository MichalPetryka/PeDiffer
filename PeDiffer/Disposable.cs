using System;
using System.Threading;

namespace PeDiffer;

internal sealed class Disposable<T>(T value, Action<T> disposer) : IDisposable
{
    public T? Value { get; private set; } = value ?? throw new ArgumentNullException(nameof(value));
    private Action<T>? _disposer = disposer ?? throw new ArgumentNullException(nameof(disposer));

    public static Disposable<T> Create(T value, Action<T> disposer) => new(value, disposer);

    private void Free()
    {
        Interlocked.Exchange(ref _disposer, null)?.Invoke(Value ?? throw new InvalidOperationException());
        Value = default;

        GC.KeepAlive(this);
    }

    public void Dispose()
    {
        Free();
        GC.SuppressFinalize(this);
    }

    ~Disposable()
    {
        Free();
    }
}
