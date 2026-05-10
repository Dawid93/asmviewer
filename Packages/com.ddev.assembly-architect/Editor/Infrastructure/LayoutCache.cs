using System;
using System.IO;
using UnityEngine;

namespace AssemblyArchitect.Editor.Infrastructure
{
    [Serializable]
    internal sealed class LayoutCacheData
    {
        public int            Version          = 1;
        public Vector2        ViewportPosition = Vector2.zero;
        public float          ViewportScale    = 1f;
        public PositionEntry[] Positions       = Array.Empty<PositionEntry>();
    }

    [Serializable]
    internal struct PositionEntry
    {
        public string Id;
        public float  X;
        public float  Y;
    }

    /// <summary>Saves and restores per-project node positions and viewport state to disk.</summary>
    internal sealed class LayoutCache
    {
        private const string TeamPath =
            "ProjectSettings/Packages/com.ddev.assembly-architect/Layout.json";

        private readonly IFileSystem _fs;

        public LayoutCache(IFileSystem fs = null)
        {
            _fs = fs ?? new DefaultFileSystem();
        }

        public LayoutCacheData Load()
        {
            if (!_fs.Exists(TeamPath))
                return new LayoutCacheData();

            try
            {
                var json = _fs.ReadAllText(TeamPath);
                var data = new LayoutCacheData();
                JsonUtility.FromJsonOverwrite(json, data);
                return data ?? new LayoutCacheData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssemblyArchitect] Failed to load layout cache: {e.Message}");
                return new LayoutCacheData();
            }
        }

        public void Save(LayoutCacheData data)
        {
            if (data == null) return;

            // Sort by id for deterministic diffs
            if (data.Positions != null && data.Positions.Length > 1)
                Array.Sort(data.Positions, (a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));

            try
            {
                EnsureDirectory(TeamPath);
                var json = JsonUtility.ToJson(data, prettyPrint: true);
                _fs.WriteAllText(TeamPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssemblyArchitect] Failed to save layout cache: {e.Message}");
            }
        }

        public void Delete()
        {
            if (File.Exists(TeamPath))
                File.Delete(TeamPath);
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
