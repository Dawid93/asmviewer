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
    /// <summary>Main editor window for Assembly Architect.</summary>
    internal sealed class AssemblyArchitectWindow : EditorWindow
    {
        private const string UxmlPath = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectWindow.uxml";
        private const string UssPath  = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectWindow.uss";
        private const string IconPath = "Packages/com.ddev.assembly-architect/Editor/Resources/Icons/window-icon.png";

        // ── Serialized state ──────────────────────────────────────────────────

        [SerializeField] private string lastSelectedNodeId;
        [SerializeField] internal bool showPackages;
        [SerializeField] internal bool showBuiltIns;
        [SerializeField] internal bool showMiniMap = true;

        // ── Runtime state ─────────────────────────────────────────────────────

        private AsmDefRepository              _repo;
        private AsmDefGraphView               _graphView;
        private AssemblyArchitectToolbar      _toolbar;
        private DependencyGraphModel          _model;
        private LayoutKind                    _currentLayout = LayoutKind.Hierarchical;
        private readonly Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();
        private Debouncer                     _rebuildDebouncer;

        // ── Menu ─────────────────────────────────────────────────────────────

        [MenuItem("Window/Analysis/Assembly Architect")]
        public static void Open()
        {
            var window = GetWindow<AssemblyArchitectWindow>();
            window.titleContent = BuildTitleContent();
            window.minSize      = new Vector2(900f, 600f);
            window.Show();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            titleContent = BuildTitleContent();

            _repo = new AsmDefRepository();
            _repo.Changed += ScheduleRebuild;
            _rebuildDebouncer = new Debouncer(100, Rebuild);

            EditorApplication.delayCall += Rebuild;
        }

        private void OnDisable()
        {
            if (_toolbar != null)
                _toolbar.SaveState(this);

            if (_repo != null)
                _repo.Changed -= ScheduleRebuild;

            _rebuildDebouncer?.Dispose();
            _rebuildDebouncer = null;
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void CreateGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[AssemblyArchitect] UXML not found at {UxmlPath}");
                return;
            }
            uxml.CloneTree(rootVisualElement);

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null)
                rootVisualElement.styleSheets.Add(uss);

            var graphHost = rootVisualElement.Q<VisualElement>("graph-host");
            graphHost?.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");

            // Graph view
            _graphView = new AsmDefGraphView { name = "asmdef-graph" };
            _graphView.style.flexGrow = 1;
            graphHost?.Add(_graphView);

            WireGraphViewEvents();

            // Toolbar
            var toolbarHost = rootVisualElement.Q<VisualElement>("toolbar");
            _toolbar = new AssemblyArchitectToolbar();
            _toolbar.LoadState(this);
            toolbarHost?.Add(_toolbar);

            WireToolbarEvents();
        }

        // ── Rebuild pipeline ──────────────────────────────────────────────────

        private void ScheduleRebuild() => _rebuildDebouncer?.Bump();

        private void Rebuild()
        {
            if (_graphView == null) return;

            // Save viewport and selection
            var viewPos   = _graphView.viewTransform.position;
            var viewScale = _graphView.viewTransform.scale;
            var prevSelected = lastSelectedNodeId;

            var data = _repo?.LoadAll() ?? (IReadOnlyList<AsmDefData>)System.Array.Empty<AsmDefData>();
            _model = DependencyGraphModel.Build(data);

            // Compute layout — only fill missing positions
            IGraphLayout layout = _currentLayout == LayoutKind.Hierarchical
                ? (IGraphLayout)new HierarchicalLayout()
                : new ForceDirectedLayout();

            var fresh = layout.Compute(_model, new LayoutOptions());
            foreach (var kv in fresh)
            {
                if (!_positions.ContainsKey(kv.Key))
                    _positions[kv.Key] = kv.Value;
            }

            _graphView.Populate(_model, _positions);

            // Restore viewport
            _graphView.UpdateViewTransform(viewPos, viewScale);

            // Update status bar
            var cycles = CycleDetector.FindCycles(_model);
            var status = rootVisualElement?.Q<Label>("status-label");
            if (status != null)
                status.text = $"{_model.Nodes.Count} asmdefs · {_model.Edges.Count} references · {_model.MissingReferences.Count} missing · {cycles.Count} cycles";
        }

        // ── Event wiring ──────────────────────────────────────────────────────

        private void WireGraphViewEvents()
        {
            _graphView.NodeSelected       += id => { lastSelectedNodeId = id; /* TODO Task 4.4 */ };
            _graphView.EdgeAddRequested   += (src, tgt) => { /* TODO Task 4.1 */ };
            _graphView.EdgeRemoveRequested += (src, tgt) => { /* TODO Task 4.2 */ };
            _graphView.NodePositionChanged += (id, pos) => _positions[id] = pos;
        }

        private void WireToolbarEvents()
        {
            if (_toolbar == null) return;

            _toolbar.RefreshRequested    += Rebuild;
            _toolbar.LayoutRequested     += kind =>
            {
                _currentLayout = kind;
                _positions.Clear();
                Rebuild();
            };
            _toolbar.SaveLayoutRequested   += () => { /* TODO Task 5.4 */ };
            _toolbar.SearchChanged         += query => { /* TODO Task 5.2 */ };
            _toolbar.ShowPackagesChanged   += show => { showPackages = show; Rebuild(); };
            _toolbar.ShowBuiltInsChanged   += show => { showBuiltIns = show; Rebuild(); };
            _toolbar.MiniMapToggled        += show => { showMiniMap = show; /* TODO Task 5.3 */ };
            _toolbar.OpenSettingsRequested += () => SettingsService.OpenProjectSettings("Project/Assembly Architect");
            _toolbar.OpenDocsRequested     += () => Application.OpenURL("https://github.com");
            _toolbar.ResetLayoutRequested  += () => { _positions.Clear(); Rebuild(); };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GUIContent BuildTitleContent()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            return icon != null
                ? new GUIContent("Assembly Architect", icon)
                : new GUIContent("Assembly Architect");
        }
    }
}
