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
            fs.AddFile(absolutePath, AsmDefJsonSerializer.Serialize(data));
        }
    }
}
