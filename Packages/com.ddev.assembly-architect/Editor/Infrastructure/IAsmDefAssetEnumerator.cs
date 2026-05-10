using System.Collections.Generic;

namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>Entry returned by <see cref="IAsmDefAssetEnumerator"/>.</summary>
    internal readonly struct AsmDefAssetEntry
    {
        /// <summary>AssetDatabase GUID for this asset.</summary>
        public readonly string Guid;

        /// <summary>Project-relative asset path, e.g. <c>Assets/Foo/Foo.asmdef</c>.</summary>
        public readonly string AssetPath;

        /// <summary>Absolute filesystem path.</summary>
        public readonly string AbsolutePath;

        public AsmDefAssetEntry(string guid, string assetPath, string absolutePath)
        {
            Guid         = guid;
            AssetPath    = assetPath;
            AbsolutePath = absolutePath;
        }
    }

    /// <summary>Seam over <c>AssetDatabase.FindAssets</c> so unit tests can supply a fake list.</summary>
    internal interface IAsmDefAssetEnumerator
    {
        /// <summary>Returns all <c>.asmdef</c> assets found in the project.</summary>
        IReadOnlyList<AsmDefAssetEntry> GetAll();
    }
}
