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

        Assert.Equal(113, report.FileCount);
        Assert.Equal(2, report.Candidates.Length);
        Assert.Collection(report.Candidates,
            candidate => Assert.Equal("Stored ZIP", candidate.Format),
            candidate => Assert.Equal("Compressed ZIP", candidate.Format));
        Assert.All(report.Candidates, candidate =>
        {
            Assert.True(candidate.ArchiveBytes > 0);
            Assert.Equal(0, candidate.NewPayloadCompressedBytes);
            Assert.All(candidate.ForwardPasses.Concat(candidate.ReversePasses),
                pass => Assert.Equal(report.PayloadBytes, pass.BytesRead));
        });
        Assert.Equal(113, report.Delta!.UnchangedPaths.FileCount);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
    }

    [Fact]
    public void EvaluateMeasuresStartupAndMapDecodingForBothArchiveLayouts()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree workspace = new();
        source.WriteStartupSnapshot();
        ContentWorkloadFixture.WriteNewbieMap(source);

        string contentSet = workspace.ChildPath("world");
        ContentSetImporter.Import(source.RootPath, contentSet, mapDataId: 1010);
        string scratch = workspace.ChildPath("scratch");
        Directory.CreateDirectory(scratch);

        ContentPackagingReport report = ContentPackagingEvaluator.Evaluate(
            contentSet, scratch, mapDataId: 1010);

        Assert.All(report.Candidates, candidate =>
        {
            Assert.Equal(1, candidate.LooseDecode.Maps);
            Assert.Equal(1, candidate.ArchiveDecode.Maps);
            Assert.Equal(candidate.LooseDecode.Frames, candidate.ArchiveDecode.Frames);
            Assert.True(candidate.ArchiveDecode.Frames > 0);
            Assert.Equal(candidate.LooseDecode.StartupFiles, candidate.ArchiveDecode.StartupFiles);
            Assert.True(candidate.ArchiveDecode.StartupTime >= TimeSpan.Zero);
            Assert.True(candidate.ArchiveDecode.WorldTime >= TimeSpan.Zero);
        });
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
    }

    [Fact]
    public void EvaluateRejectsScratchThroughLinkedAncestor()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree workspace = new();
        source.WriteStartupSnapshot();
        string contentSet = workspace.ChildPath("content-set");
        ContentSetImporter.Import(source.RootPath, contentSet);
        string alias = workspace.ChildPath("alias");
        Directory.CreateSymbolicLink(alias, contentSet);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ContentPackagingEvaluator.Evaluate(contentSet, Path.Combine(alias, "payload")));

        Assert.Contains("outside", exception.Message, StringComparison.Ordinal);
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
