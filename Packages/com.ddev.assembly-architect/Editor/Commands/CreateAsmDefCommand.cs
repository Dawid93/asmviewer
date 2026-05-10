using System;
using System.IO;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Editor.Commands
{
    /// <summary>Creates a new <c>.asmdef</c> file on disk and optionally adds a back-reference.</summary>
    internal sealed class CreateAsmDefCommand
    {
        private readonly AsmDefRepository   _repo;
        private readonly AddReferenceCommand _addRefCmd;
        private readonly IFileSystem        _fs;

        // ── Args ─────────────────────────────────────────────────────────────

        public sealed class Args
        {
            /// <summary>Project-relative folder, e.g. <c>Assets/Foo</c>.</summary>
            public string Folder;

            /// <summary>Assembly name, e.g. <c>Acme.Bar</c>. Used as file name too.</summary>
            public string Name;

            /// <summary>Graph-content-space position where the new node should appear.</summary>
            public Vector2 GraphPosition;

            /// <summary>
            /// When non-empty, the command will call <see cref="AddReferenceCommand"/> to wire
            /// <c>AutoReferenceFromSourceId → newAssembly</c> after creation.
            /// </summary>
            public string AutoReferenceFromSourceId;
        }

        // ── Constructor ───────────────────────────────────────────────────────

        public CreateAsmDefCommand(AsmDefRepository repo, AsmDefWriter writer, IFileSystem fs = null)
        {
            _repo      = repo;
            _addRefCmd = new AddReferenceCommand(repo, writer);
            _fs        = fs ?? new DefaultFileSystem();
        }

        // ── Execute ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the <c>.asmdef</c> file and optionally wires a reference.
        /// </summary>
        /// <param name="args">Creation arguments.</param>
        /// <param name="onPositionReady">
        ///   Called with <c>(stableId, graphPos)</c> once the GUID is known so the caller can
        ///   pre-seed the layout position before the graph rebuild fires.
        /// </param>
        public void Execute(Args args, Action<string, Vector2> onPositionReady = null)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            var folder    = (args.Folder ?? "Assets").TrimEnd('/', '\\');
            var assetPath = $"{folder}/{args.Name}.asmdef";

            // ── Existence guard ───────────────────────────────────────────────
            var absolutePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", assetPath));

            if (_fs.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog("File exists",
                    $"A file already exists at '{assetPath}'.", "OK");
                return;
            }

            // ── Ensure directory ──────────────────────────────────────────────
            var dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // ── Write new asmdef to disk ──────────────────────────────────────
            var data = new AsmDefData { Name = args.Name };
            var json = AsmDefJsonSerializer.Serialize(data);
            _fs.WriteAllText(absolutePath, json);

            // ── Import and resolve GUID ───────────────────────────────────────
            // ImportAsset triggers the postprocessor (which schedules a debounced rebuild).
            // We capture the GUID immediately after import so we can pre-seed _positions
            // before the rebuild fires (~100 ms later).
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            data.AssetPath = assetPath;
            data.Guid      = guid;
            data.Origin    = AsmDefOrigin.ProjectAssets;

            var newId = data.StableId; // GUID if available, else Name

            // ── Notify position ───────────────────────────────────────────────
            onPositionReady?.Invoke(newId, args.GraphPosition);

            // ── Optional auto-reference ───────────────────────────────────────
            if (!string.IsNullOrEmpty(args.AutoReferenceFromSourceId))
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Create Assembly Definition");
                _addRefCmd.Execute(args.AutoReferenceFromSourceId, newId);
            }
        }
    }
}
