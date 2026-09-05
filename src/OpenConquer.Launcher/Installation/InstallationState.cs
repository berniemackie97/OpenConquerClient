namespace OpenConquer.Launcher.Installation;

/// <summary>The current inspection state. No state in this slice authorizes game launch.</summary>
internal abstract record InstallationState
{
    private InstallationState()
    {
    }

    private InstallationState(InstallationRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Root = root;
    }

    public InstallationRoot? Root
    {
        get;
    }

    internal sealed record Unselected : InstallationState;
    internal sealed record InvalidPath : InstallationState;

    internal sealed record Checking : InstallationState
    {
        public Checking(InstallationRoot root) : base(root)
        {
        }
    }

    internal sealed record Located : InstallationState
    {
        public Located(InstallationRoot root, Version assemblyVersion) : base(root)
        {
            ArgumentNullException.ThrowIfNull(assemblyVersion);
            AssemblyVersion = assemblyVersion;
        }

        public Version AssemblyVersion
        {
            get;
        }
    }

    internal sealed record Rejected : InstallationState
    {
        public Rejected(InstallationRoot root, InstallationIssue issue) : base(root)
        {
            Issue = issue;
        }

        public InstallationIssue Issue
        {
            get;
        }
    }

    internal sealed record Cancelled : InstallationState
    {
        public Cancelled(InstallationRoot root) : base(root)
        {
        }
    }

    internal sealed record Faulted : InstallationState
    {
        public Faulted(InstallationRoot root) : base(root)
        {
        }
    }
}
