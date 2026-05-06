using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using NUnit.Framework;

namespace AssemblyArchitect.Tests.Editor
{
    internal sealed class AsmDefJsonSerializerTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new AsmDefData
            {
                Name                  = "MyAssembly",
                RootNamespace         = "MyNS",
                References            = new[] { "GUID:abc123", "OtherAssembly" },
                IncludePlatforms      = new[] { "Editor" },
                ExcludePlatforms      = System.Array.Empty<string>(),
                AllowUnsafeCode       = true,
                AutoReferenced        = false,
                OverrideReferences    = true,
                PrecompiledReferences = new[] { "Newtonsoft.Json.dll" },
                DefineConstraints     = new[] { "UNITY_EDITOR" },
                VersionDefines        = System.Array.Empty<VersionDefine>(),
                NoEngineReferences    = false,
            };

            var json   = AsmDefJsonSerializer.Serialize(original);
            var result = AsmDefJsonSerializer.Deserialize(json);

            Assert.AreEqual(original.Name,                  result.Name);
            Assert.AreEqual(original.RootNamespace,         result.RootNamespace);
            CollectionAssert.AreEqual(original.References,            result.References);
            CollectionAssert.AreEqual(original.IncludePlatforms,      result.IncludePlatforms);
            Assert.AreEqual(original.AllowUnsafeCode,       result.AllowUnsafeCode);
            Assert.AreEqual(original.AutoReferenced,        result.AutoReferenced);
            Assert.AreEqual(original.OverrideReferences,    result.OverrideReferences);
            CollectionAssert.AreEqual(original.PrecompiledReferences, result.PrecompiledReferences);
            CollectionAssert.AreEqual(original.DefineConstraints,     result.DefineConstraints);
            Assert.AreEqual(original.NoEngineReferences,    result.NoEngineReferences);
        }

        [Test]
        public void KeyOrder_MatchesCanonicalSchema()
        {
            var data = new AsmDefData { Name = "Foo" };
            var json = AsmDefJsonSerializer.Serialize(data);

            // Keys must appear in canonical Unity schema order
            int nameIdx       = json.IndexOf("\"name\"",              System.StringComparison.Ordinal);
            int nsIdx         = json.IndexOf("\"rootNamespace\"",     System.StringComparison.Ordinal);
            int refsIdx       = json.IndexOf("\"references\"",        System.StringComparison.Ordinal);
            int includeIdx    = json.IndexOf("\"includePlatforms\"",  System.StringComparison.Ordinal);
            int noEngineIdx   = json.IndexOf("\"noEngineReferences\"",System.StringComparison.Ordinal);

            Assert.Greater(nsIdx,       nameIdx,    "rootNamespace should come after name");
            Assert.Greater(refsIdx,     nsIdx,      "references should come after rootNamespace");
            Assert.Greater(includeIdx,  refsIdx,    "includePlatforms should come after references");
            Assert.Greater(noEngineIdx, includeIdx, "noEngineReferences should be last");
        }

        [Test]
        public void EmptyArrays_WrittenInline()
        {
            var data = new AsmDefData { Name = "Bar" };
            var json = AsmDefJsonSerializer.Serialize(data);

            // Empty arrays should be on one line: "key": []
            StringAssert.Contains("\"references\": [],",       json);
            StringAssert.Contains("\"includePlatforms\": [],", json);
            StringAssert.Contains("\"versionDefines\": [],",   json);
        }
    }
}
