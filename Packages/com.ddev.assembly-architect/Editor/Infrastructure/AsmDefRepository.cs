using System;
using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using UnityEngine;

namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>Single source of truth for all <c>.asmdef</c> assets in the project.</summary>
    internal sealed class AsmDefRepository
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        /// <summary>Shared instance used by editor windows and the asset postprocessor.</summary>
        public static readonly AsmDefRepository Default = new AsmDefRepository();

        // ── State ─────────────────────────────────────────────────────────────

        private readonly IFileSystem _fs;
        private readonly IAsmDefAssetEnumerator _enumerator;
        private List<AsmDefData> _cache;
        private bool _dirty = true;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new repository.
        /// Pass custom <paramref name="fs"/> / <paramref name="enumerator"/> in tests;
        /// production code uses <see cref="Default"/>.
        /// </summary>
        public AsmDefRepository(IFileSystem fs = null, IAsmDefAssetEnumerator enumerator = null)
        {
            _fs         = fs         ?? new DefaultFileSystem();
            _enumerator = enumerator ?? new AssetDatabaseEnumerator();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Fires whenever an <c>.asmdef</c> asset is imported, deleted, or moved.</summary>
        public event Action Changed;

        /// <summary>
        /// Returns all <c>.asmdef</c> assets found in the project.
        /// The result is cached and only rebuilt when the repository is dirty.
        /// </summary>
        public IReadOnlyList<AsmDefData> LoadAll()
        {
            if (!_dirty && _cache != null)
                return _cache;

            _cache = BuildCache();
            _dirty = false;
            return _cache;
        }

        /// <summary>Returns the <see cref="AsmDefData"/> whose <see cref="AsmDefData.Guid"/> matches, or <c>null</c>.</summary>
        public AsmDefData FindByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var d in LoadAll())
                if (string.Equals(d.Guid, guid, StringComparison.Ordinal))
                    return d;
            return null;
        }

        /// <summary>Returns the <see cref="AsmDefData"/> whose <see cref="AsmDefData.Name"/> matches, or <c>null</c>.</summary>
        public AsmDefData FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var d in LoadAll())
                if (string.Equals(d.Name, name, StringComparison.Ordinal))
                    return d;
            return null;
        }

        /// <summary>Marks the cache as stale and raises <see cref="Changed"/>. Called by <see cref="AsmDefAssetPostprocessor"/>.</summary>
        internal void NotifyChanged()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private List<AsmDefData> BuildCache()
        {
            var result  = new List<AsmDefData>();
            var entries = _enumerator.GetAll();

            foreach (var entry in entries)
            {
                if (!_fs.Exists(entry.AbsolutePath))
                    continue;

                AsmDefData data;
                try
                {
                    var json = _fs.ReadAllText(entry.AbsolutePath);
                    data = JsonUtility.FromJson<AsmDefData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AssemblyArchitect] Failed to parse {entry.AssetPath}: {ex.Message}");
                    continue;
                }

                data.AssetPath = entry.AssetPath;
                data.Guid      = entry.Guid;
                data.Origin    = ClassifyOrigin(entry.AssetPath, entry.AbsolutePath);
                result.Add(data);
            }

            return result;
        }

        private static AsmDefOrigin ClassifyOrigin(string assetPath, string absolutePath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return AsmDefOrigin.ProjectAssets;

            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                // Normalize separators for comparison
                var normalized = absolutePath.Replace('\\', '/');
                if (normalized.Contains("/Library/PackageCache/"))
                    return AsmDefOrigin.RegistryPackage;
                return AsmDefOrigin.EmbeddedPackage;
            }

            return AsmDefOrigin.Unknown;
        }

        /// <summary>Strips the <c>GUID:</c> prefix from a reference entry if present.</summary>
        public static string NormalizeReference(string entry)
        {
            if (entry != null && entry.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                return entry.Substring(5);
            return entry;
        }
    }
}
