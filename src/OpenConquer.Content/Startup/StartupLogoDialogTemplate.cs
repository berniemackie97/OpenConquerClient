namespace OpenConquer.Content.Startup;

/// <summary>
/// The retail startup-logo dialog template, decoded from <c>RT_DIALOG</c> id <c>0x111</c> in
/// <c>Conquer.exe</c> (RVA <c>0x57B7C0</c>, 32 bytes).
/// </summary>
/// <remarks>
/// <para>
/// The resource is a classic <c>DLGTEMPLATE</c>: <c>style 0x80000840</c>
/// (<c>WS_POPUP | DS_CENTER | DS_SETFONT</c>), extended style <c>0</c>, <c>cdit 0</c>,
/// <c>x 0</c>, <c>y 0</c>, <c>cx 250</c>, <c>cy 188</c>, font <c>12 pt</c> SimSun. <c>DS_SHELLFONT</c>
/// is <b>not</b> set: it is <c>DS_SETFONT | DS_FIXEDSYS</c> (<c>0x48</c>) and only <c>0x40</c> is
/// present. The template carries no border, caption, or frame style, so the outer window rect
/// equals the client rect.
/// </para>
/// <para>
/// <c>cx</c> and <c>cy</c> are <b>dialog template units</b>, not pixels. Treating them as pixels
/// renders the logo at roughly half size.
/// </para>
/// </remarks>
public static class StartupLogoDialogTemplate
{
    /// <summary>Template width in dialog units (<c>cx</c>).</summary>
    public const int WidthInDialogUnits = 250;

    /// <summary>Template height in dialog units (<c>cy</c>).</summary>
    public const int HeightInDialogUnits = 188;

    /// <summary>Average character width, in pixels, of SimSun 12 pt realized at 96 DPI.</summary>
    private const int ReferenceFontAverageCharacterWidthPixels = 8;

    /// <summary><c>tmHeight</c>, in pixels, of SimSun 12 pt realized at 96 DPI.</summary>
    private const int ReferenceFontHeightPixels = 16;

    /// <summary>
    /// The client size the template resolves to under the retail-era environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived, not verified.</b> USER32 computes dialog base units from the realized
    /// <c>DS_SETFONT</c> font, so the true pixel size is a runtime font and DPI property that the
    /// binary does not fix. This value assumes 96 DPI with SimSun present
    /// (<c>tmAveCharWidth</c> 8, <c>tmHeight</c> 16) and applies the documented conversion
    /// <c>px_x = cx * baseUnitX / 4</c>, <c>px_y = cy * baseUnitY / 8</c>. On a host without SimSun
    /// or at another DPI, retail itself would produce a different size and does not compensate.
    /// </para>
    /// <para>
    /// It is used only as the surface size when no logo bitmap could be loaded. Whenever a bitmap
    /// is available, its own dimensions are authoritative.
    /// </para>
    /// </remarks>
    public static (int Width, int Height) DeriveReferenceClientSize()
    {
        return (
            WidthInDialogUnits * ReferenceFontAverageCharacterWidthPixels / 4,
            HeightInDialogUnits * ReferenceFontHeightPixels / 8
        );
    }
}
