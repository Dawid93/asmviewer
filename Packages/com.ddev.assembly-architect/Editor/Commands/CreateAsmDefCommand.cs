using System;
using System.IO;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Editor.Commands
{
    internal sealed class CreateAsmDefCommand
    {
        private readonly AsmDefRepository repo;
        private readonly AsmDefWriter writer;
        private readonly Action<string, Vector2> setPendingPosition;

        public CreateAsmDefCommand(
            AsmDefRepository repo,
            AsmDefWriter writer,
            Action<string, Vector2> setPendingPosition = null)
        {
            this.repo = repo ?? throw new ArgumentNullException(nameof(repo));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.setPendingPosition = setPendingPosition;
        }

        public sealed class Args
        {
            public string Folder;
            public string Name;
            public Vector2 GraphPosition;
            public string AutoReferenceFromSourceId;
        }

        public void Execute(Args args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            var folder = string.IsNullOrEmpty(args.Folder) ? "Assets" : args.Folder.TrimEnd('/');
            var assetPath = $"{folder}/{args.Name}.asmdef";
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (File.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog("File exists", $"An assembly definition already exists at:\n{assetPath}", "OK");
                return;
            }

            var data = new AsmDefData
            {
                Name = args.Name,
                AssetPath = assetPath,
                Origin = AsmDefOrigin.ProjectAssets,
            };

            Undo.IncrementCurrentGroup();
            var groupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Assembly Definition");

            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, AsmDefJsonSerializer.Serialize(data));
            AssetDatabase.ImportAsset(assetPath);

            var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(assetPath);
            if (asset != null)
                Undo.RegisterCreatedObjectUndo(asset, "Create Assembly Definition");

            data.Guid = AssetDatabase.AssetPathToGUID(assetPath);
            setPendingPosition?.Invoke(data.StableId, args.GraphPosition);

            if (!string.IsNullOrEmpty(args.AutoReferenceFromSourceId))
            {
                repo.NotifyChanged();
                new AddReferenceCommand(repo, writer).Execute(args.AutoReferenceFromSourceId, data.StableId);
            }
            else
            {
                repo.NotifyChanged();
            }

            Undo.CollapseUndoOperations(groupId);
        }
    }
}
