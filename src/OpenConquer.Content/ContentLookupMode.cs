namespace OpenConquer.Content;

/// <summary>
/// Selects which storage a content lookup is allowed to satisfy.
/// </summary>
/// <remarks>
/// <para>
/// The values match the native <c>FILETYPE</c> argument threaded through
/// <c>TqPackageWdf.dll!sub_10001640</c> (<c>0x10001640</c>), which the retail client dispatches on
/// directly: mode <c>0</c> probes the loose file and falls back to the package (<c>0x100016C8</c>),
/// mode <c>1</c> is loose only (<c>0x10001685</c>), and mode <c>2</c> is package only
/// (<c>0x10001677</c>).
/// </para>
/// <para>
/// Precedence in retail is a property of the entry point, not a global rule.
/// <c>TqFDump</c> (<c>0x10004220</c>) always requests <see cref="LooseThenPackage"/>, while
/// <c>TqFOpen</c> (<c>0x100042B0</c>) hard-codes <see cref="PackageOnly"/> at <c>0x10001747</c>.
/// Callers therefore state the mode they need rather than inheriting one.
/// </para>
/// </remarks>
public enum ContentLookupMode
{
    /// <summary>
    /// Resolve a loose file first and fall back to a registered package. Native <c>FILETYPE</c> 0.
    /// </summary>
    LooseThenPackage = 0,

    /// <summary>
    /// Resolve only a loose file on the host filesystem. Native <c>FILETYPE</c> 1.
    /// </summary>
    LooseOnly = 1,

    /// <summary>
    /// Resolve only an entry inside a registered package. Native <c>FILETYPE</c> 2.
    /// </summary>
    PackageOnly = 2,
}
