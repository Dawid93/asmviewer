using System;
using AssemblyArchitect.Editor.Core.Layout;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Editor.Settings
{
    [FilePath("ProjectSettings/Packages/com.ddev.assembly-architect/Settings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class AssemblyArchitectSettings : ScriptableSingleton<AssemblyArchitectSettings>
    {
        public static event Action Changed;

        public LayoutKind DefaultLayout = LayoutKind.Hierarchical;
        public bool FailBuildsOnCycles;
        public bool LayoutCacheIsTeamShared = true;
        public Color ProjectNodeColor = new Color(0.30f, 0.63f, 1f);
        public Color EmbeddedPkgColor = new Color(0.63f, 0.42f, 1f);
        public Color RegistryPkgColor = new Color(0.53f, 0.53f, 0.53f);
        public Color CycleEdgeColor = new Color(1f, 0.55f, 0.26f);
        public Color BrokenColor = new Color(1f, 0.30f, 0.30f);
        public int ForceLayoutIterations = 200;

        public void Save()
        {
            Save(true);
            Changed?.Invoke();
        }

        public void ResetToDefaults()
        {
            DefaultLayout = LayoutKind.Hierarchical;
            FailBuildsOnCycles = false;
            LayoutCacheIsTeamShared = true;
            ProjectNodeColor = new Color(0.30f, 0.63f, 1f);
            EmbeddedPkgColor = new Color(0.63f, 0.42f, 1f);
            RegistryPkgColor = new Color(0.53f, 0.53f, 0.53f);
            CycleEdgeColor = new Color(1f, 0.55f, 0.26f);
            BrokenColor = new Color(1f, 0.30f, 0.30f);
            ForceLayoutIterations = 200;
            Save();
        }
    }
}
