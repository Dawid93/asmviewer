using System.Collections.Generic;
using System.Linq;
using AssemblyArchitect.Editor.Commands;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Core.Layout;
using AssemblyArchitect.Editor.Graph;
using AssemblyArchitect.Editor.Infrastructure;
using AssemblyArchitect.Editor.Settings;
using AssemblyArchitect.Editor.Window.Dialogs;
using AssemblyArchitect.Editor.Window.Inspector;
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
        private AsmDefInspectorPanel inspector;
        private CycleBanner cycleBanner;
        private AddReferenceCommand addReferenceCommand;
        private RemoveReferenceCommand removeReferenceCommand;
        private CreateAsmDefCommand createAsmDefCommand;
        private LayoutCache layoutCache;
        private LayoutCacheData layoutCacheData = new LayoutCacheData();
        private DependencyGraphModel model = DependencyGraphModel.Empty;
        private LayoutKind currentLayout = LayoutKind.Hierarchical;
        private GraphFilter filter;
        private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>(System.StringComparer.Ordinal);
        private readonly HashSet<string> userPositionIds = new HashSet<string>(System.StringComparer.Ordinal);

        private TwoPaneSplitView splitView;
        private VisualElement inspectorHost;
        private Label statusLabel;
        private SerializedObject serializedWindowState;
        private SerializedProperty inspectorPaneDimensionProperty;
        private Debouncer rebuildDebouncer;
        private Debouncer layoutSaveDebouncer;
        private Debouncer statusResetDebouncer;
        private bool cacheViewportRestored;
        private bool suppressLayoutAutoSave;

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
            filter = new GraphFilter((searchText ?? string.Empty).ToLowerInvariant(), showPackages, showBuiltIns);
            layoutCache = new LayoutCache();
            layoutCacheData = layoutCache.Load();
            positions.Clear();
            userPositionIds.Clear();
            foreach (var entry in layoutCacheData.positions ?? System.Array.Empty<PositionEntry>())
            {
                positions[entry.id] = new Vector2(entry.x, entry.y);
                userPositionIds.Add(entry.id);
            }
            cacheViewportRestored = false;

            repo = AsmDefRepository.Default;
            var writer = new AsmDefWriter();
            addReferenceCommand = new AddReferenceCommand(repo, writer);
            removeReferenceCommand = new RemoveReferenceCommand(repo, writer);
            createAsmDefCommand = new CreateAsmDefCommand(repo, writer, SetPendingPosition);
            repo.Changed += ScheduleRebuild;
            rebuildDebouncer = new Debouncer(100, Rebuild);
            layoutSaveDebouncer = new Debouncer(500, () => SaveLayout(false));
            statusResetDebouncer = new Debouncer(2000, UpdateStatus);
            EditorApplication.delayCall += Rebuild;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= Rebuild;
            if (repo != null)
                repo.Changed -= ScheduleRebuild;

            rebuildDebouncer?.Cancel();
            layoutSaveDebouncer?.Cancel();
            statusResetDebouncer?.Cancel();
            SaveLayout(false);
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
            AttachCycleBanner();
            AttachInspector();

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
            graphView.CreateAsmDefRequested += OnCreateAsmDefRequested;
            graphView.SetMiniMapVisible(miniMapVisible);
            graphView.ApplyFilter(filter);
            graphView.viewTransformChanged += _ => ScheduleLayoutSave();
        }

        private void AttachCycleBanner()
        {
            var root = rootVisualElement.Q<VisualElement>("root");
            if (root == null || graphView == null)
                return;

            cycleBanner = new CycleBanner(graphView);
            root.Insert(1, cycleBanner);
        }

        private void AttachInspector()
        {
            if (inspectorHost == null)
                return;

            inspectorHost.Clear();
            inspector = new AsmDefInspectorPanel(repo, new AsmDefWriter());
            inspectorHost.Add(inspector);
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
            SaveLayout(true);
            SetTemporaryStatus("Layout saved.");
        }

        private void OnSearchChanged(string value)
        {
            searchText = value;
            UpdateFilter(new GraphFilter((value ?? string.Empty).ToLowerInvariant(), filter.ShowPackages, filter.ShowBuiltIns));
        }

        private void OnShowPackagesChanged(bool value)
        {
            showPackages = value;
            UpdateFilter(new GraphFilter(filter.SearchQuery, value, filter.ShowBuiltIns));
        }

        private void OnShowBuiltInsChanged(bool value)
        {
            showBuiltIns = value;
            UpdateFilter(new GraphFilter(filter.SearchQuery, filter.ShowPackages, value));
        }

        private void OnMiniMapToggled(bool value)
        {
            miniMapVisible = value;
            graphView?.SetMiniMapVisible(value);
        }

        private void OnOpenProjectSettingsRequested()
        {
            SettingsService.OpenProjectSettings("Project/Assembly Architect");
        }

        private void OnOpenDocumentationRequested()
        {
            // Implemented in Task 7.2.
        }

        private void OnResetLayoutRequested()
        {
            layoutSaveDebouncer?.Cancel();
            layoutCache?.Clear();
            positions.Clear();
            userPositionIds.Clear();
            suppressLayoutAutoSave = true;
            Rebuild();
            suppressLayoutAutoSave = false;
            SetTemporaryStatus("Layout reset.");
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
            var cycles = CycleDetector.FindCycles(model);

            PruneStalePositions();
            MergeFreshLayoutPositions();

            graphView.Populate(model, positions);
            if (!cacheViewportRestored)
            {
                graphView.UpdateViewTransform(layoutCacheData.viewportPosition, Vector3.one * Mathf.Max(0.1f, layoutCacheData.viewportScale));
                cacheViewportRestored = true;
            }
            else
            {
                graphView.UpdateViewTransform(viewPosition, viewScale);
            }
            graphView.ApplyCycleHighlight(cycles);
            graphView.ApplyFilter(filter);
            graphView.SetMiniMapVisible(miniMapVisible);
            cycleBanner?.Update(cycles);
            graphView.SelectNode(selectedNodeId);

            UpdateStatus();
            ScheduleLayoutSave();
        }

        private void MergeFreshLayoutPositions()
        {
            var layout = currentLayout == LayoutKind.Hierarchical
                ? (IGraphLayout)new HierarchicalLayout()
                : new ForceDirectedLayout();
            var fresh = layout.Compute(model, new LayoutOptions
            {
                Iterations = AssemblyArchitectSettings.instance.ForceLayoutIterations,
            });

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

        private void UpdateFilter(GraphFilter nextFilter)
        {
            filter = nextFilter;
            graphView?.ApplyFilter(filter);
            if (!string.IsNullOrEmpty(lastSelectedNodeId) &&
                graphView?.GetNodeById(lastSelectedNodeId)?.style.display == DisplayStyle.None)
            {
                OnNodeSelected(string.Empty);
            }
        }

        private void OnNodeSelected(string nodeId)
        {
            lastSelectedNodeId = nodeId ?? string.Empty;
            inspector?.ShowFor(lastSelectedNodeId);
        }

        private void OnEdgeAddRequested(string sourceId, string targetId)
        {
            addReferenceCommand?.Execute(sourceId, targetId);
        }

        private void OnEdgeRemoveRequested(string sourceId, string targetId, bool skipConfirmation)
        {
            removeReferenceCommand?.Execute(sourceId, targetId, skipConfirmation);
        }

        private void OnNodePositionChanged(string id, Vector2 position)
        {
            if (string.IsNullOrEmpty(id))
                return;

            positions[id] = position;
            userPositionIds.Add(id);
            ScheduleLayoutSave();
        }

        private void OnCreateAsmDefRequested(Vector2 graphPosition, string autoReferenceSourceId, Rect activatorRect)
        {
            CreateAsmDefPopup.Show(
                activatorRect,
                repo,
                createAsmDefCommand,
                graphPosition,
                autoReferenceSourceId);
        }

        private void SetPendingPosition(string id, Vector2 position)
        {
            if (string.IsNullOrEmpty(id))
                return;

            positions[id] = position;
            userPositionIds.Add(id);
            ScheduleLayoutSave();
        }

        private void ScheduleLayoutSave()
        {
            if (!suppressLayoutAutoSave)
                layoutSaveDebouncer?.Bump();
        }

        private void SaveLayout(bool immediate)
        {
            if (layoutCache == null || graphView == null)
                return;

#pragma warning disable 0618
            var viewportPosition = graphView.viewTransform.position;
            var viewportScale = graphView.viewTransform.scale.x;
#pragma warning restore 0618

            var data = new LayoutCacheData
            {
                viewportPosition = viewportPosition,
                viewportScale = viewportScale,
                positions = positions
                    .Select(entry => new PositionEntry { id = entry.Key, x = entry.Value.x, y = entry.Value.y })
                    .ToArray(),
            };
            layoutCache.Save(data);
            layoutCacheData = data;
        }

        private void SetTemporaryStatus(string message)
        {
            if (statusLabel == null)
                return;

            statusLabel.text = message;
            statusResetDebouncer?.Bump();
        }

        private sealed class AssemblyArchitectWindowState : ScriptableObject
        {
#pragma warning disable 0414
            [SerializeField] private float _inspectorPaneDimension = 320f;
#pragma warning restore 0414
        }
    }
}
