namespace OpenConquer.Launcher.Settings;

internal interface IDisplaySettingsStore
{
    Task<SettingsReadResult> LoadAsync(CancellationToken cancellationToken);
    Task<SettingsIssue?> SaveAsync(DisplayPreferences preferences, string? expectedRevision, bool resetInvalid, CancellationToken cancellationToken);
}
