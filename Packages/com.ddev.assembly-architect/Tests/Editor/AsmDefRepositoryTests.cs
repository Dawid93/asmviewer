using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Infrastructure;
using AssemblyArchitect.Tests.Editor.Helpers;
using NUnit.Framework;

namespace AssemblyArchitect.Tests.Editor
{
    internal sealed class AsmDefRepositoryTests
    {
        private FakeFileSystem _fs;
        private FakeAsmDefAssetEnumerator _enumerator;
        private AsmDefRepository _repo;

        [SetUp]
        public void SetUp()
        {
            _fs         = new FakeFileSystem();
            _enumerator = new FakeAsmDefAssetEnumerator();
            _repo       = new AsmDefRepository(_fs, _enumerator);
        }

        [Test]
        public void LoadAll_ParsesEveryAsmDef()
        {
            A.Register(_enumerator, _fs, "guid-a", "Assets/A/A.asmdef", "/proj/Assets/A/A.asmdef", "AssemblyA");
            A.Register(_enumerator, _fs, "guid-b", "Assets/B/B.asmdef", "/proj/Assets/B/B.asmdef", "AssemblyB");

            var all = _repo.LoadAll();

            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void LoadAll_ClassifiesOriginCorrectly()
        {
            // Project asset
            A.Register(_enumerator, _fs,
                "g1", "Assets/MyGame/Game.asmdef", "/proj/Assets/MyGame/Game.asmdef", "Game");

            // Embedded package (Packages/ but not in Library/PackageCache)
            A.Register(_enumerator, _fs,
                "g2", "Packages/com.my.pkg/MyPkg.asmdef", "/proj/Packages/com.my.pkg/MyPkg.asmdef", "MyPkg");

            // Registry package (path contains /Library/PackageCache/)
            A.Register(_enumerator, _fs,
                "g3", "Packages/com.reg.pkg/Reg.asmdef",
                "/proj/Library/PackageCache/com.reg.pkg@1.0.0/Reg.asmdef", "Reg");

            var all = _repo.LoadAll();
            Assert.AreEqual(3, all.Count);

            AsmDefData game = null, myPkg = null, reg = null;
            foreach (var d in all)
            {
                if (d.Name == "Game")   game  = d;
                if (d.Name == "MyPkg")  myPkg = d;
                if (d.Name == "Reg")    reg   = d;
            }

            Assert.IsNotNull(game,  "Game not found");
            Assert.IsNotNull(myPkg, "MyPkg not found");
            Assert.IsNotNull(reg,   "Reg not found");

            Assert.AreEqual(AsmDefOrigin.ProjectAssets,   game.Origin);
            Assert.AreEqual(AsmDefOrigin.EmbeddedPackage, myPkg.Origin);
            Assert.AreEqual(AsmDefOrigin.RegistryPackage, reg.Origin);
        }

        [Test]
        public void Changed_FiresWhenNotifyChangedCalled()
        {
            int callCount = 0;
            _repo.Changed += () => callCount++;

            _repo.NotifyChanged();
            _repo.NotifyChanged();

            Assert.AreEqual(2, callCount);
        }
    }
}
