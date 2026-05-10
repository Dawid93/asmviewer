using System;
using System.IO;
using System.Linq;
using AssemblyArchitect.Editor.Commands;
using AssemblyArchitect.Editor.Infrastructure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace AssemblyArchitect.Tests.Editor.Integration
{
    internal sealed class AddRemoveReferenceIntegrationTests : IntegrationFixtureBase
    {
        private AsmDefRepository _repo;
        private AsmDefWriter     _writer;

        [SetUp]
        public void SetUpTest()
        {
            // Fresh repo per test so dirty state doesn't leak between tests
            _repo   = new AsmDefRepository();
            _writer = new AsmDefWriter();

            // Start each test with a clean undo stack so Undo.PerformUndo()
            // in RemoveReference_ReversibleByUndo only undoes this test's operations.
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDownTest()
        {
            LogAssert.NoUnexpectedReceived();
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void AddReference_WritesFileAndUpdatesGraph()
        {
            var a = CreateAsmDef("AddRef1", "AAR_A");
            var b = CreateAsmDef("AddRef1", "AAR_B");

            _repo.NotifyChanged();
            var cmd = new AddReferenceCommand(_repo, _writer);
            cmd.Execute(a.StableId, b.StableId);

            _repo.NotifyChanged();
            var reloaded = ReadAsmDef(a.AssetPath);
            bool hasRef = reloaded.References.Any(r =>
                r == b.Name || r == "GUID:" + b.Guid);
            Assert.IsTrue(hasRef, "A.asmdef should contain a reference to B after AddReference");
        }

        [Test]
        public void AddReference_DuplicatesAreNoOps()
        {
            var a = CreateAsmDef("DupRef", "DUP_A");
            var b = CreateAsmDef("DupRef", "DUP_B");

            _repo.NotifyChanged();
            var cmd = new AddReferenceCommand(_repo, _writer);
            cmd.Execute(a.StableId, b.StableId);

            _repo.NotifyChanged();
            var absoluteA = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", a.AssetPath));
            var mtime1 = File.GetLastWriteTime(absoluteA);

            // Small delay so a write would produce a different mtime
            System.Threading.Thread.Sleep(20);

            cmd.Execute(a.StableId, b.StableId);
            var mtime2 = File.GetLastWriteTime(absoluteA);

            Assert.AreEqual(mtime1, mtime2, "Second AddReference call must not touch the file");
        }

        [Test]
        public void RemoveReference_ReversibleByUndo()
        {
            var a = CreateAsmDef("UndoRef", "UNDO_A");
            var b = CreateAsmDef("UndoRef", "UNDO_B");

            _repo.NotifyChanged();
            var addCmd    = new AddReferenceCommand(_repo, _writer);
            var removeCmd = new RemoveReferenceCommand(_repo, _writer);

            // Add then remove
            addCmd.Execute(a.StableId, b.StableId);
            _repo.NotifyChanged();
            removeCmd.Execute(a.StableId, b.StableId, skipConfirmation: true);

            _repo.NotifyChanged();
            var afterRemove = ReadAsmDef(a.AssetPath);
            bool hasRefAfterRemove = afterRemove.References.Any(r =>
                r == b.Name || r == "GUID:" + b.Guid);
            Assert.IsFalse(hasRefAfterRemove, "Reference should be gone after remove");

            // Undo the remove
            Undo.PerformUndo();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _repo.NotifyChanged();
            var afterUndo = ReadAsmDef(a.AssetPath);
            bool hasRefAfterUndo = afterUndo.References.Any(r =>
                r == b.Name || r == "GUID:" + b.Guid);
            Assert.IsTrue(hasRefAfterUndo, "Reference should be restored after Undo");
        }

        [Test]
        public void AddReference_CycleDialog_Cancel_DoesNotModifyFile()
        {
            // A->B already exists, so adding B->A would create a cycle
            var a = CreateAsmDef("CycleDlg", "CYC_A");
            var b = CreateAsmDef("CycleDlg", "CYC_B");

            _repo.NotifyChanged();
            var cmd = new AddReferenceCommand(_repo, _writer);
            cmd.Execute(a.StableId, b.StableId);
            _repo.NotifyChanged();

            var absoluteB = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", b.AssetPath));
            var mtimeBefore = File.GetLastWriteTime(absoluteB);

            // Attempt to add B->A with dialog returning false (cancel)
            var cancelCmd = new AddReferenceCommand(_repo, _writer, _ => false);
            cancelCmd.Execute(b.StableId, a.StableId);

            var mtimeAfter = File.GetLastWriteTime(absoluteB);
            Assert.AreEqual(mtimeBefore, mtimeAfter, "Cancelling cycle dialog must not modify the file");
        }
    }
}
