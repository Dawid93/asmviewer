using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyArchitect.Editor.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Graph
{
    internal sealed class AsmDefGraphView : GraphView
    {
        private const string UssPath = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefGraphView.uss";
        private static readonly Vector2 DefaultNodeSize = new Vector2(220f, 80f);

        private readonly Dictionary<string, AsmDefNode> nodesById = new Dictionary<string, AsmDefNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> pendingPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Debouncer positionDebouncer;

        public AsmDefGraphView()
        {
            AddToClassList("aa-asmdef-graph-view");

            this.AddManipulator(new ContentZoomer { minScale = 0.25f, maxScale = 2f });
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<MouseUpEvent>(_ => NotifySelectionChanged());
            RegisterCallback<KeyUpEvent>(_ => NotifySelectionChanged());
            positionDebouncer = new Debouncer(500, FlushPendingPositions);
        }

        public event Action<string> NodeSelected;
        public event Action<string, string> EdgeAddRequested;
        public event Action<string, string> EdgeRemoveRequested;
        public event Action<string, Vector2> NodePositionChanged;

        public void Populate(
            DependencyGraphModel model,
            IReadOnlyDictionary<string, Vector2> positions)
        {
            Clear();

            if (model == null)
                model = DependencyGraphModel.Empty;

            positions = positions ?? new Dictionary<string, Vector2>();
            var brokenNodeIds = new HashSet<string>(
                model.MissingReferences.Select(reference => reference.SourceId),
                StringComparer.Ordinal);

            for (var index = 0; index < model.Nodes.Count; index++)
            {
                var nodeModel = model.Nodes[index];
                var node = new AsmDefNode(nodeModel);
                var position = positions.TryGetValue(nodeModel.Id, out var storedPosition)
                    ? storedPosition
                    : new Vector2(50f * index, 50f * index);

                node.SetPosition(new Rect(position, DefaultNodeSize));
                if (brokenNodeIds.Contains(nodeModel.Id))
                    node.ApplyState(NodeVisualState.Broken);

                nodesById[nodeModel.Id] = node;
                AddElement(node);
            }

            foreach (var edgeModel in model.Edges)
            {
                if (!nodesById.TryGetValue(edgeModel.SourceId, out var sourceNode) ||
                    !nodesById.TryGetValue(edgeModel.TargetId, out var targetNode))
                {
                    continue;
                }

                var edge = new AsmDefEdge
                {
                    output = sourceNode.OutputPort,
                    input = targetNode.InputPort,
                };
                edge.output.Connect(edge);
                edge.input.Connect(edge);
                AddElement(edge);
            }

            foreach (var node in nodesById.Values)
            {
                node.RefreshExpandedState();
                node.RefreshPorts();
            }
        }

        public new void Clear()
        {
            pendingPositions.Clear();
            nodesById.Clear();

            var elements = graphElements.ToList();
            foreach (var element in elements)
                RemoveElement(element);
        }

        public void SelectNode(string id)
        {
            ClearSelection();

            if (string.IsNullOrEmpty(id))
            {
                NodeSelected?.Invoke(string.Empty);
                return;
            }

            if (nodesById.TryGetValue(id, out var node))
                AddToSelection(node);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            var compatible = new List<Port>();
            foreach (var port in ports.ToList())
            {
                if (port == startPort)
                    continue;
                if (port.node == startPort.node)
                    continue;
                if (port.direction == startPort.direction)
                    continue;

                compatible.Add(port);
            }

            return compatible;
        }

        private void NotifySelectionChanged()
        {
            var selectedNodes = selection.OfType<AsmDefNode>().ToList();
            NodeSelected?.Invoke(selectedNodes.Count == 1 ? selectedNodes[0].AsmDefId : string.Empty);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                foreach (var edge in change.edgesToCreate)
                    EmitEdgeAdd(edge);

                change.edgesToCreate = null;
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                var filtered = new List<GraphElement>(change.elementsToRemove.Count);
                foreach (var element in change.elementsToRemove)
                {
                    if (element is AsmDefEdge edge)
                    {
                        EmitEdgeRemove(edge);
                        continue;
                    }

                    if (element is AsmDefNode)
                        continue;

                    filtered.Add(element);
                }

                change.elementsToRemove = filtered;
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                foreach (var node in change.movedElements.OfType<AsmDefNode>())
                {
                    pendingPositions[node.AsmDefId] = node.GetPosition().position;
                    positionDebouncer.Bump();
                }
            }

            return change;
        }

        private void EmitEdgeAdd(Edge edge)
        {
            if (!(edge.output?.node is AsmDefNode sourceNode) ||
                !(edge.input?.node is AsmDefNode targetNode))
            {
                return;
            }

            EdgeAddRequested?.Invoke(sourceNode.AsmDefId, targetNode.AsmDefId);
        }

        private void EmitEdgeRemove(AsmDefEdge edge)
        {
            if (!(edge.output?.node is AsmDefNode sourceNode) ||
                !(edge.input?.node is AsmDefNode targetNode))
            {
                return;
            }

            EdgeRemoveRequested?.Invoke(sourceNode.AsmDefId, targetNode.AsmDefId);
        }

        private void FlushPendingPositions()
        {
            if (pendingPositions.Count == 0)
                return;

            var snapshot = new Dictionary<string, Vector2>(pendingPositions, StringComparer.Ordinal);
            pendingPositions.Clear();

            foreach (var entry in snapshot)
                NodePositionChanged?.Invoke(entry.Key, entry.Value);
        }
    }
}
