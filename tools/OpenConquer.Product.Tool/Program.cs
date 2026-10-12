using OpenConquer.Product.Tool;

return args.Length > 0 && RealmCatalogTool.IsCommand(args[0]) ? RealmCatalogTool.Run(args) : ProductToolHost.Run(args);
