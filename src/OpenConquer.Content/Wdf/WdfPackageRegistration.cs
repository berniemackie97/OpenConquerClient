namespace OpenConquer.Content.Wdf;

/// <summary>
/// The outcome of one <c>ini/package.ini</c> declaration.
/// </summary>
/// <remarks>
/// Native <c>GraphicData.dll!GraphicData_OpenPackagesFromPackageIni</c> (<c>0x1001A390</c>)
/// discards <c>TqPackagesOpen</c>'s result at <c>0x1001A406</c>, so no declaration outcome is fatal.
/// Recording them keeps the tolerated cases reviewable instead of invisible.
/// </remarks>
public enum WdfPackageRegistrationOutcome
{
    /// <summary>The package file was opened and its prefix registered.</summary>
    Registered,

    /// <summary>
    /// The declared file is absent. Native still creates the package object and registers the
    /// prefix with an empty index, because <c>sub_100014F0</c> discards
    /// <c>WdfHandler_OpenFile</c>'s failure at <c>0x10001620</c>; every lookup under the prefix
    /// misses either way.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// Another declaration already registered this prefix. Native returns early at
    /// <c>0x10003DEF</c> without replacing the first registration and without raising an error.
    /// </summary>
    DuplicatePrefix,
}

/// <summary>
/// One <c>ini/package.ini</c> declaration and how it was resolved.
/// </summary>
/// <param name="DeclaredName">The token exactly as it appeared in <c>ini/package.ini</c>.</param>
/// <param name="Prefix">The routing prefix derived from <paramref name="DeclaredName"/>.</param>
/// <param name="Outcome">How the declaration was resolved.</param>
public readonly record struct WdfPackageRegistration(string DeclaredName, string Prefix, WdfPackageRegistrationOutcome Outcome);
