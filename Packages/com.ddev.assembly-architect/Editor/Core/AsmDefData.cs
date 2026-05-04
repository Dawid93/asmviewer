// Schema reference: https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html

using System;

namespace AssemblyArchitect.Editor.Core
{
    /// <summary>
    /// Pure-C# mirror of Unity's <c>.asmdef</c> JSON schema.
    /// Field names match Unity's JSON keys exactly so that <c>JsonUtility</c> can round-trip
    /// instances without a custom converter. Non-serialized metadata fields are excluded from JSON.
    /// </summary>
    [Serializable]
    public class AsmDefData
    {
        /// <summary>Assembly name, e.g. <c>MyCompany.MyFeature</c>.</summary>
        public string Name = string.Empty;

        /// <summary>Default root namespace for scripts in this assembly.</summary>
        public string RootNamespace = string.Empty;

        /// <summary>
        /// References to other assemblies. Entries may be a plain name or a GUID reference
        /// in the form <c>GUID:xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx</c>.
        /// </summary>
        public string[] References = Array.Empty<string>();

        /// <summary>Platforms on which this assembly is included. Empty means all platforms.</summary>
        public string[] IncludePlatforms = Array.Empty<string>();

        /// <summary>Platforms on which this assembly is excluded.</summary>
        public string[] ExcludePlatforms = Array.Empty<string>();

        /// <summary>Whether unsafe C# code is permitted in this assembly.</summary>
        public bool AllowUnsafeCode;

        /// <summary>Whether this assembly is automatically referenced by all other assemblies.</summary>
        public bool AutoReferenced;

        /// <summary>Whether the list of precompiled references overrides the default set.</summary>
        public bool OverrideReferences;

        /// <summary>Precompiled (DLL) references.</summary>
        public string[] PrecompiledReferences = Array.Empty<string>();

        /// <summary>Scripting define constraints that must be satisfied for this assembly to be included.</summary>
        public string[] DefineConstraints = Array.Empty<string>();

        /// <summary>Version-based define symbols injected when package version constraints are met.</summary>
        public VersionDefine[] VersionDefines = Array.Empty<VersionDefine>();

        /// <summary>When <c>true</c>, Unity engine and editor assemblies are not referenced.</summary>
        public bool NoEngineReferences;

        // ── Runtime metadata (not written to JSON) ────────────────────────────

        /// <summary>Project-relative asset path, e.g. <c>Assets/Foo/Foo.asmdef</c> or <c>Packages/.../Foo.asmdef</c>.</summary>
        [NonSerialized] public string AssetPath;

        /// <summary>AssetDatabase GUID for this asset.</summary>
        [NonSerialized] public string Guid;

        /// <summary>Where this assembly definition originates within the Unity project layout.</summary>
        [NonSerialized] public AsmDefOrigin Origin;

        // ── Derived properties ────────────────────────────────────────────────

        /// <summary>Returns <see cref="Guid"/> when non-empty; otherwise falls back to <see cref="Name"/>.</summary>
        public string StableId => !string.IsNullOrEmpty(Guid) ? Guid : Name;

        // ── Methods ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a deep copy of this instance. Array fields are new allocations containing
        /// the same element values; non-serialized metadata fields are also copied.
        /// </summary>
        public AsmDefData Clone()
        {
            var copy = new AsmDefData
            {
                Name                 = Name,
                RootNamespace        = RootNamespace,
                References           = (string[])References.Clone(),
                IncludePlatforms     = (string[])IncludePlatforms.Clone(),
                ExcludePlatforms     = (string[])ExcludePlatforms.Clone(),
                AllowUnsafeCode      = AllowUnsafeCode,
                AutoReferenced       = AutoReferenced,
                OverrideReferences   = OverrideReferences,
                PrecompiledReferences = (string[])PrecompiledReferences.Clone(),
                DefineConstraints    = (string[])DefineConstraints.Clone(),
                VersionDefines       = (VersionDefine[])VersionDefines.Clone(),
                NoEngineReferences   = NoEngineReferences,
                AssetPath            = AssetPath,
                Guid                 = Guid,
                Origin               = Origin,
            };
            return copy;
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="References"/> contains <paramref name="id"/>
        /// either as a plain assembly name or in <c>GUID:&lt;id&gt;</c> form.
        /// </summary>
        public bool ReferencesById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            var guidForm = "GUID:" + id;
            foreach (var r in References)
            {
                if (string.Equals(r, id, StringComparison.Ordinal) ||
                    string.Equals(r, guidForm, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
