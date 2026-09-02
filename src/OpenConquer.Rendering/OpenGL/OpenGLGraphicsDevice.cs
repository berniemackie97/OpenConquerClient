using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

public sealed class OpenGLGraphicsDevice : IDisposable
{
    private const int MinimumMajorVersion = 3;
    private const int MinimumMinorVersion = 3;

    private readonly GL _gl;
    private bool _disposed;

    public OpenGLGraphicsDevice(OpenGLProcAddressResolver resolveProcAddress)
    {
        ArgumentNullException.ThrowIfNull(resolveProcAddress);

        GL gl = GL.GetApi(functionName => resolveProcAddress(functionName));

        try
        {
            ValidateContext(gl);

            Version = GetRequiredString(gl, StringName.Version);
            ShadingLanguageVersion = GetRequiredString(gl, StringName.ShadingLanguageVersion);
            Vendor = GetRequiredString(gl, StringName.Vendor);
            Renderer = GetRequiredString(gl, StringName.Renderer);

            _gl = gl;
        }
        catch
        {
            gl.Dispose();
            throw;
        }
    }

    public string Version
    {
        get;
    }

    public string ShadingLanguageVersion
    {
        get;
    }

    public string Vendor
    {
        get;
    }

    public string Renderer
    {
        get;
    }

    public OpenGLRenderer CreateRenderer(LogicalRenderSize logicalRenderSize, int framebufferWidth, int framebufferHeight, PresentationPolicy presentationPolicy = PresentationPolicy.Fit)
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        if (logicalRenderSize.Width <= 0 || logicalRenderSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "Logical render size must have positive width and height.");
        }

        if (!Enum.IsDefined(presentationPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(presentationPolicy), presentationPolicy, "Unknown presentation policy.");
        }

        return new OpenGLRenderer(_gl, logicalRenderSize, framebufferWidth, framebufferHeight, presentationPolicy);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _gl.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private static void ValidateContext(GL gl)
    {
        gl.GetInteger(pname: GetPName.MajorVersion, out int majorVersion);
        gl.GetInteger(pname: GetPName.MinorVersion, out int minorVersion);

        if (majorVersion < MinimumMajorVersion || majorVersion == MinimumMajorVersion && minorVersion < MinimumMinorVersion)
        {
            throw new NotSupportedException($"OpenGL {MinimumMajorVersion}.{MinimumMinorVersion} or later is required. The current context provides OpenGL {majorVersion}.{minorVersion}.");
        }

        gl.GetInteger(pname: GetPName.ContextProfileMask, out int profileMask);

        if ((profileMask & (int)GLEnum.ContextCoreProfileBit) == 0)
        {
            throw new NotSupportedException($"An OpenGL {MinimumMajorVersion}.{MinimumMinorVersion} Core profile is required.");
        }
    }

    private static string GetRequiredString(GL gl, StringName name)
    {
        return gl.GetStringS(name) ?? throw new InvalidOperationException($"OpenGL did not provide a value for {name}.");
    }
}
