namespace OpenConquer.Client;

internal static class ClientWindowCreationSequence
{
    /// <summary>
    /// Presents and destroys the initialization splash before constructing the main window.
    /// </summary>
    public static TMain CreateMainAfterStartup<TMain>(IStartupSplash startupSplash, Action initialize, Func<TMain> createMain)
        where TMain : class
    {
        ArgumentNullException.ThrowIfNull(startupSplash);
        ArgumentNullException.ThrowIfNull(initialize);
        ArgumentNullException.ThrowIfNull(createMain);

        using (startupSplash)
        {
            startupSplash.Show();
            initialize();
            startupSplash.Complete();
        }

        return createMain() ?? throw new InvalidOperationException("The main window factory returned null.");
    }
}
