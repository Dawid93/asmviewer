using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace AssemblyArchitect.Editor.Core.Layout
{
    /// <summary>
    /// Sugiyama-style hierarchical layout.
    /// Cycles are broken by removing one edge per cycle before layering.
    /// </summary>
    internal sealed class HierarchicalLayout : IGraphLayout
    {
        public IReadOnlyDictionary<string, Vector2> Compute(DependencyGraphModel graph, LayoutOptions options)
        {
            if (options == null) options = new LayoutOptions();

            var nodes = graph.Nodes;
            if (nodes.Count == 0)
                return new ReadOnlyDictionary<string, Vector2>(new Dictionary<string, Vector2>());

            // Build a mutable adjacency set, removing one edge per cycle (lexicographically smaller)
            var adj = BuildDagAdjacency(graph);

            // Assign layers: longest-path from any source
            var layers = AssignLayers(nodes, adj);

            // Group nodes by layer, ordered by id for determinism before barycenter
            var byLayer = new SortedDictionary<int, List<string>>();
            foreach (var kv in layers)
            {
                if (!byLayer.TryGetValue(kv.Value, out var list))
                    byLayer[kv.Value] = list = new List<string>();
                list.Add(kv.Key);
            }
            foreach (var list in byLayer.Values) list.Sort(StringComparer.Ordinal);

            // Barycenter ordering (4 sweeps)
            ApplyBarycenterSweeps(byLayer, adj, 4);

            // Assign coordinates
            float stepX = options.NodeWidth + options.HorizontalSpacing;
            float stepY = options.NodeHeight + options.VerticalSpacing;

            var result = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            foreach (var kv in byLayer)
            {
                var layer = kv.Key;
                var ids   = kv.Value;
                float totalWidth = ids.Count * stepX - options.HorizontalSpacing;
                float startX     = -totalWidth / 2f;
                for (int col = 0; col < ids.Count; col++)
                    result[ids[col]] = new Vector2(startX + col * stepX, layer * stepY);
            }

            return new ReadOnlyDictionary<string, Vector2>(result);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Dictionary<string, HashSet<string>> BuildDagAdjacency(DependencyGraphModel graph)
        {
            var adj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var node in graph.Nodes)
                adj[node.Id] = new HashSet<string>(StringComparer.Ordinal);

            // Collect edges to remove (one per detected cycle — lexicographically smallest)
            var cycleEdgesToRemove = new HashSet<(string, string)>();
            var cycles = CycleDetector.FindCycles(graph);
            foreach (var cycle in cycles)
            {
                if (cycle.Count < 2) continue;
                // Find the lex-smallest edge within the cycle
                string bestSrc = null, bestTgt = null;
                for (int i = 0; i < cycle.Count; i++)
                {
                    var src = cycle[i];
                    var tgt = cycle[(i + 1) % cycle.Count];
                    if (bestSrc == null ||
                        string.Compare(src, bestSrc, StringComparison.Ordinal) < 0 ||
                        (src == bestSrc && string.Compare(tgt, bestTgt, StringComparison.Ordinal) < 0))
                    {
                        bestSrc = src;
                        bestTgt = tgt;
                    }
                }
                if (bestSrc != null) cycleEdgesToRemove.Add((bestSrc, bestTgt));
            }

            foreach (var edge in graph.Edges)
            {
                if (cycleEdgesToRemove.Contains((edge.SourceId, edge.TargetId))) continue;
                if (adj.TryGetValue(edge.SourceId, out var set))
                    set.Add(edge.TargetId);
            }

            return adj;
        }

        private static Dictionary<string, int> AssignLayers(
            IReadOnlyList<AsmDefNodeModel> nodes,
            Dictionary<string, HashSet<string>> adj)
        {
            var layer = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var n in nodes) layer[n.Id] = 0;

            // Iterative longest-path (Bellman-Ford style; DAG so no negatives)
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var kv in adj)
                {
                    foreach (var tgt in kv.Value)
                    {
                        int proposed = layer[kv.Key] + 1;
                        if (proposed > layer[tgt])
                        {
                            layer[tgt] = proposed;
                            changed = true;
                        }
                    }
                }
            }

            return layer;
        }

        private static void ApplyBarycenterSweeps(
            SortedDictionary<int, List<string>> byLayer,
            Dictionary<string, HashSet<string>> adj,
            int sweeps)
        {
            // Build reverse adjacency for bottom-up sweeps
            var radj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in adj)
            {
                if (!radj.ContainsKey(kv.Key)) radj[kv.Key] = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tgt in kv.Value)
                {
                    if (!radj.TryGetValue(tgt, out var set))
                        radj[tgt] = set = new HashSet<string>(StringComparer.Ordinal);
                    set.Add(kv.Key);
                }
            }

            var layerKeys = new List<int>(byLayer.Keys);
            for (int s = 0; s < sweeps; s++)
            {
                bool topDown = (s % 2 == 0);
                var order = topDown ? layerKeys : new List<int>(Reversed(layerKeys));
                var neighbors = topDown ? adj : radj;

                foreach (var lyr in order)
                {
                    var ids = byLayer[lyr];
                    var positions = new Dictionary<string, int>(StringComparer.Ordinal);
                    for (int i = 0; i < ids.Count; i++) positions[ids[i]] = i;

                    var barycenters = new List<(float bc, string id)>();
                    foreach (var id in ids)
                    {
                        float bc = ComputeBarycenter(id, neighbors, positions, byLayer, lyr, topDown);
                        barycenters.Add((bc, id));
                    }

                    barycenters.Sort((a, b) =>
                    {
                        int c = a.bc.CompareTo(b.bc);
                        return c != 0 ? c : string.Compare(a.id, b.id, StringComparison.Ordinal);
                    });

                    ids.Clear();
                    foreach (var t in barycenters) ids.Add(t.id);
                }
            }
        }

        private static float ComputeBarycenter(
            string id,
            Dictionary<string, HashSet<string>> neighbors,
            Dictionary<string, int> currentPositions,
            SortedDictionary<int, List<string>> byLayer,
            int currentLayer,
            bool topDown)
        {
            if (!neighbors.TryGetValue(id, out var nbrs) || nbrs.Count == 0)
                return currentPositions.TryGetValue(id, out var p) ? p : 0f;

            float sum = 0f;
            int   cnt = 0;
            foreach (var nb in nbrs)
            {
                // Find nb's position in its layer
                foreach (var kv in byLayer)
                {
                    if (kv.Key == currentLayer) continue;
                    int idx = kv.Value.IndexOf(nb);
                    if (idx >= 0) { sum += idx; cnt++; break; }
                }
            }
            return cnt > 0 ? sum / cnt : (currentPositions.TryGetValue(id, out var pos) ? pos : 0f);
        }

        private static IEnumerable<T> Reversed<T>(List<T> list)
        {
            for (int i = list.Count - 1; i >= 0; i--) yield return list[i];
        }
    }
}
