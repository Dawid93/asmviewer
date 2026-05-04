using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Settings;
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
        private readonly List<AsmDefEdge> asmDefEdges = new List<AsmDefEdge>();
        private readonly HashSet<string> brokenNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> cycleNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> cycleEdgeKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> originHiddenNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> searchFilteredNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> pendingPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Debouncer positionDebouncer;
        private readonly MiniMap miniMap;
        private GraphFilter filter;
        private Vector2 lastContextGraphPosition;
        private bool skipNextRemoveConfirmation;

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

            miniMap = new MiniMap { anchored = true };
            miniMap.AddToClassList("aa-minimap");
            miniMap.SetPosition(new Rect(15f, 15f, 200f, 160f));
            Add(miniMap);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<MouseUpEvent>(_ => NotifySelectionChanged());
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(_ => NotifySelectionChanged());
            positionDebouncer = new Debouncer(500, FlushPendingPositions);
            AssemblyArchitectSettings.Changed += RefreshSettings;
            RegisterCallback<DetachFromPanelEvent>(_ => AssemblyArchitectSettings.Changed -= RefreshSettings);
        }

        public event Action<string> NodeSelected;
        public event Action<string, string> EdgeAddRequested;
        public event Action<string, string, bool> EdgeRemoveRequested;
        public event Action<string, Vector2> NodePositionChanged;
        public event Action<Vector2, string, Rect> CreateAsmDefRequested;

        public void Populate(
            DependencyGraphModel model,
            IReadOnlyDictionary<string, Vector2> positions)
        {
            Clear();

            if (model == null)
                model = DependencyGraphModel.Empty;

            positions = positions ?? new Dictionary<string, Vector2>();
            brokenNodeIds.UnionWith(model.MissingReferences.Select(reference => reference.SourceId));

            for (var index = 0; index < model.Nodes.Count; index++)
            {
                var nodeModel = model.Nodes[index];
                var node = new AsmDefNode(nodeModel);
                var position = positions.TryGetValue(nodeModel.Id, out var storedPosition)
                    ? storedPosition
                    : new Vector2(50f * index, 50f * index);

                node.SetPosition(new Rect(position, DefaultNodeSize));
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
                    SourceId = edgeModel.SourceId,
                    TargetId = edgeModel.TargetId,
                    output = sourceNode.OutputPort,
                    input = targetNode.InputPort,
                };
                edge.output.Connect(edge);
                edge.input.Connect(edge);
                asmDefEdges.Add(edge);
                AddElement(edge);
            }

            foreach (var node in nodesById.Values)
            {
                node.RefreshExpandedState();
                node.RefreshPorts();
            }

            ApplyVisualStates();
        }

        public new void Clear()
        {
            pendingPositions.Clear();
            nodesById.Clear();
            asmDefEdges.Clear();
            brokenNodeIds.Clear();
            cycleNodeIds.Clear();
            cycleEdgeKeys.Clear();
            originHiddenNodeIds.Clear();
            searchFilteredNodeIds.Clear();

            var elements = graphElements
                .Where(element => element is AsmDefNode || element is AsmDefEdge)
                .ToList();
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

        internal AsmDefNode GetNodeById(string id)
        {
            nodesById.TryGetValue(id ?? string.Empty, out var node);
            return node;
        }

        public void ApplyCycleHighlight(IReadOnlyList<IReadOnlyList<string>> cycles)
        {
            cycleNodeIds.Clear();
            cycleEdgeKeys.Clear();

            if (cycles != null)
            {
                foreach (var cycle in cycles)
                {
                    var members = new HashSet<string>(cycle, StringComparer.Ordinal);
                    foreach (var id in members)
                        cycleNodeIds.Add(id);

                    foreach (var edge in asmDefEdges)
                    {
                        if (members.Contains(edge.SourceId) && members.Contains(edge.TargetId))
                            cycleEdgeKeys.Add(GetEdgeKey(edge.SourceId, edge.TargetId));
                    }
                }
            }

            ApplyVisualStates();
        }

        public void ApplyFilter(GraphFilter filter)
        {
            this.filter = filter;
            ApplyVisualStates();
        }

        public void SetMiniMapVisible(bool visible)
        {
            miniMap.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var target = evt.target as VisualElement;
            var node = target?.GetFirstAncestorOfType<AsmDefNode>();
            if (node != null)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Show in Project", _ => PingAsset(node.AssetPath));
                evt.menu.AppendAction("Open .asmdef in External Editor", _ => OpenAsset(node.AssetPath));
                evt.menu.AppendAction(
                    "Delete Assembly Definition",
                    _ => Debug.LogWarning("[AssemblyArchitect] Delete Assembly Definition is not implemented yet."));
                return;
            }

            lastContextGraphPosition = ToGraphPosition(evt.localMousePosition);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction(
                "Create Assembly Definition...",
                _ => CreateAsmDefRequested?.Invoke(
                    lastContextGraphPosition,
                    GetSingleSelectedNodeId(),
                    new Rect(GUIUtility.GUIToScreenPoint(evt.mousePosition), Vector2.zero)));
        }

        private void NotifySelectionChanged()
        {
            var selectedNodes = selection.OfType<AsmDefNode>().ToList();
            NodeSelected?.Invoke(selectedNodes.Count == 1 ? selectedNodes[0].AsmDefId : string.Empty);
        }

        private void ApplyVisualStates()
        {
            originHiddenNodeIds.Clear();
            searchFilteredNodeIds.Clear();

            foreach (var pair in nodesById)
            {
                var node = pair.Value;
                var originVisible = PassesOriginFilter(node.Origin);
                var searchVisible = PassesSearchFilter(node.SearchName);
                node.style.display = originVisible ? DisplayStyle.Flex : DisplayStyle.None;

                if (!originVisible)
                    originHiddenNodeIds.Add(pair.Key);
                else if (!searchVisible)
                    searchFilteredNodeIds.Add(pair.Key);

                var state = NodeVisualState.None;
                if (cycleNodeIds.Contains(pair.Key))
                    state |= NodeVisualState.InCycle;
                if (brokenNodeIds.Contains(pair.Key))
                    state |= NodeVisualState.Broken;
                if (originVisible && !searchVisible)
                    state |= NodeVisualState.Filtered;

                node.ApplyState(state);
            }

            foreach (var edge in asmDefEdges)
            {
                var hidden = originHiddenNodeIds.Contains(edge.SourceId) || originHiddenNodeIds.Contains(edge.TargetId);
                edge.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;

                var state = EdgeVisualState.None;
                if (cycleEdgeKeys.Contains(GetEdgeKey(edge.SourceId, edge.TargetId)))
                    state |= EdgeVisualState.InCycle;
                if (!hidden && (searchFilteredNodeIds.Contains(edge.SourceId) || searchFilteredNodeIds.Contains(edge.TargetId)))
                    state |= EdgeVisualState.Filtered;
                edge.ApplyState(state);
            }

            var selectedHidden = selection.OfType<AsmDefNode>().Any(node => originHiddenNodeIds.Contains(node.AsmDefId));
            if (selectedHidden)
            {
                ClearSelection();
                NodeSelected?.Invoke(string.Empty);
            }
        }

        private bool PassesOriginFilter(AsmDefOrigin origin)
        {
            if (origin == AsmDefOrigin.ProjectAssets)
                return true;
            if ((origin == AsmDefOrigin.EmbeddedPackage || origin == AsmDefOrigin.RegistryPackage) && filter.ShowPackages)
                return true;
            if (origin == AsmDefOrigin.BuiltIn && filter.ShowBuiltIns)
                return true;
            return false;
        }

        private bool PassesSearchFilter(string searchName)
        {
            return string.IsNullOrEmpty(filter.SearchQuery) ||
                   (searchName ?? string.Empty).Contains(filter.SearchQuery);
        }

        private void RefreshSettings()
        {
            foreach (var node in nodesById.Values)
                node.RefreshSettings();
            foreach (var edge in asmDefEdges)
                edge.RefreshSettings();
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
                skipNextRemoveConfirmation = false;
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

            EdgeRemoveRequested?.Invoke(sourceNode.AsmDefId, targetNode.AsmDefId, skipNextRemoveConfirmation);
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

        private Vector2 ToGraphPosition(Vector2 localPosition)
        {
            return this.ChangeCoordinatesTo(contentViewContainer, localPosition);
        }

        private string GetSingleSelectedNodeId()
        {
            var selectedNodes = selection.OfType<AsmDefNode>().ToList();
            return selectedNodes.Count == 1 ? selectedNodes[0].AsmDefId : string.Empty;
        }

        private static void PingAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        private static void OpenAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                skipNextRemoveConfirmation = evt.shiftKey;
        }

        private static string GetEdgeKey(string sourceId, string targetId)
        {
            return (sourceId ?? string.Empty) + "\n" + (targetId ?? string.Empty);
        }
    }
}
