using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Tests.Editor.Helpers;
using NUnit.Framework;

namespace AssemblyArchitect.Tests.Editor
{
    internal sealed class DependencyGraphModelTests
    {
        private static DependencyGraphModel Build(params AsmDefData[] items) =>
            DependencyGraphModel.Build(new List<AsmDefData>(items));

        [Test]
        public void EmptyInput_ProducesEmptyGraph()
        {
            var graph = Build();
            Assert.AreEqual(0, graph.Nodes.Count);
            Assert.AreEqual(0, graph.Edges.Count);
        }

        [Test]
        public void GuidReference_ResolvesCorrectly()
        {
            var target = A.DefWithGuid("Target", "guid-t");
            var source = A.Def("Source", "GUID:guid-t");

            var graph = Build(source, target);

            Assert.AreEqual(1, graph.Edges.Count);
            Assert.AreEqual("guid-t",   graph.Edges[0].TargetId);
            Assert.AreEqual(0,          graph.MissingReferences.Count);
        }

        [Test]
        public void DanglingReference_RecordedAsMissing()
        {
            var source = A.Def("Source", "NonExistent");

            var graph = Build(source);

            Assert.AreEqual(0, graph.Edges.Count);
            Assert.AreEqual(1, graph.MissingReferences.Count);
            Assert.AreEqual("NonExistent", graph.MissingReferences[0].MissingReference);
        }

        [Test]
        public void SelfReference_NotAddedAsEdge()
        {
            var self = A.Def("Self", "Self");

            var graph = Build(self);

            Assert.AreEqual(0, graph.Edges.Count);
            Assert.IsTrue(graph.HasSelfReference("Self"));
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var items = new[]
            {
                A.Def("Zebra", "Apple"),
                A.Def("Apple"),
                A.Def("Mango", "Apple", "Zebra"),
            };

            var g1 = Build(items);
            var g2 = Build(items);

            Assert.AreEqual(g1.Nodes.Count, g2.Nodes.Count);
            for (int i = 0; i < g1.Nodes.Count; i++)
                Assert.AreEqual(g1.Nodes[i].Id, g2.Nodes[i].Id, $"Node order differs at index {i}");

            Assert.AreEqual(g1.Edges.Count, g2.Edges.Count);
            for (int i = 0; i < g1.Edges.Count; i++)
            {
                Assert.AreEqual(g1.Edges[i].SourceId, g2.Edges[i].SourceId, $"Edge source differs at {i}");
                Assert.AreEqual(g1.Edges[i].TargetId, g2.Edges[i].TargetId, $"Edge target differs at {i}");
            }
        }
    }
}
