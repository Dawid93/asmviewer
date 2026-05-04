using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Core.Layout;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyArchitect.Tests.Editor
{
    internal sealed class LayoutTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static AsmDefData Node(string name, params string[] refs) =>
            new AsmDefData { Name = name, References = refs };

        private static DependencyGraphModel Build(params AsmDefData[] items) =>
            DependencyGraphModel.Build(new List<AsmDefData>(items));

        private static LayoutOptions Opts() => new LayoutOptions();

        // ── Determinism ───────────────────────────────────────────────────────

        [Test]
        public void HierarchicalLayout_SameInputTwice_ExactSameResult()
        {
            var graph  = Build(Node("A", "B"), Node("B", "C"), Node("C"));
            var layout = new HierarchicalLayout();
            var opts   = Opts();

            var r1 = layout.Compute(graph, opts);
            var r2 = layout.Compute(graph, opts);

            foreach (var id in r1.Keys)
                Assert.AreEqual(r1[id], r2[id], $"Position mismatch for node {id}");
        }

        [Test]
        public void ForceDirectedLayout_SameInputTwice_ExactSameResult()
        {
            var graph  = Build(Node("A", "B"), Node("B"), Node("C", "D"), Node("D"));
            var layout = new ForceDirectedLayout();
            var opts   = Opts();

            var r1 = layout.Compute(graph, opts);
            var r2 = layout.Compute(graph, opts);

            foreach (var id in r1.Keys)
                Assert.AreEqual(r1[id], r2[id], $"Position mismatch for node {id}");
        }

        // ── Hierarchical: chain on distinct layers ────────────────────────────

        [Test]
        public void HierarchicalLayout_ChainABC_ThreeDistinctLayers()
        {
            var graph  = Build(Node("A", "B"), Node("B", "C"), Node("C"));
            var layout = new HierarchicalLayout();
            var result = layout.Compute(graph, Opts());

            Assert.IsTrue(result.ContainsKey("A"));
            Assert.IsTrue(result.ContainsKey("B"));
            Assert.IsTrue(result.ContainsKey("C"));

            float yA = result["A"].y;
            float yB = result["B"].y;
            float yC = result["C"].y;

            // B must be on a different layer from A, and C from B
            Assert.AreNotEqual(yA, yB, "A and B should be on different layers");
            Assert.AreNotEqual(yB, yC, "B and C should be on different layers");
        }

        // ── Force-directed: disconnected pairs separated ──────────────────────

        [Test]
        public void ForceDirectedLayout_DisconnectedPairs_CentroidsFarApart()
        {
            // Two disconnected pairs: A-B and C-D
            var graph  = Build(Node("A", "B"), Node("B"), Node("C", "D"), Node("D"));
            var layout = new ForceDirectedLayout();
            var opts   = new LayoutOptions { Iterations = 400 };
            var result = layout.Compute(graph, opts);

            Vector2 centroidAB = (result["A"] + result["B"]) / 2f;
            Vector2 centroidCD = (result["C"] + result["D"]) / 2f;
            float distance = (centroidAB - centroidCD).magnitude;

            Assert.Greater(distance, opts.NodeWidth,
                $"Disconnected pair centroids should be > nodeWidth apart, got {distance}");
        }

        // ── Coverage: empty graph ─────────────────────────────────────────────

        [Test]
        public void BothLayouts_EmptyGraph_ReturnEmptyDictionary()
        {
            var graph = Build();
            Assert.AreEqual(0, new HierarchicalLayout().Compute(graph, Opts()).Count);
            Assert.AreEqual(0, new ForceDirectedLayout().Compute(graph, Opts()).Count);
        }
    }
}
