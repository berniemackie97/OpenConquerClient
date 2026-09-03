namespace OpenConquer.Content.Tool.Manifest;

/// <summary>
/// One source-preserved payload file in a content set.
/// </summary>
/// <param name="SourcePath">
/// Slash-normalized retail relative path with its original case retained. This is the file's
/// identity and its location under <c>payload/</c>.
/// </param>
/// <param name="PathKey">
/// Case-folded lookup key. Held separately from <paramref name="SourcePath"/> so a case-insensitive
/// collision between two differently-cased retail paths is detectable.
/// </param>
/// <param name="Length">Exact byte count.</param>
/// <param name="Sha256">Lowercase hexadecimal SHA-256 of the payload bytes.</param>
/// <param name="Signature">
/// Magic-byte classification, independent of the file extension. Extensions are identity hints
/// only, so the observed signature is recorded and re-checked on verification.
/// </param>
internal readonly record struct ContentManifestEntry(
    string SourcePath,
    string PathKey,
    long Length,
    string Sha256,
    string Signature
);
