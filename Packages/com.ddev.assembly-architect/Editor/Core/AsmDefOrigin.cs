namespace AssemblyArchitect.Editor.Core
{
    /// <summary>Origin of an assembly definition relative to the Unity project layout.</summary>
    public enum AsmDefOrigin
    {
        /// <summary>Path starts with <c>Assets/</c>.</summary>
        ProjectAssets,

        /// <summary>Path starts with <c>Packages/&lt;id&gt;/</c> and the folder exists on disk inside the project.</summary>
        EmbeddedPackage,

        /// <summary>Path starts with <c>Packages/&lt;id&gt;/</c> and the folder lives under <c>Library/PackageCache</c>.</summary>
        RegistryPackage,

        /// <summary>Built-in Unity module — no <c>.asmdef</c> file on disk.</summary>
        BuiltIn,

        /// <summary>Origin could not be determined.</summary>
        Unknown,
    }
}
