using HidSharp;

namespace RoomLogg.Device;

public abstract class HidStreamManager(int vendorId, int productId) : IDisposable
{
    private bool _disposed;
    private HidStream? _stream;

    public bool IsConnected => !_disposed && _stream is { CanRead: true, CanWrite: true };

    public void Dispose()
    {
        if (_disposed)
            return;

        var wasConnected = _stream is not null;
        _disposed = true;
        CloseStream();

        if (wasConnected)
            Disconnected?.Invoke(this, EventArgs.Empty);

        GC.SuppressFinalize(this);
    }

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public bool TryConnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stream is { CanRead: true, CanWrite: true })
            return true;

        if (_stream is not null)
        {
            CloseStream();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        try
        {
            if (DeviceList.Local.TryGetHidDevice(out var hidDevice, vendorId, productId)
                && hidDevice is not null
                && hidDevice.TryOpen(out var stream)
                && stream is { CanRead: true, CanWrite: true })
            {
                _stream = stream;
                stream.Closed += OnStreamClosed;
                stream.InterruptRequested += OnInterruptRequested;
                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }

            CloseStream();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException
                                              or UnauthorizedAccessException or InvalidOperationException)
        {
            CloseStream();
        }

        return false;
    }

    protected TResult ExecuteOnStream<TResult>(Func<HidStream, TResult> command, TResult disconnectedResult)
    {
        if (_disposed || _stream is not { CanRead: true, CanWrite: true } stream)
            return disconnectedResult;

        try
        {
            return command(stream);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            HandleDisconnected(stream);
            return disconnectedResult;
        }
    }

    private void OnStreamClosed(object? sender, EventArgs e)
    {
        if (sender is HidStream stream)
            HandleDisconnected(stream);
    }

    private void OnInterruptRequested(object? sender, EventArgs e)
    {
        if (sender is HidStream stream)
            HandleDisconnected(stream);
    }

    private void HandleDisconnected(HidStream? stream)
    {
        if (_disposed || _stream is null || (stream is not null && !ReferenceEquals(stream, _stream)))
            return;

        CloseStream();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private void CloseStream()
    {
        var stream = _stream;
        if (stream is null)
            return;

        _stream = null;
        stream.Closed -= OnStreamClosed;
        stream.InterruptRequested -= OnInterruptRequested;
        try
        {
            stream.Dispose();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }
}