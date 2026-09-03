namespace OpenConquer.Client;

internal static class ClientWindowCreationSequence
{
    /// <summary>
    /// Presents and destroys the initialization splash before constructing the main window.
    /// </summary>
    /// <remarks>
    /// The splash is torn down as soon as <paramref name="initialize"/> returns, with no minimum
    /// display duration. Retail hides and destroys the startup logo at <c>0x5AF4E7</c> and
    /// <c>0x5AF4F2</c> immediately after its synchronous initialization block, and that block
    /// contains no sleep, wait, timer, or tick-count call.
    /// </remarks>
    public static TMain CreateMainAfterStartup<TMain>(
        IStartupSplash startupSplash,
        Action initialize,
        Func<TMain> createMain
    )
        where TMain : class
    {
        ArgumentNullException.ThrowIfNull(startupSplash);

        try
        {
            // The splash is adopted before the remaining arguments are validated so a null
            // argument cannot strand an already-constructed startup surface.
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

        return createMain()
            ?? throw new InvalidOperationException("The main window factory returned null.");
    }
}
