using System;
using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Graph
{
    /// <summary>GraphView that displays the assembly dependency graph.</summary>
    internal sealed class AsmDefGraphView : GraphView
    {
        private const string UssPath = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefGraphView.uss";
        private static readonly Vector2 DefaultNodeSize = new Vector2(220f, 80f);

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when a single node is selected. Emits the node's StableId, or "" when selection is cleared.</summary>
        public event Action<string> NodeSelected;

        /// <summary>Fired when the user drags a new edge. Does not add an edge to the graph — command layer handles it.</summary>
        public event Action<string, string> EdgeAddRequested;

        /// <summary>
        /// Fired when the user deletes an edge. Third argument is <c>true</c> when Shift is held (skip confirmation).
        /// Does not remove the edge — command layer handles it.
        /// </summary>
        public event Action<string, string, bool> EdgeRemoveRequested;

        /// <summary>Fired after a node has been still for ~500 ms following a drag.</summary>
        public event Action<string, Vector2> NodePositionChanged;

        // ── Internal state ────────────────────────────────────────────────────

        private readonly Dictionary<string, AsmDefNode> _nodeElements = new Dictionary<string, AsmDefNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _pendingPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private Debouncer _positionDebouncer;

        // ── Constructor ───────────────────────────────────────────────────────

        public AsmDefGraphView()
        {
            this.AddManipulator(new ContentZoomer { minScale = 0.25f, maxScale = 2f });
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) styleSheets.Add(uss);

            _positionDebouncer = new Debouncer(500, FlushPendingPositions);

            graphViewChanged = OnGraphViewChanged;
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifySelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifySelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            string id = string.Empty;
            if (selection.Count == 1 && selection[0] is AsmDefNode n)
                id = n.AsmDefId;
            NodeSelected?.Invoke(id);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Clears the graph and re-populates it from <paramref name="model"/> using the given <paramref name="positions"/>.</summary>
        public void Populate(DependencyGraphModel model, IReadOnlyDictionary<string, Vector2> positions)
        {
            Clear();
            _nodeElements.Clear();

            int index = 0;
            foreach (var nodeModel in model.Nodes)
            {
                Vector2 pos = positions != null && positions.TryGetValue(nodeModel.Id, out var p)
                    ? p
                    : new Vector2(50f + index * 50f, 50f + index * 50f);

                var node = new AsmDefNode(nodeModel);
                node.SetPosition(new Rect(pos, DefaultNodeSize));
                AddElement(node);
                _nodeElements[nodeModel.Id] = node;
                index++;
            }

            foreach (var edgeModel in model.Edges)
            {
                if (!_nodeElements.TryGetValue(edgeModel.SourceId, out var srcNode)) continue;
                if (!_nodeElements.TryGetValue(edgeModel.TargetId, out var tgtNode)) continue;

                var edge = new AsmDefEdge
                {
                    output = srcNode.OutputPort,
                    input  = tgtNode.InputPort,
                };
                edge.input.Connect(edge);
                edge.output.Connect(edge);
                AddElement(edge);
            }
        }

        /// <summary>Removes all elements from the graph.</summary>
        public new void Clear()
        {
            DeleteElements(graphElements.ToList());
            _nodeElements.Clear();
        }

        // ── Compatibility ─────────────────────────────────────────────────────

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            var compatible = new List<Port>();
            foreach (var port in ports)
            {
                if (port == startPort) continue;
                if (port.node == startPort.node) continue;
                if (port.direction == startPort.direction) continue;
                compatible.Add(port);
            }
            return compatible;
        }

        // ── Graph view change ─────────────────────────────────────────────────

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // Edges to create — forward as events, don't let GraphView add them
            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var srcNode = edge.output?.node as AsmDefNode;
                    var tgtNode = edge.input?.node as AsmDefNode;
                    if (srcNode != null && tgtNode != null)
                        EdgeAddRequested?.Invoke(srcNode.AsmDefId, tgtNode.AsmDefId);
                }
                change.edgesToCreate.Clear();
            }

            // Elements to remove — intercept AsmDefEdge removals
            if (change.elementsToRemove != null)
            {
                bool shift = Event.current?.shift ?? false;
                for (int i = change.elementsToRemove.Count - 1; i >= 0; i--)
                {
                    if (change.elementsToRemove[i] is AsmDefEdge ae)
                    {
                        var srcNode = ae.output?.node as AsmDefNode;
                        var tgtNode = ae.input?.node as AsmDefNode;
                        if (srcNode != null && tgtNode != null)
                            EdgeRemoveRequested?.Invoke(srcNode.AsmDefId, tgtNode.AsmDefId, shift);
                        change.elementsToRemove.RemoveAt(i);
                    }
                }
            }

            // Moved elements — debounce position updates
            if (change.movedElements != null)
            {
                foreach (var el in change.movedElements)
                {
                    if (el is AsmDefNode node)
                        _pendingPositions[node.AsmDefId] = node.GetPosition().position;
                }
                _positionDebouncer.Bump();
            }

            return change;
        }

        // ── Position flush ────────────────────────────────────────────────────

        private void FlushPendingPositions()
        {
            foreach (var kv in _pendingPositions)
                NodePositionChanged?.Invoke(kv.Key, kv.Value);
            _pendingPositions.Clear();
        }
    }
}
