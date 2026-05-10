using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>Production implementation of <see cref="IAsmDefAssetEnumerator"/> backed by <c>AssetDatabase</c>.</summary>
    internal sealed class AssetDatabaseEnumerator : IAsmDefAssetEnumerator
    {
        public IReadOnlyList<AsmDefAssetEntry> GetAll()
        {
            var guids  = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            var result = new List<AsmDefAssetEntry>(guids.Length);

            foreach (var guid in guids)
            {
                var assetPath    = AssetDatabase.GUIDToAssetPath(guid);
                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                result.Add(new AsmDefAssetEntry(guid, assetPath, absolutePath));
            }

            return result;
        }
    }
}
