namespace OpenConquer.Content.Tool.Manifest;


internal readonly record struct ContentManifestEntry(string SourcePath, string PathKey, long Length, string Sha256, string Signature);
