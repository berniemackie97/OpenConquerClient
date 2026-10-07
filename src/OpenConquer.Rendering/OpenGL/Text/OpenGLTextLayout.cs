using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// Represents a measured native-compatible text layout produced by one OpenGL text context.
/// </summary>
public sealed class OpenGLTextLayout
{
    private readonly OpenGLTextContext _owner;

    internal OpenGLTextLayout(OpenGLTextContext owner, NativeTextLayout nativeLayout)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(nativeLayout);

        _owner = owner;
        NativeLayout = nativeLayout;
    }

    public int WidthPixels => NativeLayout.WidthPixels;
    public int HeightPixels => NativeLayout.HeightPixels;

    internal NativeTextLayout NativeLayout
    {
        get;
    }

    internal void ValidateOwner(OpenGLTextContext owner, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        if (!ReferenceEquals(_owner, owner))
        {
            throw new ArgumentException("The text layout was produced by a different OpenGL text context.", parameterName);
        }
    }
}
