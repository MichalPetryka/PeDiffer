using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;

using TerraFX.Interop.Windows;

namespace PeDiffer;

internal sealed unsafe class DataModule : IDisposable
{
    private SafeFileHandle? _fileHandle;
    private Disposable<HANDLE>? _mappingHandle;
    private Disposable<nuint>? _viewHandle;
    private readonly uint _size;

    public byte* Data => (byte*)_viewHandle!.Value;

    public ReadOnlySpan<byte> Span => new(Data, checked((int)_size));

    public Architecture Architecture { get; }

    public DataModule(string path)
    {
        _fileHandle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);

        HANDLE handle = (HANDLE)_fileHandle.DangerousAcquire();
        try
        {
            HANDLE mappingHandle = Windows.CreateFileMappingW(handle, null, PAGE.PAGE_READONLY | SEC.SEC_IMAGE_NO_EXECUTE, 0, 0, null);
            if (mappingHandle == HANDLE.NULL)
                throw new Win32Exception(Marshal.GetLastSystemError());

            _mappingHandle = Disposable<HANDLE>.Create(mappingHandle, static handle =>
            {
                if (!Windows.CloseHandle(handle))
                    throw new Win32Exception(Marshal.GetLastSystemError());
            });

            void* view = Windows.MapViewOfFile(_mappingHandle.Value, FILE.FILE_MAP_READ, 0, 0, 0);
            if (view == null)
                throw new Win32Exception(Marshal.GetLastSystemError());

            _viewHandle = Disposable<nuint>.Create((nuint)view, static view =>
            {
                if (!Windows.UnmapViewOfFile((void*)view))
                    throw new Win32Exception(Marshal.GetLastSystemError());
            });

            MEMORY_BASIC_INFORMATION info = default;
            if (Windows.VirtualQuery(Data, &info, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) == 0)
                throw new Win32Exception(Marshal.GetLastSystemError());

            if (Data != info.AllocationBase)
                throw new InvalidOperationException();

            (Architecture, _size) = PeHelpers.GetPeImageInfo(new ReadOnlySpan<byte>(Data, checked((int)info.RegionSize)));

            if (_size > info.RegionSize)
                throw new InvalidOperationException();
        }
        finally
        {
            _fileHandle.DangerousRelease();
        }
    }

    private void Free()
    {
        Interlocked.Exchange(ref _viewHandle, null)?.Dispose();
        Interlocked.Exchange(ref _mappingHandle, null)?.Dispose();
        Interlocked.Exchange(ref _fileHandle, null)?.Dispose();

        GC.KeepAlive(this);
    }

    public void Dispose()
    {
        Free();
        GC.SuppressFinalize(this);
    }

    ~DataModule()
    {
        Free();
    }
}
