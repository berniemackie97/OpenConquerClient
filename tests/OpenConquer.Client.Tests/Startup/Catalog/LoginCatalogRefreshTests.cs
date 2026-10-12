using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.Startup.Catalog;

public sealed class LoginCatalogRefreshTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstAcquisitionStartsAsynchronouslyAndAcceptsNewerSignedSnapshot()
    {
        SignedLoginCatalog first = Catalog(7, "2026-10-11T20:20:00Z");
        SignedLoginCatalog next = Catalog(8, "2026-10-11T20:30:00Z");
        Queue<Task<SignedLoginCatalog?>> responses = new([Task.FromResult<SignedLoginCatalog?>(first), Task.FromResult<SignedLoginCatalog?>(next)]);
        int requests = 0;
        using LoginCatalogRefresh refresh = new(_ => { requests++; return responses.Dequeue(); });

        Assert.False(refresh.Advance(s_now));
        Assert.Equal(1, requests);
        Assert.Null(refresh.Current);
        Assert.True(refresh.Advance(s_now));
        Assert.Same(first, refresh.Current);
        Assert.False(refresh.LastAttemptFailed);
        Assert.False(refresh.Advance(s_now.AddMinutes(4)));
        Assert.Equal(1, requests);
        Assert.False(refresh.Advance(s_now.AddMinutes(5)));
        Assert.Equal(2, requests);
        Assert.True(refresh.Advance(s_now.AddMinutes(5)));
        Assert.Same(next, refresh.Current);
    }

    [Fact]
    public void FailedAcquisitionRetriesWithoutClearingUnexpiredSnapshot()
    {
        SignedLoginCatalog first = Catalog(7, "2026-10-11T20:20:00Z");
        Queue<Task<SignedLoginCatalog?>> responses = new([
            Task.FromResult<SignedLoginCatalog?>(first),
            Task.FromResult<SignedLoginCatalog?>(null),
            Task.FromResult<SignedLoginCatalog?>(null),
        ]);
        int requests = 0;
        using LoginCatalogRefresh refresh = new(_ => { requests++; return responses.Dequeue(); });

        Assert.False(refresh.Advance(s_now));
        Assert.True(refresh.Advance(s_now));
        Assert.False(refresh.Advance(s_now.AddMinutes(5)));
        Assert.False(refresh.Advance(s_now.AddMinutes(5)));
        Assert.Same(first, refresh.Current);
        Assert.True(refresh.LastAttemptFailed);
        Assert.False(refresh.Advance(s_now.AddMinutes(5).AddSeconds(29)));
        Assert.Equal(2, requests);
        Assert.False(refresh.Advance(s_now.AddMinutes(5).AddSeconds(30)));
        Assert.Equal(3, requests);
        Assert.Same(first, refresh.Current);
    }

    [Fact]
    public void SameOrOlderSequenceCannotReplaceAnAcceptedCatalog()
    {
        SignedLoginCatalog first = Catalog(8, "2026-10-11T20:30:00Z");
        SignedLoginCatalog alteredSameSequence = Catalog(8, "2026-10-11T20:40:00Z");
        SignedLoginCatalog older = Catalog(7, "2026-10-11T20:40:00Z");
        Queue<Task<SignedLoginCatalog?>> responses = new([
            Task.FromResult<SignedLoginCatalog?>(first),
            Task.FromResult<SignedLoginCatalog?>(alteredSameSequence),
            Task.FromResult<SignedLoginCatalog?>(older),
        ]);
        using LoginCatalogRefresh refresh = new(_ => responses.Dequeue());

        refresh.Advance(s_now);
        Assert.True(refresh.Advance(s_now));
        refresh.Advance(s_now.AddMinutes(5));
        Assert.False(refresh.Advance(s_now.AddMinutes(5)));
        Assert.Same(first, refresh.Current);
        refresh.Advance(s_now.AddMinutes(10));
        Assert.False(refresh.Advance(s_now.AddMinutes(10)));
        Assert.Same(first, refresh.Current);
    }

    [Fact]
    public void ExpiryRevokesSelectionEvenWhileRefreshIsOutstanding()
    {
        SignedLoginCatalog first = Catalog(7, "2026-10-11T20:01:00Z");
        SignedLoginCatalog replacement = Catalog(8, "2026-10-11T20:30:00Z");
        TaskCompletionSource<SignedLoginCatalog?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue<Task<SignedLoginCatalog?>> responses = new([Task.FromResult<SignedLoginCatalog?>(first), pending.Task]);
        using LoginCatalogRefresh refresh = new(_ => responses.Dequeue());
        LoginServerSelection selection = new();
        LoginSelectionInput input = new(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)), selection);

        refresh.Advance(s_now);
        Assert.True(refresh.Advance(s_now));
        input.ReplaceCatalog(refresh.Current!.View);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        Assert.NotNull(selection.SelectedServer);

        Assert.False(refresh.Advance(s_now.AddSeconds(10)));
        Assert.True(refresh.Advance(s_now.AddMinutes(1)));
        input.ReplaceCatalog(refresh.Current?.View ?? LoginServerCatalog.Empty);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
        Assert.Empty(selection.Catalog.Regions);

        pending.SetResult(replacement);
        Assert.True(refresh.Advance(s_now.AddMinutes(1).AddSeconds(1)));
        input.ReplaceCatalog(refresh.Current!.View);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
        Assert.Single(selection.Catalog.Regions);
    }

    [Fact]
    public void NewSignedSequenceClearsPreviouslySelectedRealm()
    {
        SignedLoginCatalog first = Catalog(7, "2026-10-11T20:30:00Z");
        SignedLoginCatalog replacement = Catalog(8, "2026-10-11T20:40:00Z");
        Queue<Task<SignedLoginCatalog?>> responses = new([
            Task.FromResult<SignedLoginCatalog?>(first),
            Task.FromResult<SignedLoginCatalog?>(replacement),
        ]);
        using LoginCatalogRefresh refresh = new(_ => responses.Dequeue());
        LoginServerSelection selection = new();
        LoginSelectionInput input = new(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)), selection);

        refresh.Advance(s_now);
        Assert.True(refresh.Advance(s_now));
        input.ReplaceCatalog(refresh.Current!.View);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));

        refresh.Advance(s_now.AddMinutes(5));
        Assert.True(refresh.Advance(s_now.AddMinutes(5)));
        input.ReplaceCatalog(refresh.Current!.View);

        Assert.Same(replacement.View, selection.Catalog);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
        Assert.Equal(-1, input.FocusedRegionSlot);
        Assert.Equal(-1, input.FocusedServerSlot);
    }

    [Fact]
    public void PendingAcquisitionNeverOverlapsAnotherRequest()
    {
        TaskCompletionSource<SignedLoginCatalog?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0;
        using LoginCatalogRefresh refresh = new(_ => { requests++; return pending.Task; });

        Assert.False(refresh.Advance(s_now));
        Assert.False(refresh.Advance(s_now.AddMinutes(5)));
        Assert.False(refresh.Advance(s_now.AddMinutes(30)));
        Assert.Equal(1, requests);
        pending.SetResult(null);
        Assert.False(refresh.Advance(s_now.AddMinutes(30)));
        Assert.Equal(1, requests);
        Assert.False(refresh.Advance(s_now.AddMinutes(30).AddSeconds(29)));
        Assert.Equal(1, requests);
    }

    [Fact]
    public void BackwardWallClockRevokesAndPermanentlyClosesTheCurrentProcess()
    {
        SignedLoginCatalog first = Catalog(7, "2026-10-11T20:20:00Z");
        int requests = 0;
        using LoginCatalogRefresh refresh = new(_ => { requests++; return Task.FromResult<SignedLoginCatalog?>(first); });

        refresh.Advance(s_now);
        Assert.True(refresh.Advance(s_now));
        Assert.True(refresh.Advance(s_now.AddTicks(-1)));
        Assert.Null(refresh.Current);
        Assert.False(refresh.Advance(s_now.AddMinutes(10)));
        Assert.Null(refresh.Current);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task StopAsyncCancelsOutstandingAcquisitionAndObservesCompletion()
    {
        CancellationToken captured = default;
        LoginCatalogRefresh refresh = new(token =>
        {
            captured = token;
            return WaitForCancellationAsync(token);
        });

        Assert.False(refresh.Advance(s_now));
        await refresh.StopAsync();

        Assert.True(captured.IsCancellationRequested);
        Assert.Null(refresh.Current);
        Assert.Throws<ObjectDisposedException>(() => refresh.Advance(s_now));
        await refresh.StopAsync();
    }

    [Fact]
    public void SynchronousFetcherFailureIsRetriedOnBoundedSchedule()
    {
        int calls = 0;
        using LoginCatalogRefresh refresh = new(_ =>
        {
            calls++;
            throw new IOException("Simulated transport failure");
        });

        Assert.False(refresh.Advance(s_now));
        Assert.False(refresh.Advance(s_now.AddSeconds(29)));
        Assert.Equal(1, calls);
        Assert.False(refresh.Advance(s_now.AddSeconds(30)));
        Assert.Equal(2, calls);
        Assert.True(refresh.LastAttemptFailed);
    }

    private static async Task<SignedLoginCatalog?> WaitForCancellationAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
        return null;
    }

    private static SignedLoginCatalog Catalog(int sequence, string expiresAt)
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string json = Encoding.UTF8.GetString(SignedLoginCatalogTests.ValidPayload());
        json = json.Replace("\"sequence\":7", $"\"sequence\":{sequence}", StringComparison.Ordinal);
        json = json.Replace("2026-10-11T21:00:00Z", expiresAt, StringComparison.Ordinal);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        return SignedLoginCatalog.Read(payload, SignedLoginCatalogTests.Sign(signer, payload), SignedLoginCatalogTests.Authority(signer), s_now);
    }
}
