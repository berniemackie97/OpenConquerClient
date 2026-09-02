using Silk.NET.Core.Contexts;

namespace OpenConquer.Platform.Internal;

internal sealed class SilkOpenGLContext : IOpenGLContext
{
    private readonly IGLContext _context;

    public SilkOpenGLContext(IGLContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    internal bool IsCurrent => _context.IsCurrent;

    public nint GetProcAddress(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        return _context.GetProcAddress(functionName);
    }

    internal void MakeCurrent()
    {
        _context.MakeCurrent();
    }
}
