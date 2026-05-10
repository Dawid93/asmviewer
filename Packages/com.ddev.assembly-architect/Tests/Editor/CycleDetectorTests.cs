using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using NUnit.Framework;

namespace AssemblyArchitect.Tests.Editor
{
    internal sealed class CycleDetectorTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static AsmDefData MakeData(string name, params string[] refs)
        {
            return new AsmDefData { Name = name, References = refs };
        }

        private static DependencyGraphModel Build(params AsmDefData[] items)
        {
            return DependencyGraphModel.Build(new List<AsmDefData>(items));
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void EmptyGraph_NoCycles()
        {
            var graph = Build();
            var cycles = CycleDetector.FindCycles(graph);
            Assert.AreEqual(0, cycles.Count);
        }

        [Test]
        public void PureDAG_NoCycles_AndWouldCreateCycleCorrect()
        {
            // A -> B -> C
            var graph = Build(
                MakeData("A", "B"),
                MakeData("B", "C"),
                MakeData("C"));

            var cycles = CycleDetector.FindCycles(graph);
            Assert.AreEqual(0, cycles.Count);

            // Adding C->A would close the cycle
            Assert.IsTrue(CycleDetector.WouldCreateCycle(graph, "C", "A"));
            // Adding A->C would not (A already reaches C, but C can't reach A)
            Assert.IsFalse(CycleDetector.WouldCreateCycle(graph, "A", "C"));
        }

        [Test]
        public void TwoCycle_DetectedAsSingleSCC()
        {
            // A -> B, B -> A
            var graph = Build(
                MakeData("A", "B"),
                MakeData("B", "A"));

            var cycles = CycleDetector.FindCycles(graph);
            Assert.AreEqual(1, cycles.Count);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, cycles[0]);
        }

        [Test]
        public void SelfLoop_SingleElementCycle()
        {
            var graph = Build(MakeData("A", "A"));

            var cycles = CycleDetector.FindCycles(graph);
            Assert.AreEqual(1, cycles.Count);
            Assert.AreEqual(1, cycles[0].Count);
            Assert.AreEqual("A", cycles[0][0]);
        }

        [Test]
        public void NestedSCCs_DetectedSeparately()
        {
            // A->B->C->A  (SCC1)  and  D->E->D  (SCC2),  plus C->D
            var graph = Build(
                MakeData("A", "B"),
                MakeData("B", "C"),
                MakeData("C", "A", "D"),
                MakeData("D", "E"),
                MakeData("E", "D"));

            var cycles = CycleDetector.FindCycles(graph);
            Assert.AreEqual(2, cycles.Count);

            // Outer list sorted by first element
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, cycles[0]);
            CollectionAssert.AreEquivalent(new[] { "D", "E" }, cycles[1]);
        }

        [Test]
        public void DisconnectedComponents_BothDetected()
        {
            // A->B->A  and  X->Y->X  (no edges between components)
            var graph = Build(
                MakeData("A", "B"),
                MakeData("B", "A"),
                MakeData("X", "Y"),
                MakeData("Y", "X"));

            var cycles = CycleDetector.FindCycles(graph);
            Assert.AreEqual(2, cycles.Count);
        }
    }
}
