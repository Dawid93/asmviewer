using System;
using System.IO;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssemblyArchitect.Tests.Editor.Integration
{
    /// <summary>Base class that creates and tears down a temporary sandbox folder in <c>Assets/</c>.</summary>
    internal abstract class IntegrationFixtureBase
    {
        protected string SandboxPath { get; private set; }

        [OneTimeSetUp]
        public void CreateSandbox()
        {
            // Importing .asmdef files triggers assembly recompilation and can cause a domain reload
            // that kills the running test session. Lock reloads for the duration of the test class.
            EditorApplication.LockReloadAssemblies();

            var uid = Guid.NewGuid().ToString("N").Substring(0, 8);
            SandboxPath = $"Assets/AssemblyArchitectTests_{uid}";
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(SandboxPath));
        }

        [OneTimeTearDown]
        public void DeleteSandbox()
        {
            if (!string.IsNullOrEmpty(SandboxPath) && AssetDatabase.IsValidFolder(SandboxPath))
                AssetDatabase.DeleteAsset(SandboxPath);

            EditorApplication.UnlockReloadAssemblies();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Writes a real .asmdef file inside the sandbox, imports it, and returns the parsed data
        /// with GUID and AssetPath populated.
        /// </summary>
        protected AsmDefData CreateAsmDef(string subFolder, string name, params string[] refs)
        {
            var folder    = string.IsNullOrEmpty(subFolder) ? SandboxPath : $"{SandboxPath}/{subFolder}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(SandboxPath, subFolder);

            var assetPath    = $"{folder}/{name}.asmdef";
            var absolutePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", assetPath));

            var data = new AsmDefData { Name = name, References = refs };
            File.WriteAllText(absolutePath, AsmDefJsonSerializer.Serialize(data));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            data.AssetPath = assetPath;
            data.Guid      = guid;
            data.Origin    = AsmDefOrigin.ProjectAssets;
            return data;
        }

        /// <summary>Reads and parses the .asmdef file at <paramref name="assetPath"/>.</summary>
        protected AsmDefData ReadAsmDef(string assetPath)
        {
            var absolutePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", assetPath));
            var json = File.ReadAllText(absolutePath);
            var data = AsmDefJsonSerializer.Deserialize(json);
            data.AssetPath = assetPath;
            data.Guid      = AssetDatabase.AssetPathToGUID(assetPath);
            data.Origin    = AsmDefOrigin.ProjectAssets;
            return data;
        }
    }
}
