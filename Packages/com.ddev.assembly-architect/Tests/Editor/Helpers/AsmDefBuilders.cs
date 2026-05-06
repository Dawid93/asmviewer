using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;

namespace AssemblyArchitect.Tests.Editor.Helpers
{
    /// <summary>Fluent builder helpers that keep test data creation terse.</summary>
    internal static class A
    {
        /// <summary>Creates an <see cref="AsmDefData"/> with just a name and optional references.</summary>
        public static AsmDefData Def(string name, params string[] references) =>
            new AsmDefData
            {
                Name       = name,
                References = references,
            };

        /// <summary>Creates an <see cref="AsmDefData"/> carrying a GUID as well.</summary>
        public static AsmDefData DefWithGuid(string name, string guid, params string[] references) =>
            new AsmDefData
            {
                Name       = name,
                Guid       = guid,
                References = references,
            };

        /// <summary>Creates an <see cref="AsmDefData"/> for a project asset.</summary>
        public static AsmDefData ProjectDef(string name, string assetPath = null, string guid = null) =>
            new AsmDefData
            {
                Name      = name,
                AssetPath = assetPath ?? $"Assets/{name}/{name}.asmdef",
                Guid      = guid ?? name + "-guid",
                Origin    = AsmDefOrigin.ProjectAssets,
            };

        /// <summary>Creates an <see cref="AsmDefData"/> for an embedded package.</summary>
        public static AsmDefData EmbeddedDef(string name) =>
            new AsmDefData
            {
                Name      = name,
                AssetPath = $"Packages/com.test.{name.ToLowerInvariant()}/{name}.asmdef",
                Guid      = name + "-guid",
                Origin    = AsmDefOrigin.EmbeddedPackage,
            };

        /// <summary>Minimal valid JSON for an .asmdef file with just a name.</summary>
        public static string Json(string name) =>
            $"{{\"name\":\"{name}\",\"references\":[],\"includePlatforms\":[],\"excludePlatforms\":[]," +
            $"\"allowUnsafeCode\":false,\"overrideReferences\":false,\"precompiledReferences\":[]," +
            $"\"autoReferenced\":true,\"defineConstraints\":[],\"versionDefines\":[],\"noEngineReferences\":false}}";

        /// <summary>Registers an .asmdef entry in both the fake enumerator and fake filesystem.</summary>
        public static void Register(
            FakeAsmDefAssetEnumerator enumerator,
            FakeFileSystem fs,
            string guid,
            string assetPath,
            string absolutePath,
            string name,
            string[] references = null)
        {
            enumerator.Add(guid, assetPath, absolutePath);
            var data = new AsmDefData
            {
                Name       = name,
                References = references ?? System.Array.Empty<string>(),
            };
            fs.AddFile(absolutePath, UnityEngine.JsonUtility.ToJson(data));
        }
    }
}
