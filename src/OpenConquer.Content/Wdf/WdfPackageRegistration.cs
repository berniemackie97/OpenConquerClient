namespace OpenConquer.Content.Wdf;

/// <summary>
/// The outcome of one <c>ini/package.ini</c> declaration.
/// </summary>
/// <remarks>
/// Native <c>GraphicData.dll!GraphicData_OpenPackagesFromPackageIni</c> (<c>0x1001A390</c>)
/// discards <c>TqPackagesOpen</c>'s result at <c>0x1001A406</c>. Expected package-availability
/// failures are therefore represented as observable registration outcomes instead of becoming
/// client-startup failures.
/// </remarks>
public enum WdfPackageRegistrationOutcome
{
    /// <summary>The package file was opened, indexed, and registered.</summary>
    Registered = 0,

    /// <summary>
    /// The declared file is absent. Native still creates the package object and registers its
    /// prefix hash with an empty index because <c>sub_100014F0</c> discards
    /// <c>WdfHandler_OpenFile</c>'s failure at <c>0x10001620</c>.
    /// </summary>
    FileNotFound = 1,

    /// <summary>
    /// Another declaration already owns the same native prefix hash. Native compares the
    /// 32-bit routing hash rather than the source prefix string and returns at
    /// <c>0x10003DEF</c> without replacing the first registration.
    /// </summary>
    DuplicatePrefix = 2,

    /// <summary>
    /// The declared file exists but its archive could not be resolved, opened, or structurally
    /// validated. Native retains the already-registered package object with an empty index, so
    /// lookups under the routing hash miss while client initialization continues.
    /// </summary>
    ArchiveUnavailable = 3,
}

/// <summary>
/// One <c>ini/package.ini</c> declaration and how it was resolved.
/// </summary>
/// <param name="DeclaredName">The token exactly as it appeared in <c>ini/package.ini</c>.</param>
/// <param name="Prefix">
/// The normalized human-readable prefix derived from <paramref name="DeclaredName"/>. Native
/// registration and routing use this prefix's 32-bit WDF hash as the actual package identity.
/// </param>
/// <param name="Outcome">How the declaration was resolved.</param>
public readonly record struct WdfPackageRegistration(string DeclaredName, string Prefix, WdfPackageRegistrationOutcome Outcome);
