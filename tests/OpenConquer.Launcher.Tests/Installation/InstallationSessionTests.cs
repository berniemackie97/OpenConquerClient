using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests.Installation;

public sealed class InstallationSessionTests
{
    [Fact]
    public async Task Inspect_TransitionsThroughCheckingAndRejectsOverlap()
    {
        ControlledInspector inspector = new();
        InstallationSession session = new(inspector);
        Assert.IsType<InstallationState.Unselected>(session.State);

        Task operation = session.InspectAsync(Path.GetTempPath(), CancellationToken.None);
        InstallationState.Checking checking = Assert.IsType<InstallationState.Checking>(session.State);
        Assert.NotNull(checking.Root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.InspectAsync(Path.GetTempPath(), CancellationToken.None));
        Assert.Equal(1, inspector.CallCount);

        inspector.Completion.SetResult(new InstallationInspection.Located(new Version(1, 2, 3, 4)));
        await operation;
        InstallationState.Located located = Assert.IsType<InstallationState.Located>(session.State);
        Assert.Same(checking.Root, located.Root);
        Assert.Equal(new Version(1, 2, 3, 4), located.AssemblyVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/game")]
    public async Task Inspect_InvalidSelectionDoesNotTouchFilesystem(string? path)
    {
        ControlledInspector inspector = new();
        InstallationSession session = new(inspector);
        await session.InspectAsync(path, CancellationToken.None);
        Assert.IsType<InstallationState.InvalidPath>(session.State);
        Assert.Equal(0, inspector.CallCount);
    }

    [Fact]
    public async Task Inspect_CancellationWinsOverALateSuccessfulResultAndAllowsRetry()
    {
        ControlledInspector inspector = new();
        InstallationSession session = new(inspector);
        using CancellationTokenSource cancellation = new();
        Task operation = session.InspectAsync(Path.GetTempPath(), cancellation.Token);
        cancellation.Cancel();
        inspector.Completion.SetResult(new InstallationInspection.Located(new Version(1, 0)));
        await operation;
        Assert.IsType<InstallationState.Cancelled>(session.State);

        await session.InspectAsync(Path.GetTempPath(), CancellationToken.None);
        Assert.IsType<InstallationState.Located>(session.State);
        Assert.Equal(2, inspector.CallCount);
    }

    [Fact]
    public async Task Inspect_ExpectedFailureIsRecoverableState()
    {
        ControlledInspector inspector = new();
        inspector.Completion.SetResult(new InstallationInspection.Rejected(InstallationIssue.AccessDenied));
        InstallationSession session = new(inspector);
        await session.InspectAsync(Path.GetTempPath(), CancellationToken.None);
        Assert.Equal(InstallationIssue.AccessDenied, Assert.IsType<InstallationState.Rejected>(session.State).Issue);
    }

    [Fact]
    public async Task Inspect_UnexpectedFailureEscapesWithOriginalIdentity()
    {
        ControlledInspector inspector = new();
        InvalidOperationException failure = new("sensitive-diagnostic-test-value");
        inspector.Completion.SetException(failure);
        InstallationSession session = new(inspector);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => session.InspectAsync(Path.GetTempPath(), CancellationToken.None)));
        Assert.IsType<InstallationState.Faulted>(session.State);
        Assert.DoesNotContain("sensitive-diagnostic-test-value", InstallationStatusText.For(session.State).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_UnrelatedCancellationIsAnUnexpectedFailure()
    {
        ControlledInspector inspector = new();
        inspector.Completion.SetCanceled(TestContext.Current.CancellationToken);
        InstallationSession session = new(inspector);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.InspectAsync(Path.GetTempPath(), CancellationToken.None));
        Assert.IsType<InstallationState.Faulted>(session.State);
    }

    [Fact]
    public async Task ClearSelection_InvalidatesLocatedStateButCannotInterruptAnActiveRead()
    {
        ControlledInspector inspector = new();
        InstallationSession session = new(inspector);
        Task operation = session.InspectAsync(Path.GetTempPath(), CancellationToken.None);
        Assert.Throws<InvalidOperationException>(session.ClearSelection);
        inspector.Completion.SetResult(new InstallationInspection.Located(new Version(1, 0)));
        await operation;
        session.ClearSelection();
        Assert.IsType<InstallationState.Unselected>(session.State);
        Assert.Null(session.State.Root);
    }

    [Fact]
    public void Root_NormalizesWithoutDependingOnWorkingDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "game", "..", "game") + Path.DirectorySeparatorChar;
        Assert.True(InstallationRoot.TryCreate(path, out InstallationRoot? root));
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), root.Path);
        Assert.False(InstallationRoot.TryCreate(Path.Combine(Path.GetTempPath(), "invalid\0path"), out _));
    }

    private sealed class ControlledInspector : IInstallationInspector
    {
        public TaskCompletionSource<InstallationInspection> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount
        {
            get; private set;
        }

        public Task<InstallationInspection> InspectAsync(InstallationRoot root, CancellationToken cancellationToken)
        {
            CallCount++;
            return Completion.Task;
        }
    }
}
