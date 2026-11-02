namespace OpenConquer.Product.Tool;

internal sealed record LocalProductOptions(string WorkingDirectoryPath, uint? MapDataId = null) : ProductToolOptions;
