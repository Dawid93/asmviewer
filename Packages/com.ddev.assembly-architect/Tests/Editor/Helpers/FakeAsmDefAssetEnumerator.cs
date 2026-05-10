using System.Collections.Generic;
using AssemblyArchitect.Editor.Infrastructure;

namespace AssemblyArchitect.Tests.Editor.Helpers
{
    /// <summary>In-memory <see cref="IAsmDefAssetEnumerator"/> for unit tests.</summary>
    internal sealed class FakeAsmDefAssetEnumerator : IAsmDefAssetEnumerator
    {
        private readonly List<AsmDefAssetEntry> _entries = new List<AsmDefAssetEntry>();

        public void Add(string guid, string assetPath, string absolutePath) =>
            _entries.Add(new AsmDefAssetEntry(guid, assetPath, absolutePath));

        public IReadOnlyList<AsmDefAssetEntry> GetAll() => _entries;
    }
}
