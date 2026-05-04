using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssemblyArchitect.Editor.Core
{
    /// <summary>
    /// Immutable dependency graph built from a snapshot of <see cref="AsmDefData"/> instances.
    /// All collections are sorted for determinism. Use <see cref="Build"/> to construct.
    /// </summary>
    public sealed class DependencyGraphModel
    {
        /// <summary>All nodes, sorted ascending by <see cref="AsmDefNodeModel.Name"/> (case-insensitive).</summary>
        public IReadOnlyList<AsmDefNodeModel> Nodes { get; }

        /// <summary>All edges, sorted by <c>(SourceId, TargetId)</c>.</summary>
        public IReadOnlyList<AsmDefEdgeModel> Edges { get; }

        /// <summary>References that could not be resolved, sorted by <c>(SourceId, MissingReference)</c>.</summary>
        public IReadOnlyList<MissingReferenceInfo> MissingReferences { get; }

        /// <summary>Fast node lookup by <see cref="AsmDefNodeModel.Id"/>.</summary>
        public IReadOnlyDictionary<string, AsmDefNodeModel> NodesById { get; }

        /// <summary>Maps a source node id to the ids of all assemblies it references.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Outgoing { get; }

        /// <summary>Maps a target node id to the ids of all assemblies that reference it.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Incoming { get; }

        private readonly HashSet<string> _selfReferenceIds;

        private DependencyGraphModel(
            IReadOnlyList<AsmDefNodeModel> nodes,
            IReadOnlyList<AsmDefEdgeModel> edges,
            IReadOnlyList<MissingReferenceInfo> missing,
            IReadOnlyDictionary<string, AsmDefNodeModel> nodesById,
            IReadOnlyDictionary<string, IReadOnlyList<string>> outgoing,
            IReadOnlyDictionary<string, IReadOnlyList<string>> incoming,
            HashSet<string> selfReferenceIds)
        {
            Nodes             = nodes;
            Edges             = edges;
            MissingReferences = missing;
            NodesById         = nodesById;
            Outgoing          = outgoing;
            Incoming          = incoming;
            _selfReferenceIds = selfReferenceIds;
        }

        /// <summary>Returns <c>true</c> if the node with <paramref name="id"/> has a self-reference in its asmdef.</summary>
        public bool HasSelfReference(string id) => _selfReferenceIds.Contains(id);

        /// <summary>Returns the node with <paramref name="id"/>, or <c>false</c> if not found.</summary>
        public bool TryGetNode(string id, out AsmDefNodeModel node) => NodesById.TryGetValue(id ?? string.Empty, out node);

        /// <summary>Builds an immutable <see cref="DependencyGraphModel"/> from a snapshot of assembly data.</summary>
        public static DependencyGraphModel Build(IReadOnlyList<AsmDefData> data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Index by guid and by name for fast resolution
            var byGuid = new Dictionary<string, AsmDefData>(StringComparer.Ordinal);
            var byName = new Dictionary<string, AsmDefData>(StringComparer.Ordinal);
            foreach (var d in data)
            {
                if (!string.IsNullOrEmpty(d.Guid))
                    byGuid[d.Guid] = d;
                if (!string.IsNullOrEmpty(d.Name))
                    byName[d.Name] = d;
            }

            // Build nodes
            var nodeList = new List<AsmDefNodeModel>(data.Count);
            var nodesById = new Dictionary<string, AsmDefNodeModel>(StringComparer.Ordinal);
            foreach (var d in data)
            {
                var node = new AsmDefNodeModel(d.StableId, d.Name, d.Origin, d.AssetPath);
                nodeList.Add(node);
                nodesById[node.Id] = node;
            }
            nodeList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            // Build edges and missing references
            var edgeList        = new List<AsmDefEdgeModel>();
            var missingList     = new List<MissingReferenceInfo>();
            var outRaw          = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var inRaw           = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var selfRefIds      = new HashSet<string>(StringComparer.Ordinal);

            foreach (var d in data)
            {
                var sourceId = d.StableId;
                foreach (var rawRef in d.References)
                {
                    if (string.IsNullOrEmpty(rawRef)) continue;

                    AsmDefData resolved = null;
                    string reason = null;

                    if (rawRef.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                    {
                        var guid = rawRef.Substring(5);
                        if (!byGuid.TryGetValue(guid, out resolved))
                            reason = "guid not found";
                    }
                    else
                    {
                        if (!byName.TryGetValue(rawRef, out resolved))
                            reason = "name not found";
                    }

                    if (resolved == null)
                    {
                        missingList.Add(new MissingReferenceInfo(sourceId, rawRef, reason));
                        continue;
                    }

                    var targetId = resolved.StableId;

                    // Drop self-references from edges but record them for cycle detection
                    if (string.Equals(targetId, sourceId, StringComparison.Ordinal))
                    {
                        selfRefIds.Add(sourceId);
                        continue;
                    }

                    edgeList.Add(new AsmDefEdgeModel(sourceId, targetId));

                    if (!outRaw.TryGetValue(sourceId, out var outList))
                        outRaw[sourceId] = outList = new List<string>();
                    outList.Add(targetId);

                    if (!inRaw.TryGetValue(targetId, out var inList))
                        inRaw[targetId] = inList = new List<string>();
                    inList.Add(sourceId);
                }
            }

            edgeList.Sort((a, b) =>
            {
                var c = string.Compare(a.SourceId, b.SourceId, StringComparison.Ordinal);
                return c != 0 ? c : string.Compare(a.TargetId, b.TargetId, StringComparison.Ordinal);
            });

            missingList.Sort((a, b) =>
            {
                var c = string.Compare(a.SourceId, b.SourceId, StringComparison.Ordinal);
                return c != 0 ? c : string.Compare(a.MissingReference, b.MissingReference, StringComparison.Ordinal);
            });

            // Wrap adjacency lists as read-only
            var outgoing = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var kv in outRaw) outgoing[kv.Key] = new ReadOnlyCollection<string>(kv.Value);

            var incoming = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var kv in inRaw) incoming[kv.Key] = new ReadOnlyCollection<string>(kv.Value);

            return new DependencyGraphModel(
                new ReadOnlyCollection<AsmDefNodeModel>(nodeList),
                new ReadOnlyCollection<AsmDefEdgeModel>(edgeList),
                new ReadOnlyCollection<MissingReferenceInfo>(missingList),
                new ReadOnlyDictionary<string, AsmDefNodeModel>(nodesById),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(outgoing),
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(incoming),
                selfRefIds);
        }
    }
}
