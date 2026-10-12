namespace OpenConquer.Client.UI.Login;

internal enum LoginDirectoryAvailability
{
    None,
    NotConfigured,
    Checking,
    Unavailable,
    NoRegions,
    ChooseRegion,
    NoServers,
    ChooseServer,
}

/// <summary>Derives presentation-only availability from authenticated selection state.</summary>
internal static class LoginDirectoryStatus
{
    public static LoginDirectoryAvailability Resolve(bool hasAuthority, bool hasVerifiedCatalog, bool acquisitionFailed, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (!hasAuthority)
        {
            return LoginDirectoryAvailability.NotConfigured;
        }

        if (!hasVerifiedCatalog)
        {
            return acquisitionFailed ? LoginDirectoryAvailability.Unavailable : LoginDirectoryAvailability.Checking;
        }

        if (selection.Catalog.Regions.Count == 0)
        {
            return LoginDirectoryAvailability.NoRegions;
        }

        if (selection.SelectedRegion is not { } region)
        {
            return LoginDirectoryAvailability.ChooseRegion;
        }

        if (region.Servers.Count == 0)
        {
            return LoginDirectoryAvailability.NoServers;
        }

        return selection.SelectedServer is null ? LoginDirectoryAvailability.ChooseServer : LoginDirectoryAvailability.None;
    }

    public static string Text(LoginDirectoryAvailability availability) => availability switch
    {
        LoginDirectoryAvailability.NotConfigured => "REALM DIRECTORY NOT CONFIGURED",
        LoginDirectoryAvailability.Checking => "CHECKING REALM DIRECTORY",
        LoginDirectoryAvailability.Unavailable => "REALM DIRECTORY UNAVAILABLE",
        LoginDirectoryAvailability.NoRegions => "NO REALMS PUBLISHED",
        LoginDirectoryAvailability.ChooseRegion => "SELECT A REGION",
        LoginDirectoryAvailability.NoServers => "NO SERVERS IN REGION",
        LoginDirectoryAvailability.ChooseServer => "SELECT A SERVER",
        _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, "This status has no visible label."),
    };

    public static LoginSpritePlacement Bounds(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ReadOnlySpan<LoginSpritePlacement> servers = layout.ServerCells;
        LoginSpritePlacement first = servers[0];
        LoginSpritePlacement last = servers[^1];
        int y = checked(last.Y + last.Height + Scale(8, layout.Width));
        LoginSpritePlacement bounds = new(first.X, y, checked(last.X + last.Width - first.X), Scale(26, layout.Width));

        if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0 ||
            (long)bounds.X + bounds.Width > layout.Width || (long)bounds.Y + bounds.Height > layout.AccountPanel.Y)
        {
            throw new InvalidOperationException("Directory status does not fit between the server grid and account panel.");
        }

        return bounds;
    }

    public static int Scale(int referenceValue, int logicalWidth) => checked((int)(((long)referenceValue * logicalWidth + 512) / 1024));
}
