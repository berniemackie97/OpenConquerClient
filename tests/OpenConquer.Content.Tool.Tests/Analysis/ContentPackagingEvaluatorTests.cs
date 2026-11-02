using OpenConquer.Content.Tool.Analysis;
using OpenConquer.Content.Tool.Import;

namespace OpenConquer.Content.Tool.Tests.Analysis;

public sealed class ContentPackagingEvaluatorTests
{
    [Fact]
    public void EvaluateBuildsAndVerifiesBothCandidatesAndRemovesScratchArtifacts()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree workspace = new();
        source.WriteStartupSnapshot();
        string contentSet = workspace.ChildPath("content-set");
        ContentSetImporter.Import(source.RootPath, contentSet);
        string scratch = workspace.ChildPath("scratch");
        Directory.CreateDirectory(scratch);

        ContentPackagingReport report = ContentPackagingEvaluator.Evaluate(
            contentSet, scratch, previousRootPath: contentSet);

        Assert.Equal(111, report.FileCount);
        Assert.Equal(2, report.Candidates.Length);
        Assert.Equal(new[] { "Stored ZIP", "Compressed ZIP" },
            report.Candidates.Select(candidate => candidate.Format));
        Assert.All(report.Candidates, candidate =>
        {
            Assert.True(candidate.ArchiveBytes > 0);
            Assert.Equal(0, candidate.NewPayloadCompressedBytes);
            Assert.All(candidate.ForwardPasses.Concat(candidate.ReversePasses),
                pass => Assert.Equal(report.PayloadBytes, pass.BytesRead));
        });
        Assert.Equal(111, report.Delta!.UnchangedPaths.FileCount);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
    }

    [Fact]
    public void EvaluateRejectsScratchWithinContentSet()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree workspace = new();
        source.WriteStartupSnapshot();
        string contentSet = workspace.ChildPath("content-set");
        ContentSetImporter.Import(source.RootPath, contentSet);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ContentPackagingEvaluator.Evaluate(contentSet, contentSet));

        Assert.Contains("outside", exception.Message, StringComparison.Ordinal);
    }
}
