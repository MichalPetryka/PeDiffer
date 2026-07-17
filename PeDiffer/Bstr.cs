using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PeDiffer;

internal unsafe ref struct Bstr : IDisposable
{
    private char* _ptr;

    public readonly char** GetAddressOf()
    {
        return (char**)Unsafe.AsPointer(ref Unsafe.AsRef(in this));
    }

    public override readonly string ToString()
    {
        return _ptr == null ? string.Empty : Marshal.PtrToStringBSTR((nint)_ptr);
    }

    public void Dispose()
    {
        Marshal.FreeBSTR((nint)_ptr);
        _ptr = null;
    }
}