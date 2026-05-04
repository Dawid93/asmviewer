using System;
using System.Linq;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;

namespace AssemblyArchitect.Editor.Commands
{
    internal sealed class RemoveReferenceCommand
    {
        private readonly AsmDefRepository repo;
        private readonly AsmDefWriter writer;

        public RemoveReferenceCommand(AsmDefRepository repo, AsmDefWriter writer)
        {
            this.repo = repo ?? throw new ArgumentNullException(nameof(repo));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Execute(string sourceId, string targetId, bool skipConfirmation)
        {
            var data = repo.LoadAll();
            var source = data.FirstOrDefault(item => string.Equals(item.StableId, sourceId, StringComparison.Ordinal));
            var target = data.FirstOrDefault(item => string.Equals(item.StableId, targetId, StringComparison.Ordinal));
            if (source == null || target == null)
                return;

            if (AddReferenceCommand.IsReadOnly(source))
            {
                EditorUtility.DisplayDialog(
                    "Read-only assembly",
                    "Cannot modify a package not embedded in this project.",
                    "OK");
                return;
            }

            if (!AddReferenceCommand.References(source, target))
                return;

            if (!skipConfirmation &&
                !EditorUtility.DisplayDialog(
                    "Remove reference",
                    $"Remove reference {target.Name} from {source.Name}?",
                    "Remove",
                    "Cancel"))
            {
                return;
            }

            var guidReference = !string.IsNullOrEmpty(target.Guid) ? "GUID:" + target.Guid : null;
            var copy = source.Clone();
            copy.References = (copy.References ?? Array.Empty<string>())
                .Where(reference =>
                    !string.Equals(reference, target.Name, StringComparison.Ordinal) &&
                    (string.IsNullOrEmpty(guidReference) || !string.Equals(reference, guidReference, StringComparison.Ordinal)))
                .ToArray();

            Undo.IncrementCurrentGroup();
            var groupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Remove Reference: {source.Name} -> {target.Name}");
            writer.Save(copy, "Remove Assembly Reference");
            Undo.CollapseUndoOperations(groupId);
        }
    }
}
