using System.IO;
using System.Linq;
using AssemblyArchitect.Editor.Commands;
using AssemblyArchitect.Editor.Infrastructure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace AssemblyArchitect.Tests.Editor.Integration
{
    internal sealed class CreateAsmDefIntegrationTests : IntegrationFixtureBase
    {
        private AsmDefRepository   _repo;
        private AsmDefWriter       _writer;

        [SetUp]
        public void SetUpTest()
        {
            _repo   = new AsmDefRepository();
            _writer = new AsmDefWriter();
        }

        [TearDown]
        public void TearDownTest()
        {
            LogAssert.NoUnexpectedReceived();
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Create_NewAsmDefAppearsInRepository()
        {
            _repo.NotifyChanged();

            var cmd  = new CreateAsmDefCommand(_repo, _writer);
            var args = new CreateAsmDefCommand.Args
            {
                Folder = SandboxPath + "/Create1",
                Name   = "CRE_New",
            };

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(args.Folder))
                AssetDatabase.CreateFolder(SandboxPath, "Create1");

            cmd.Execute(args);

            _repo.NotifyChanged();
            var all = _repo.LoadAll();
            bool found = all.Any(d => d.Name == "CRE_New");
            Assert.IsTrue(found, "Newly created assembly should appear in repository");
        }

        [Test]
        public void Create_WithAutoReference_AddsReferenceToParent()
        {
            var parent = CreateAsmDef("AutoRef", "AUTOREF_Parent");

            _repo.NotifyChanged();
            var cmd  = new CreateAsmDefCommand(_repo, _writer);
            var args = new CreateAsmDefCommand.Args
            {
                Folder                   = SandboxPath + "/AutoRef",
                Name                     = "AUTOREF_Child",
                AutoReferenceFromSourceId = parent.StableId,
            };

            cmd.Execute(args);

            // After creation the command writes a GUID-based reference ("GUID:xxx") to the parent.
            // Resolve the child's GUID from the repository so we can accept either form.
            _repo.NotifyChanged();
            var childData    = _repo.FindByName("AUTOREF_Child");
            var childGuidRef = childData != null && !string.IsNullOrEmpty(childData.Guid)
                ? "GUID:" + childData.Guid
                : null;

            var reloaded = ReadAsmDef(parent.AssetPath);
            bool hasRef  = reloaded.References.Any(r =>
                r == "AUTOREF_Child" || (childGuidRef != null && r == childGuidRef));
            Assert.IsTrue(hasRef, "Parent should reference the newly created child assembly");
        }

        [Test]
        public void Create_DuplicateName_ShowsErrorAndReturns()
        {
            // Pre-create the file
            var existing = CreateAsmDef("DupCreate", "DUP_Create");
            _repo.NotifyChanged();

            var absolutePath = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", existing.AssetPath));
            var mtimeBefore = File.GetLastWriteTime(absolutePath);

            var cmd  = new CreateAsmDefCommand(_repo, _writer);
            var args = new CreateAsmDefCommand.Args
            {
                Folder = SandboxPath + "/DupCreate",
                Name   = "DUP_Create",
            };

            cmd.Execute(args);

            var mtimeAfter = File.GetLastWriteTime(absolutePath);
            Assert.AreEqual(mtimeBefore, mtimeAfter,
                "Creating a duplicate should not overwrite the existing file");
        }
    }
}
