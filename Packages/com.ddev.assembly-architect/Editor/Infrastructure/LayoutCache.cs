using System;
using System.IO;
using System.Linq;
using AssemblyArchitect.Editor.Settings;
using UnityEngine;

namespace AssemblyArchitect.Editor.Infrastructure
{
    internal sealed class LayoutCache
    {
        private const string RelativePath = "Packages/com.ddev.assembly-architect/Layout.json";
        private readonly IFileSystem fs;

        public LayoutCache(IFileSystem fs = null)
        {
            this.fs = fs ?? new DefaultFileSystem();
        }

        public LayoutCacheData Load()
        {
            var path = GetPath();
            if (!fs.Exists(path))
                return new LayoutCacheData();

            try
            {
                return JsonUtility.FromJson<LayoutCacheData>(fs.ReadAllText(path)) ?? new LayoutCacheData();
            }
            catch
            {
                return new LayoutCacheData();
            }
        }

        public void Save(LayoutCacheData data)
        {
            if (data == null)
                data = new LayoutCacheData();

            data.version = 1;
            data.positions = (data.positions ?? Array.Empty<PositionEntry>())
                .OrderBy(entry => entry.id, StringComparer.Ordinal)
                .ToArray();

            var path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            fs.WriteAllText(path, JsonUtility.ToJson(data, true) + "\n");
        }

        public void Clear()
        {
            var projectPath = GetPath(true);
            var userPath = GetPath(false);
            if (File.Exists(projectPath))
                File.Delete(projectPath);
            if (File.Exists(userPath))
                File.Delete(userPath);
        }

        public void MoveFrom(bool wasTeamShared)
        {
            var oldPath = GetPath(wasTeamShared);
            var newPath = GetPath(AssemblyArchitectSettings.instance.LayoutCacheIsTeamShared);
            if (!File.Exists(oldPath) || string.Equals(oldPath, newPath, StringComparison.Ordinal))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(newPath));
            if (File.Exists(newPath))
                File.Delete(newPath);
            File.Move(oldPath, newPath);
        }

        private static string GetPath()
        {
            return GetPath(AssemblyArchitectSettings.instance.LayoutCacheIsTeamShared);
        }

        private static string GetPath(bool teamShared)
        {
            var root = teamShared ? "ProjectSettings" : "UserSettings";
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", root, RelativePath));
        }
    }

    [Serializable]
    internal sealed class LayoutCacheData
    {
        public int version = 1;
        public Vector2 viewportPosition;
        public float viewportScale = 1f;
        public PositionEntry[] positions = Array.Empty<PositionEntry>();
    }

    [Serializable]
    internal struct PositionEntry
    {
        public string id;
        public float x;
        public float y;
    }
}
