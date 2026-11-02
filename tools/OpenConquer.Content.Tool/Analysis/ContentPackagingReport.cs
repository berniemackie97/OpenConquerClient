namespace OpenConquer.Content.Tool.Analysis;

internal sealed record ContentPackagingCandidate(
    string Format, long ArchiveBytes, TimeSpan BuildTime,
    ContentReadPass[] ForwardPasses, ContentReadPass[] ReversePasses,
    ContentDecodeMetrics LooseDecode, ContentDecodeMetrics ArchiveDecode,
    long? NewPayloadCompressedBytes);

internal sealed record ContentPackagingReport(
    int FileCount, long PayloadBytes, ContentReadPass[] LooseForward,
    ContentReadPass[] LooseReverse, ContentPackagingCandidate[] Candidates,
    ContentSetDeltaReport? Delta)
{
    public IEnumerable<string> ToReportLines()
    {
        yield return $"Content: {FileCount} files, {PayloadBytes} bytes";
        foreach (string line in ReadPassLines("Loose files", LooseForward, LooseReverse))
        {
            yield return line;
        }

        if (Delta is not null)
        {
            foreach (string line in Delta.ToReportLines())
            {
                yield return line;
            }
        }

        foreach (ContentPackagingCandidate candidate in Candidates)
        {
            yield return FormattableString.Invariant(
                $"{candidate.Format}: archive {candidate.ArchiveBytes} bytes, build {candidate.BuildTime.TotalMilliseconds:F3} ms");
            yield return FormattableString.Invariant(
                $"{candidate.Format} decoded startup: loose {candidate.LooseDecode.StartupTime.TotalMilliseconds:F3} ms, archive {candidate.ArchiveDecode.StartupTime.TotalMilliseconds:F3} ms; {candidate.ArchiveDecode.StartupFiles} dependencies");
            yield return FormattableString.Invariant(
                $"{candidate.Format} decoded maps: loose {candidate.LooseDecode.WorldTime.TotalMilliseconds:F3} ms, archive {candidate.ArchiveDecode.WorldTime.TotalMilliseconds:F3} ms; {candidate.ArchiveDecode.Maps} maps, {candidate.ArchiveDecode.Frames} frames");
            if (candidate.NewPayloadCompressedBytes is long bytes)
            {
                yield return $"{candidate.Format}: compressed bytes for distinct new payloads {bytes}";
            }

            foreach (string line in ReadPassLines(candidate.Format, candidate.ForwardPasses, candidate.ReversePasses))
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<string> ReadPassLines(string name,
        ContentReadPass[] forward, ContentReadPass[] reverse)
    {
        foreach (ContentReadPass pass in forward)
        {
            yield return FormattableString.Invariant(
                $"{name} forward pass {pass.Number}: {pass.BytesRead} bytes, {pass.Elapsed.TotalMilliseconds:F3} ms");
        }

        foreach (ContentReadPass pass in reverse)
        {
            yield return FormattableString.Invariant(
                $"{name} reverse pass {pass.Number}: {pass.BytesRead} bytes, {pass.Elapsed.TotalMilliseconds:F3} ms");
        }
    }
}
