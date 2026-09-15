namespace OpenConquer.Rendering.Text;

/// <summary>
/// Represents an expected failure while opening or configuring a resolved font face.
/// </summary>
internal sealed class FontFaceCreationException(string message) : InvalidOperationException(message);
