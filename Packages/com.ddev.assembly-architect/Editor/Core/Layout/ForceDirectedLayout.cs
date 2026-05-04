using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Random = System.Random;

namespace AssemblyArchitect.Editor.Core.Layout
{
    /// <summary>Fruchterman–Reingold force-directed layout with a deterministic PRNG seed.</summary>
    internal sealed class ForceDirectedLayout : IGraphLayout
    {
        public IReadOnlyDictionary<string, Vector2> Compute(DependencyGraphModel graph, LayoutOptions options)
        {
            if (options == null) options = new LayoutOptions();

            var nodes = graph.Nodes;
            int n = nodes.Count;

            if (n == 0)
                return new ReadOnlyDictionary<string, Vector2>(new Dictionary<string, Vector2>());

            // Deterministic initial positions
            float side = (float)Math.Sqrt(n) * (options.NodeWidth + options.HorizontalSpacing);
            var rng = new Random(options.Seed);
            var pos = new Dictionary<string, Vector2>(n, StringComparer.Ordinal);
            var ids = new string[n];
            for (int i = 0; i < n; i++)
            {
                ids[i] = nodes[i].Id;
                pos[ids[i]] = new Vector2(
                    (float)(rng.NextDouble() * side - side / 2.0),
                    (float)(rng.NextDouble() * side - side / 2.0));
            }

            // Optimal distance k
            float area = side * side;
            float k    = (float)Math.Sqrt(area / Math.Max(n, 1));
            float t0   = side / 2f;
            int   iter = Math.Max(1, options.Iterations);

            var disp = new Dictionary<string, Vector2>(n, StringComparer.Ordinal);

            for (int it = 0; it < iter; it++)
            {
                float t = t0 * (1f - (float)it / iter);

                // Reset displacements
                foreach (var id in ids) disp[id] = Vector2.zero;

                // Repulsive forces (O(n²))
                for (int a = 0; a < n; a++)
                {
                    for (int b = a + 1; b < n; b++)
                    {
                        var delta = pos[ids[a]] - pos[ids[b]];
                        float dist = delta.magnitude;
                        if (dist < 0.0001f) { dist = 0.0001f; delta = new Vector2(0.0001f, 0f); }
                        float rep = k * k / dist;
                        var force = delta.normalized * rep;
                        disp[ids[a]] += force;
                        disp[ids[b]] -= force;
                    }
                }

                // Attractive forces along edges
                foreach (var edge in graph.Edges)
                {
                    if (!pos.ContainsKey(edge.SourceId) || !pos.ContainsKey(edge.TargetId)) continue;
                    var delta = pos[edge.SourceId] - pos[edge.TargetId];
                    float dist = delta.magnitude;
                    if (dist < 0.0001f) continue;
                    float att = dist * dist / k;
                    var force = delta.normalized * att;
                    disp[edge.SourceId] -= force;
                    disp[edge.TargetId] += force;
                }

                // Apply displacements clamped by temperature
                foreach (var id in ids)
                {
                    var d    = disp[id];
                    float dm = d.magnitude;
                    if (dm < 0.0001f) continue;
                    pos[id] += d.normalized * Math.Min(dm, t);
                }
            }

            return new ReadOnlyDictionary<string, Vector2>(pos);
        }
    }
}
