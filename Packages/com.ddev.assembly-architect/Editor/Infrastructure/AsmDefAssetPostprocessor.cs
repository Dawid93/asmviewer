using UnityEditor;

namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>Watches for <c>.asmdef</c> asset changes and invalidates <see cref="AsmDefRepository.Default"/>.</summary>
    internal sealed class AsmDefAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssets)
        {
            if (TouchesAsmDef(importedAssets)  ||
                TouchesAsmDef(deletedAssets)    ||
                TouchesAsmDef(movedAssets)      ||
                TouchesAsmDef(movedFromAssets))
            {
                AsmDefRepository.Default.NotifyChanged();
            }
        }

        private static bool TouchesAsmDef(string[] paths)
        {
            foreach (var p in paths)
                if (p.EndsWith(".asmdef", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
