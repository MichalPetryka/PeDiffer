using System;
using System.Runtime.InteropServices;

namespace PeDiffer;

internal static unsafe class HandleExtensions
{
    extension(SafeHandle handle)
    {
        public void* DangerousAcquire()
        {
            if (handle.IsInvalid || handle.IsClosed)
                throw new InvalidOperationException();

            bool success = false;
            handle.DangerousAddRef(ref success);
            return success ? (void*)handle.DangerousGetHandle() : throw new InvalidOperationException();
        }
    }
}
