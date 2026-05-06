using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Tests.Editor.Helpers;
using NUnit.Framework;

namespace AssemblyArchitect.Tests.Editor
{
    internal sealed class AsmDefDataTests
    {
        [Test]
        public void Clone_DeepCopiesArrays()
        {
            var original = A.Def("Foo", "Bar", "Baz");
            original.DefineConstraints = new[] { "UNITY_EDITOR" };

            var clone = original.Clone();

            // Mutating clone's arrays must not affect original
            clone.References[0]       = "MUTATED";
            clone.DefineConstraints[0] = "MUTATED";

            Assert.AreEqual("Bar",         original.References[0]);
            Assert.AreEqual("UNITY_EDITOR", original.DefineConstraints[0]);
        }

        [Test]
        public void StableId_PrefersGuidOverName()
        {
            var withGuid    = A.DefWithGuid("MyAsm", "abc123");
            var withoutGuid = A.Def("MyAsm");

            Assert.AreEqual("abc123", withGuid.StableId);
            Assert.AreEqual("MyAsm",  withoutGuid.StableId);
        }

        [Test]
        public void ReferencesById_MatchesEitherForm()
        {
            var data = A.Def("Foo", "GUID:abc123", "BarByName");

            Assert.IsTrue(data.ReferencesById("abc123"),   "should match guid form");
            Assert.IsTrue(data.ReferencesById("BarByName"), "should match plain name");
            Assert.IsFalse(data.ReferencesById("Unknown"),  "should not match unknown");
        }
    }
}
