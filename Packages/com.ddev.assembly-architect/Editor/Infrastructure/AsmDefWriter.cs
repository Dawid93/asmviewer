using System;
using AssemblyArchitect.Editor.Core;
using UnityEditor;

namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>Persists <see cref="AsmDefData"/> changes back to disk with Undo support and asset reimport.</summary>
    internal sealed class AsmDefWriter
    {
        private readonly IFileSystem _fs;

        /// <summary>
        /// Creates a new writer.
        /// Pass a custom <paramref name="fs"/> in tests; leave <c>null</c> for the production <see cref="DefaultFileSystem"/>.
        /// </summary>
        public AsmDefWriter(IFileSystem fs = null)
        {
            _fs = fs ?? new DefaultFileSystem();
        }

        /// <summary>
        /// Serializes <paramref name="data"/> and writes it to disk, wrapped in an Undo record.
        /// Returns early without writing if the serialized content is identical to what is already on disk.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="AsmDefData.AssetPath"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file cannot be made editable.</exception>
        public void Save(AsmDefData data, string undoLabel = "Modify Assembly Definition")
        {
            if (string.IsNullOrEmpty(data.AssetPath))
                throw new ArgumentException("AsmDefData.AssetPath must be set before saving.", nameof(data));

            if (!_fs.Exists(data.AssetPath) &&
                !_fs.Exists(System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", data.AssetPath))))
            {
                throw new ArgumentException($"File not found: {data.AssetPath}", nameof(data));
            }

            if (!AssetDatabase.MakeEditable(data.AssetPath))
                throw new InvalidOperationException(
                    $"Cannot make '{data.AssetPath}' editable. Is the file checked out?");

            var serialized = AsmDefJsonSerializer.Serialize(data);

            // Resolve the absolute path the same way the repository does
            var absolutePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", data.AssetPath));

            if (_fs.Exists(absolutePath))
            {
                var existing = _fs.ReadAllText(absolutePath);
                if (existing == serialized)
                    return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(data.AssetPath);
            if (asset != null)
                Undo.RegisterCompleteObjectUndo(asset, undoLabel);

            _fs.WriteAllText(absolutePath, serialized);
            AssetDatabase.ImportAsset(data.AssetPath);
        }
    }
}
