using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssemblyArchitect.Editor.Core
{
    /// <summary>Detects dependency cycles in a <see cref="DependencyGraphModel"/> using Tarjan's SCC algorithm.</summary>
    internal static class CycleDetector
    {
        /// <summary>
        /// Returns every strongly-connected component of size ≥ 2, plus any node with a self-edge.
        /// Each inner list is sorted ascending by node id; the outer list is sorted by the first element.
        /// </summary>
        public static IReadOnlyList<IReadOnlyList<string>> FindCycles(DependencyGraphModel graph)
        {
            var state  = new TarjanState(graph);
            foreach (var node in graph.Nodes)
                if (!state.Visited.Contains(node.Id))
                    state.StrongConnect(node.Id);

            state.Result.Sort((a, b) => string.Compare(a[0], b[0], System.StringComparison.Ordinal));

            var result = new List<IReadOnlyList<string>>(state.Result.Count);
            foreach (var cycle in state.Result)
                result.Add(new ReadOnlyCollection<string>(cycle));
            return new ReadOnlyCollection<IReadOnlyList<string>>(result);
        }

        /// <summary>
        /// Returns <c>true</c> if adding an edge from <paramref name="sourceId"/> to <paramref name="targetId"/>
        /// would introduce a cycle — i.e., <paramref name="targetId"/> can already reach <paramref name="sourceId"/>.
        /// </summary>
        public static bool WouldCreateCycle(DependencyGraphModel graph, string sourceId, string targetId)
        {
            // Self-reference is always a cycle
            if (sourceId == targetId) return true;

            // DFS from targetId; if we reach sourceId the proposed edge closes a cycle
            var visited = new HashSet<string>(System.StringComparer.Ordinal);
            var stack   = new Stack<string>();
            stack.Push(targetId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!visited.Add(current)) continue;

                if (graph.Outgoing.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor == sourceId) return true;
                        if (!visited.Contains(neighbor))
                            stack.Push(neighbor);
                    }
                }
            }

            return false;
        }

        // ── Tarjan implementation ─────────────────────────────────────────────

        private sealed class TarjanState
        {
            private readonly DependencyGraphModel _graph;
            private readonly Dictionary<string, int> _index   = new Dictionary<string, int>(System.StringComparer.Ordinal);
            private readonly Dictionary<string, int> _lowlink = new Dictionary<string, int>(System.StringComparer.Ordinal);
            private readonly HashSet<string> _onStack = new HashSet<string>(System.StringComparer.Ordinal);
            private readonly Stack<string> _stack = new Stack<string>();

            public readonly HashSet<string> Visited = new HashSet<string>(System.StringComparer.Ordinal);
            public readonly List<List<string>> Result = new List<List<string>>();

            private int _counter;

            public TarjanState(DependencyGraphModel graph) => _graph = graph;

            public void StrongConnect(string v)
            {
                _index[v]   = _counter;
                _lowlink[v] = _counter;
                _counter++;
                Visited.Add(v);
                _stack.Push(v);
                _onStack.Add(v);

                if (_graph.Outgoing.TryGetValue(v, out var neighbors))
                {
                    foreach (var w in neighbors)
                    {
                        if (!Visited.Contains(w))
                        {
                            StrongConnect(w);
                            _lowlink[v] = System.Math.Min(_lowlink[v], _lowlink[w]);
                        }
                        else if (_onStack.Contains(w))
                        {
                            _lowlink[v] = System.Math.Min(_lowlink[v], _index[w]);
                        }
                    }
                }

                // v is a root of an SCC
                if (_lowlink[v] == _index[v])
                {
                    var scc = new List<string>();
                    string w;
                    do
                    {
                        w = _stack.Pop();
                        _onStack.Remove(w);
                        scc.Add(w);
                    } while (w != v);

                    // Include SCC with ≥ 2 nodes, or single-node SCCs that have a self-edge
                    if (scc.Count >= 2 || HasSelfEdge(v))
                    {
                        scc.Sort(System.StringComparer.Ordinal);
                        Result.Add(scc);
                    }
                }
            }

            private bool HasSelfEdge(string id) => _graph.HasSelfReference(id);
        }
    }
}
