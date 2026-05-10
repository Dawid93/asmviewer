using System;
using AssemblyArchitect.Editor.Core.Layout;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Editor.Settings
{
    [FilePath("ProjectSettings/Packages/com.ddev.assembly-architect/Settings.asset",
              FilePathAttribute.Location.ProjectFolder)]
    internal sealed class AssemblyArchitectSettings : ScriptableSingleton<AssemblyArchitectSettings>
    {
        public LayoutKind DefaultLayout          = LayoutKind.Hierarchical;
        public bool       FailBuildsOnCycles     = false;
        public bool       LayoutCacheIsTeamShared = true;
        public Color      ProjectNodeColor       = new Color(0.30f, 0.63f, 1f);
        public Color      EmbeddedPkgColor       = new Color(0.63f, 0.42f, 1f);
        public Color      RegistryPkgColor       = new Color(0.53f, 0.53f, 0.53f);
        public Color      CycleEdgeColor         = new Color(1f, 0.55f, 0.26f);
        public Color      BrokenColor            = new Color(1f, 0.30f, 0.30f);
        public int        ForceLayoutIterations  = 200;

        /// <summary>Raised after any setting change is saved.</summary>
        public static event Action Changed;

        /// <summary>Saves the asset and notifies listeners.</summary>
        public void Save()
        {
            base.Save(true);
            Changed?.Invoke();
        }
    }
}
