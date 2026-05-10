using System;
using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Editor.Commands
{
    /// <summary>Adds an assembly reference from source to target, with cycle and read-only guards.</summary>
    internal sealed class AddReferenceCommand
    {
        private readonly AsmDefRepository        _repo;
        private readonly AsmDefWriter            _writer;
        private readonly Func<DialogPrompt, bool> _showDialog;

        /// <param name="showDialog">
        /// Optional seam for cycle-warning dialogs. Receives a <see cref="DialogPrompt"/> and returns
        /// <c>true</c> to confirm, <c>false</c> to cancel. Defaults to <see cref="EditorUtility.DisplayDialog"/>.
        /// </param>
        public AddReferenceCommand(AsmDefRepository repo, AsmDefWriter writer,
                                   Func<DialogPrompt, bool> showDialog = null)
        {
            _repo       = repo;
            _writer     = writer;
            _showDialog = showDialog ?? (p => EditorUtility.DisplayDialog(p.Title, p.Message, p.Confirm, p.Cancel));
        }

        public void Execute(string sourceId, string targetId)
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
                Debug.LogWarning($"[AssemblyArchitect] AddReference: could not resolve '{sourceId}' or '{targetId}'.");
                return;
            }

            // Self-reference
            if (sourceId == targetId) return;

            // Duplicate guard
            var guidForm = string.IsNullOrEmpty(target.Guid) ? null : "GUID:" + target.Guid;
            foreach (var r in source.References)
                if (r == target.Name || (guidForm != null && r == guidForm)) return;

            // Read-only guard
            if (source.Origin == AsmDefOrigin.RegistryPackage || source.Origin == AsmDefOrigin.BuiltIn)
            {
                EditorUtility.DisplayDialog("Read-only assembly",
                    "Cannot modify a package not embedded in this project.", "OK");
                return;
            }

            // Cycle guard
            var model = DependencyGraphModel.Build(all);
            if (CycleDetector.WouldCreateCycle(model, sourceId, targetId))
            {
                var prompt = new DialogPrompt(
                    "Creates a dependency cycle",
                    $"Adding {target.Name} as a reference of {source.Name} would create a cycle.\nAdd anyway?",
                    "Add anyway", "Cancel");
                if (!_showDialog(prompt))
                    return;
            }

            // Clone and mutate
            var clone = source.Clone();
            var refs  = new List<string>(clone.References);
            refs.Add(!string.IsNullOrEmpty(target.Guid) ? "GUID:" + target.Guid : target.Name);
            clone.References = refs.ToArray();

            // Write with Undo group
            Undo.IncrementCurrentGroup();
            var groupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Add Reference: {source.Name} → {target.Name}");
            _writer.Save(clone, "Add Assembly Reference");
            Undo.CollapseUndoOperations(groupId);
        }
    }
}
