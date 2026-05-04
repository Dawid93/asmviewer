using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Core.Layout;
using AssemblyArchitect.Editor.Graph;
using AssemblyArchitect.Editor.Infrastructure;
using AssemblyArchitect.Editor.Window.Toolbar;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window
{
    internal sealed class AssemblyArchitectWindow : EditorWindow
    {
        private const string WindowTitle = "Assembly Architect";
        private const string UxmlPath = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectWindow.uxml";
        private const string UssPath = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectWindow.uss";
        private const string WindowIconResourcePath = "Icons/window-icon";
        private const string SplitDimensionPropertyName = "_inspectorPaneDimension";

        [SerializeField] private string lastSelectedNodeId;
        [SerializeField] private AssemblyArchitectWindowState windowState;
        [SerializeField] private LayoutKind toolbarLayoutKind = LayoutKind.Hierarchical;
        [SerializeField] private bool showPackages;
        [SerializeField] private bool showBuiltIns;
        [SerializeField] private bool miniMapVisible = true;
        [SerializeField] private string searchText = string.Empty;

        private AsmDefRepository repo;
        private AsmDefGraphView graphView;
        private AssemblyArchitectToolbar toolbar;
        private DependencyGraphModel model = DependencyGraphModel.Empty;
        private LayoutKind currentLayout = LayoutKind.Hierarchical;
        private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>(System.StringComparer.Ordinal);
        private readonly HashSet<string> userPositionIds = new HashSet<string>(System.StringComparer.Ordinal);

        private TwoPaneSplitView splitView;
        private VisualElement inspectorHost;
        private Label statusLabel;
        private SerializedObject serializedWindowState;
        private SerializedProperty inspectorPaneDimensionProperty;
        private Debouncer rebuildDebouncer;

        [MenuItem("Window/Analysis/Assembly Architect")]
        public static void Open()
        {
            var window = GetWindow<AssemblyArchitectWindow>();
            window.titleContent = new GUIContent(WindowTitle, Resources.Load<Texture2D>(WindowIconResourcePath));
            window.minSize = new Vector2(900f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle, Resources.Load<Texture2D>(WindowIconResourcePath));
            minSize = new Vector2(900f, 600f);
            EnsureWindowState();

            currentLayout = toolbarLayoutKind;
            repo = AsmDefRepository.Default;
            repo.Changed += ScheduleRebuild;
            rebuildDebouncer = new Debouncer(100, Rebuild);
            EditorApplication.delayCall += Rebuild;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= Rebuild;
            if (repo != null)
                repo.Changed -= ScheduleRebuild;

            rebuildDebouncer?.Cancel();
            SaveToolbarState();
            StoreInspectorPaneDimension();
            serializedWindowState?.ApplyModifiedPropertiesWithoutUndo();
        }

        private void CreateGUI()
        {
            EnsureWindowState();

            rootVisualElement.Clear();
            CloneWindowUxml();
            ApplyWindowStyles();
            ApplySkinClass();

            splitView = rootVisualElement.Q<TwoPaneSplitView>("main-split-view");
            inspectorHost = rootVisualElement.Q<VisualElement>("inspector-host");
            statusLabel = rootVisualElement.Q<Label>("status-label");

            RestoreInspectorPaneDimension();
            AttachToolbar();
            AttachGraphView();

            splitView?.RegisterCallback<GeometryChangedEvent>(OnSplitGeometryChanged);
            Rebuild();
        }

        private void CloneWindowUxml()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[AssemblyArchitect] Missing window UXML at {UxmlPath}.");
                return;
            }

            visualTree.CloneTree(rootVisualElement);
        }

        private void ApplyWindowStyles()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet == null)
            {
                Debug.LogError($"[AssemblyArchitect] Missing window USS at {UssPath}.");
                return;
            }

            rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void ApplySkinClass()
        {
            var root = rootVisualElement.Q<VisualElement>("root") ?? rootVisualElement;
            root.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("light", !EditorGUIUtility.isProSkin);
        }

        private void AttachToolbar()
        {
            var toolbarHost = rootVisualElement.Q<VisualElement>("toolbar");
            if (toolbarHost == null)
                return;

            toolbarHost.Clear();
            toolbar = new AssemblyArchitectToolbar();
            toolbarHost.Add(toolbar);

            toolbar.LoadState(new AssemblyArchitectToolbarState(
                toolbarLayoutKind,
                showPackages,
                showBuiltIns,
                miniMapVisible,
                searchText));

            toolbar.RefreshRequested += OnRefreshRequested;
            toolbar.LayoutRequested += OnLayoutRequested;
            toolbar.SaveLayoutRequested += OnSaveLayoutRequested;
            toolbar.SearchChanged += OnSearchChanged;
            toolbar.ShowPackagesChanged += OnShowPackagesChanged;
            toolbar.ShowBuiltInsChanged += OnShowBuiltInsChanged;
            toolbar.MiniMapToggled += OnMiniMapToggled;
            toolbar.OpenProjectSettingsRequested += OnOpenProjectSettingsRequested;
            toolbar.OpenDocumentationRequested += OnOpenDocumentationRequested;
            toolbar.ResetLayoutRequested += OnResetLayoutRequested;
        }

        private void AttachGraphView()
        {
            var graphHost = rootVisualElement.Q<VisualElement>("graph-host");
            if (graphHost == null)
                return;

            graphHost.Clear();
            graphView = new AsmDefGraphView { name = "asmdef-graph" };
            graphView.style.flexGrow = 1f;
            graphHost.Add(graphView);

            graphView.NodeSelected += OnNodeSelected;
            graphView.EdgeAddRequested += OnEdgeAddRequested;
            graphView.EdgeRemoveRequested += OnEdgeRemoveRequested;
            graphView.NodePositionChanged += OnNodePositionChanged;
        }

        private void SaveToolbarState()
        {
            if (toolbar == null)
                return;

            var state = toolbar.SaveState();
            toolbarLayoutKind = state.LayoutKind;
            showPackages = state.ShowPackages;
            showBuiltIns = state.ShowBuiltIns;
            miniMapVisible = state.MiniMapVisible;
            searchText = state.SearchText;
        }

        private void EnsureWindowState()
        {
            if (windowState == null)
            {
                windowState = CreateInstance<AssemblyArchitectWindowState>();
                windowState.hideFlags = HideFlags.HideAndDontSave;
            }

            if (serializedWindowState == null || serializedWindowState.targetObject == null)
            {
                serializedWindowState = new SerializedObject(windowState);
                inspectorPaneDimensionProperty = serializedWindowState.FindProperty(SplitDimensionPropertyName);
            }
        }

        private void RestoreInspectorPaneDimension()
        {
            if (splitView == null || inspectorPaneDimensionProperty == null)
                return;

            serializedWindowState.Update();
            var dimension = inspectorPaneDimensionProperty.floatValue;
            if (dimension > 0f)
                splitView.fixedPaneInitialDimension = dimension;
        }

        private void StoreInspectorPaneDimension()
        {
            if (inspectorHost == null || inspectorPaneDimensionProperty == null)
                return;

            var width = inspectorHost.resolvedStyle.width;
            if (width <= 0f)
                return;

            serializedWindowState.Update();
            inspectorPaneDimensionProperty.floatValue = width;
            serializedWindowState.ApplyModifiedPropertiesWithoutUndo();
        }

        private void OnSplitGeometryChanged(GeometryChangedEvent evt)
        {
            StoreInspectorPaneDimension();
        }

        private void OnRefreshRequested()
        {
            Rebuild();
        }

        private void OnLayoutRequested(LayoutKind kind)
        {
            toolbarLayoutKind = kind;
            currentLayout = kind;
            KeepOnlyUserMovedPositions();
            Rebuild();
        }

        private void OnSaveLayoutRequested()
        {
            // Implemented in Task 5.4.
        }

        private void OnSearchChanged(string value)
        {
            searchText = value;
            // Implemented in Task 5.2.
        }

        private void OnShowPackagesChanged(bool value)
        {
            showPackages = value;
            Rebuild();
        }

        private void OnShowBuiltInsChanged(bool value)
        {
            showBuiltIns = value;
            Rebuild();
        }

        private void OnMiniMapToggled(bool value)
        {
            miniMapVisible = value;
            // Implemented in Task 5.3.
        }

        private void OnOpenProjectSettingsRequested()
        {
            // Implemented in Task 5.5.
        }

        private void OnOpenDocumentationRequested()
        {
            // Implemented in Task 7.2.
        }

        private void OnResetLayoutRequested()
        {
            // Implemented in Task 5.4.
        }

        private void ScheduleRebuild()
        {
            rebuildDebouncer?.Bump();
        }

        private void Rebuild()
        {
            if (graphView == null)
                return;

#pragma warning disable 0618
            var viewPosition = graphView.viewTransform.position;
            var viewScale = graphView.viewTransform.scale;
#pragma warning restore 0618
            var selectedNodeId = lastSelectedNodeId;

            var data = repo != null ? repo.LoadAll() : System.Array.Empty<AsmDefData>();
            model = DependencyGraphModel.Build(data);

            PruneStalePositions();
            MergeFreshLayoutPositions();

            graphView.Populate(model, positions);
            graphView.UpdateViewTransform(viewPosition, viewScale);
            graphView.SelectNode(selectedNodeId);

            UpdateStatus();
        }

        private void MergeFreshLayoutPositions()
        {
            var layout = currentLayout == LayoutKind.Hierarchical
                ? (IGraphLayout)new HierarchicalLayout()
                : new ForceDirectedLayout();
            var fresh = layout.Compute(model, new LayoutOptions());

            foreach (var entry in fresh)
            {
                if (!positions.ContainsKey(entry.Key))
                    positions[entry.Key] = entry.Value;
            }
        }

        private void PruneStalePositions()
        {
            var validIds = new HashSet<string>(model.NodesById.Keys, System.StringComparer.Ordinal);
            var staleIds = new List<string>();

            foreach (var id in positions.Keys)
                if (!validIds.Contains(id))
                    staleIds.Add(id);

            foreach (var id in staleIds)
            {
                positions.Remove(id);
                userPositionIds.Remove(id);
            }
        }

        private void KeepOnlyUserMovedPositions()
        {
            var staleIds = new List<string>();
            foreach (var id in positions.Keys)
                if (!userPositionIds.Contains(id))
                    staleIds.Add(id);

            foreach (var id in staleIds)
                positions.Remove(id);
        }

        private void UpdateStatus()
        {
            if (statusLabel == null || model == null)
                return;

            var cycleCount = CycleDetector.FindCycles(model).Count;
            statusLabel.text = $"{model.Nodes.Count} asmdefs · {model.Edges.Count} references · {model.MissingReferences.Count} missing · {cycleCount} cycles";
        }

        private void OnNodeSelected(string nodeId)
        {
            lastSelectedNodeId = nodeId ?? string.Empty;
            // Inspector panel arrives in Task 4.4.
        }

        private void OnEdgeAddRequested(string sourceId, string targetId)
        {
            // AddReferenceCommand arrives in Task 4.1.
        }

        private void OnEdgeRemoveRequested(string sourceId, string targetId)
        {
            // RemoveReferenceCommand arrives in Task 4.2.
        }

        private void OnNodePositionChanged(string id, Vector2 position)
        {
            if (string.IsNullOrEmpty(id))
                return;

            positions[id] = position;
            userPositionIds.Add(id);
        }

        private sealed class AssemblyArchitectWindowState : ScriptableObject
        {
#pragma warning disable 0414
            [SerializeField] private float _inspectorPaneDimension = 320f;
#pragma warning restore 0414
        }
    }
}
