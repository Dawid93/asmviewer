using System;

namespace AssemblyArchitect.Editor.Core
{
    /// <summary>Mirrors a single entry in the <c>versionDefines</c> array of a <c>.asmdef</c> file.</summary>
    [Serializable]
    public struct VersionDefine
    {
        /// <summary>Package name the version constraint applies to, e.g. <c>com.unity.entities</c>.</summary>
        public string Name;

        /// <summary>SemVer range expression, e.g. <c>[1.0,2.0)</c>.</summary>
        public string Expression;

        /// <summary>Scripting define symbol injected when the constraint is satisfied, e.g. <c>ENTITIES_INSTALLED</c>.</summary>
        public string Define;
    }
}
