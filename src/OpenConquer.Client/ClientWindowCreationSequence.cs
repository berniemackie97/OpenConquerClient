namespace OpenConquer.Client;

internal static class ClientWindowCreationSequence
{
    /// <summary>
    /// Presents and destroys the initialization splash before constructing the main window.
    /// </summary>
    public static TMain CreateMainAfterStartup<TMain>(IStartupSplash startupSplash, Action initialize, Func<TMain> createMain) where TMain : class
    {
        ArgumentNullException.ThrowIfNull(startupSplash);

        try
        {
            ArgumentNullException.ThrowIfNull(initialize);
            ArgumentNullException.ThrowIfNull(createMain);

            startupSplash.Show();
            initialize();
        }
        catch
        {
            try
            {
                startupSplash.Dispose();
            }
            catch
            {
                // Preserve the startup or initialization failure that caused cleanup.
            }

            throw;
        }

        startupSplash.Dispose();

        return createMain() ?? throw new InvalidOperationException("The main window factory returned null.");
    }
}
