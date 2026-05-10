using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Editor.Commands
{
    /// <summary>Removes an assembly reference from source, with read-only guard and optional confirmation dialog.</summary>
    internal sealed class RemoveReferenceCommand
    {
        private readonly AsmDefRepository _repo;
        private readonly AsmDefWriter     _writer;

        public RemoveReferenceCommand(AsmDefRepository repo, AsmDefWriter writer)
        {
            _repo   = repo;
            _writer = writer;
        }

        /// <param name="skipConfirmation">When <c>true</c> (Shift held), the confirmation dialog is bypassed.</param>
        public void Execute(string sourceId, string targetId, bool skipConfirmation = false)
        {
            var all    = _repo.LoadAll();
            AsmDefData source = null, target = null;
            foreach (var d in all)
            {
                if (d.StableId == sourceId) source = d;
                if (d.StableId == targetId) target = d;
            }

            if (source == null || target == null)
            {
                Debug.LogWarning($"[AssemblyArchitect] RemoveReference: could not resolve '{sourceId}' or '{targetId}'.");
                return;
            }

            // Read-only guard
            if (source.Origin == AsmDefOrigin.RegistryPackage || source.Origin == AsmDefOrigin.BuiltIn)
            {
                EditorUtility.DisplayDialog("Read-only assembly",
                    "Cannot modify a package not embedded in this project.", "OK");
                return;
            }

            // Find reference entry
            var guidForm  = string.IsNullOrEmpty(target.Guid) ? null : "GUID:" + target.Guid;
            int removeIdx = -1;
            for (int i = 0; i < source.References.Length; i++)
            {
                var r = source.References[i];
                if (r == target.Name || (guidForm != null && r == guidForm))
                {
                    removeIdx = i;
                    break;
                }
            }

            if (removeIdx < 0) return; // reference doesn't exist

            // Confirmation dialog (unless Shift is held)
            if (!skipConfirmation)
            {
                if (!EditorUtility.DisplayDialog(
                        "Remove reference",
                        $"Remove reference {target.Name} from {source.Name}?",
                        "Remove", "Cancel"))
                    return;
            }

            // Clone and mutate
            var clone = source.Clone();
            var refs  = new List<string>(clone.References);
            refs.RemoveAt(removeIdx);
            clone.References = refs.ToArray();

            // Write with Undo group
            Undo.IncrementCurrentGroup();
            var groupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Remove Reference: {source.Name} → {target.Name}");
            _writer.Save(clone, "Remove Assembly Reference");
            Undo.CollapseUndoOperations(groupId);
        }
    }
}
