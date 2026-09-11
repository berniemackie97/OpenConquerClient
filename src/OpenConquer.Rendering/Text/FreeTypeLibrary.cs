using FreeTypeSharp;
using Microsoft.Win32.SafeHandles;
using static FreeTypeSharp.FT;

namespace OpenConquer.Rendering.Text;

/// <summary>
/// Owns one initialized FreeType library instance for the rendering text subsystem.
/// </summary>
internal sealed unsafe class FreeTypeLibrary : IDisposable
{
    private readonly SafeFreeTypeLibraryHandle _handle;

    public FreeTypeLibrary()
    {
        FT_LibraryRec_* library = null;

        FT_Error error = FT_Init_FreeType(&library);

        if (error != FT_Error.FT_Err_Ok || library is null)
        {
            throw new InvalidOperationException($"FreeType initialization failed with error code {(int)error}.");
        }

        _handle = new SafeFreeTypeLibraryHandle(library);
    }

    public void Dispose()
    {
        _handle.Dispose();
    }

    internal T UseHandle<T>(FreeTypeLibraryOperation<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        bool addedReference = false;

        try
        {
            _handle.DangerousAddRef(ref addedReference);

            ObjectDisposedException.ThrowIf(_handle.IsInvalid || _handle.IsClosed, this);

            return operation((FT_LibraryRec_*)_handle.DangerousGetHandle());
        }
        finally
        {
            if (addedReference)
            {
                _handle.DangerousRelease();
            }
        }
    }

    internal delegate T FreeTypeLibraryOperation<T>(FT_LibraryRec_* library);

    private sealed class SafeFreeTypeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFreeTypeLibraryHandle(FT_LibraryRec_* library) : base(ownsHandle: true)
        {
            SetHandle((nint)library);
        }

        protected override bool ReleaseHandle()
        {
            return FT_Done_FreeType((FT_LibraryRec_*)handle) == FT_Error.FT_Err_Ok;
        }
    }
}
