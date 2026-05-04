using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;

namespace AssemblyArchitect.Editor.Commands
{
    internal sealed class AddReferenceCommand
    {
        private readonly AsmDefRepository repo;
        private readonly AsmDefWriter writer;

        public AddReferenceCommand(AsmDefRepository repo, AsmDefWriter writer)
        {
            this.repo = repo ?? throw new ArgumentNullException(nameof(repo));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Execute(string sourceId, string targetId)
        {
            if (string.Equals(sourceId, targetId, StringComparison.Ordinal))
                return;

            var data = repo.LoadAll();
            var byId = data.ToDictionary(item => item.StableId, StringComparer.Ordinal);
            if (!byId.TryGetValue(sourceId ?? string.Empty, out var source) ||
                !byId.TryGetValue(targetId ?? string.Empty, out var target))
            {
                UnityEngine.Debug.LogWarning("[AssemblyArchitect] Cannot add reference because source or target asmdef is missing.");
                return;
            }

            if (IsReadOnly(source))
            {
                EditorUtility.DisplayDialog(
                    "Read-only assembly",
                    "Cannot modify a package not embedded in this project.",
                    "OK");
                return;
            }

            if (References(source, target))
                return;

            var model = DependencyGraphModel.Build(data);
            if (CycleDetector.WouldCreateCycle(model, sourceId, targetId) &&
                !EditorUtility.DisplayDialog(
                    "Creates a dependency cycle",
                    $"Adding {target.Name} as a reference of {source.Name} would create a cycle.\nAdd anyway?",
                    "Add anyway",
                    "Cancel"))
            {
                return;
            }

            var copy = source.Clone();
            var references = new List<string>(copy.References ?? Array.Empty<string>())
            {
                !string.IsNullOrEmpty(target.Guid) ? "GUID:" + target.Guid : target.Name,
            };
            copy.References = references.ToArray();

            Undo.IncrementCurrentGroup();
            var groupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Add Reference: {source.Name} -> {target.Name}");
            writer.Save(copy, "Add Assembly Reference");
            Undo.CollapseUndoOperations(groupId);
        }

        internal static bool IsReadOnly(AsmDefData data)
        {
            return data == null ||
                   data.Origin == AsmDefOrigin.RegistryPackage ||
                   data.Origin == AsmDefOrigin.BuiltIn;
        }

        internal static bool References(AsmDefData source, AsmDefData target)
        {
            if (source == null || target == null)
                return false;

            var guidReference = !string.IsNullOrEmpty(target.Guid) ? "GUID:" + target.Guid : null;
            foreach (var reference in source.References ?? Array.Empty<string>())
            {
                if (string.Equals(reference, target.Name, StringComparison.Ordinal) ||
                    (!string.IsNullOrEmpty(guidReference) && string.Equals(reference, guidReference, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
