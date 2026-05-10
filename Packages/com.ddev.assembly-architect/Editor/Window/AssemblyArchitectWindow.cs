using System.Collections.Generic;
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
        private AddReferenceCommand           _addRefCmd;
        private RemoveReferenceCommand        _removeRefCmd;
        private CreateAsmDefCommand           _createAsmDefCmd;
        private AsmDefInspectorPanel          _inspector;
        private CycleBanner                   _cycleBanner;
        private GraphFilter                   _filter;
        private LayoutCache                   _cache;
        private LayoutCacheData               _cacheData;
        private Debouncer                     _saveDebouncer;
        private bool                          _viewportRestored;

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

            var writer       = new AsmDefWriter();
            _addRefCmd       = new AddReferenceCommand(_repo, writer);
            _removeRefCmd    = new RemoveReferenceCommand(_repo, writer);
            _createAsmDefCmd = new CreateAsmDefCommand(_repo, writer);

            // Load layout cache and seed positions so first rebuild uses saved layout
            _cache            = new LayoutCache();
            _cacheData        = _cache.Load();
            _viewportRestored = false;
            foreach (var entry in _cacheData.Positions)
                _positions[entry.Id] = new Vector2(entry.X, entry.Y);

            _saveDebouncer = new Debouncer(500, SaveLayout);

            AssemblyArchitectSettings.Changed += ScheduleRebuild;

            // Filter state is initialized in CreateGUI after toolbar loads persisted toggles
            EditorApplication.delayCall += Rebuild;
        }

        private void OnDisable()
        {
            if (_toolbar != null)
                _toolbar.SaveState(this);

            if (_repo != null)
                _repo.Changed -= ScheduleRebuild;

            AssemblyArchitectSettings.Changed -= ScheduleRebuild;

            _rebuildDebouncer?.Dispose();
            _rebuildDebouncer = null;
            _saveDebouncer?.Dispose();
            _saveDebouncer = null;
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

            // Inspector panel
            var inspectorHost = rootVisualElement.Q<VisualElement>("inspector-host");
            if (inspectorHost != null)
            {
                _inspector = new AsmDefInspectorPanel(_repo, new AsmDefWriter());
                _inspector.style.flexGrow = 1;
                inspectorHost.Add(_inspector);
            }

            // Search provider (Spacebar shortcut)
            var searchProvider = AsmDefSearchProvider.Create(screenPos =>
            {
                var graphPos = _graphView.GetViewportCenter();
                OpenCreatePopup(graphPos, screenPos);
            });
            _graphView.SetSearchProvider(searchProvider);

            // Toolbar
            var toolbarHost = rootVisualElement.Q<VisualElement>("toolbar");
            _toolbar = new AssemblyArchitectToolbar();
            _toolbar.LoadState(this);
            toolbarHost?.Add(_toolbar);

            WireToolbarEvents();

            // Initialize filter from persisted toggle state (toolbar has already called LoadState)
            _filter = new GraphFilter(string.Empty, showPackages, showBuiltIns);

            // Apply persisted mini-map visibility
            _graphView.SetMiniMapVisible(showMiniMap);

            // Cycle banner — inserted between toolbar and body
            _cycleBanner = new CycleBanner(_graphView);
            var toolbarEl = rootVisualElement.Q<VisualElement>("toolbar");
            toolbarEl?.parent?.Insert(1, _cycleBanner);
        }

        // ── Position seeding ──────────────────────────────────────────────────

        /// <summary>
        /// Pre-seeds a graph position for a node whose <see cref="AsmDefData.StableId"/>
        /// is known but whose graph element hasn't been created yet (e.g. after file creation).
        /// </summary>
        public void SetPendingPosition(string id, Vector2 pos) => _positions[id] = pos;

        // ── Popup factory ─────────────────────────────────────────────────────

        private void OpenCreatePopup(Vector2 graphPos, Vector2 screenPos)
        {
            CreateAsmDefPopup.Show(
                new Rect(screenPos, Vector2.zero),
                _createAsmDefCmd,
                _repo,
                graphPos,
                lastSelectedNodeId,
                SetPendingPosition
            );
        }

        // ── Rebuild pipeline ──────────────────────────────────────────────────

        private void ScheduleRebuild() => _rebuildDebouncer?.Bump();

        private void Rebuild()
        {
            if (_graphView == null) return;

            // Save viewport and selection
            var cc        = _graphView.contentViewContainer;
            var t         = cc.resolvedStyle.translate;
            var s         = cc.resolvedStyle.scale.value;
            var viewPos   = new Vector3(t.x, t.y, 0f);
            var viewScale = new Vector3(s.x, s.y, 1f);

            var data = _repo?.LoadAll() ?? (System.Collections.Generic.IReadOnlyList<AsmDefData>)System.Array.Empty<AsmDefData>();
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

            // Restore viewport — use cache on first rebuild, saved state on subsequent ones
            if (!_viewportRestored && _cacheData != null)
            {
                _viewportRestored = true;
                var vp = _cacheData.ViewportPosition;
                var vs = _cacheData.ViewportScale;
                _graphView.UpdateViewTransform(
                    new Vector3(vp.x, vp.y, 0f),
                    new Vector3(vs, vs, 1f));
            }
            else
            {
                _graphView.UpdateViewTransform(viewPos, viewScale);
            }

            // Schedule auto-save after each rebuild
            _saveDebouncer?.Bump();

            // Cycle highlight + banner
            var cycles = CycleDetector.FindCycles(_model);
            _graphView.ApplyCycleHighlight(cycles);
            _cycleBanner?.Update(cycles);

            // Apply active filter (preserves cycle highlight state)
            _graphView.ApplyFilter(_filter);

            // Update status bar
            var status = rootVisualElement?.Q<Label>("status-label");
            if (status != null)
                status.text = $"{_model.Nodes.Count} asmdefs · {_model.Edges.Count} references · {_model.MissingReferences.Count} missing · {cycles.Count} cycles";
        }

        // ── Event wiring ──────────────────────────────────────────────────────

        private void WireGraphViewEvents()
        {
            _graphView.NodeSelected        += id =>
            {
                lastSelectedNodeId = id;
                _inspector?.ShowFor(id);
            };
            // EdgeAddRequested fires (outputNode, inputNode). With the provider→consumer arrow
            // convention, outputNode is the referenced assembly and inputNode is the referencing one,
            // so the referencing assembly (tgt/inputNode) should receive outputNode (src) as its reference.
            _graphView.EdgeAddRequested    += (src, tgt) => _addRefCmd.Execute(tgt, src);
            _graphView.EdgeRemoveRequested += (src, tgt, force) => _removeRefCmd.Execute(tgt, src, force);
            _graphView.NodePositionChanged += (id, pos) =>
            {
                _positions[id] = pos;
                _saveDebouncer?.Bump();
            };
            _graphView.viewTransformChanged += _ => _saveDebouncer?.Bump();

            _graphView.CreateAsmDefRequested += (graphPos, screenPos) => OpenCreatePopup(graphPos, screenPos);

            _graphView.NodePingRequested += id =>
            {
                var asset = FindAsmDefAsset(id);
                if (asset != null) EditorGUIUtility.PingObject(asset);
            };

            _graphView.NodeOpenInEditorRequested += id =>
            {
                var asset = FindAsmDefAsset(id);
                if (asset != null) AssetDatabase.OpenAsset(asset);
            };
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
                // After a layout switch the new positions are centred around (0,0) while
                // the old viewport may be panned far away.  Schedule FrameAll on the next
                // editor tick (after Unity resolves element positions) so the user sees the
                // freshly laid-out graph immediately without having to scroll to find it.
                _graphView?.schedule.Execute(() => _graphView.FrameAll());
            };
            _toolbar.SaveLayoutRequested   += () =>
            {
                SaveLayout();
                var status = rootVisualElement?.Q<Label>("status-label");
                if (status != null)
                {
                    var prev = status.text;
                    status.text = "Layout saved.";
                    rootVisualElement.schedule.Execute(() => status.text = prev).StartingIn(2000);
                }
            };
            _toolbar.SearchChanged         += query => UpdateFilter(new GraphFilter(
                (query ?? "").ToLowerInvariant(), _filter.ShowPackages, _filter.ShowBuiltIns));
            _toolbar.ShowPackagesChanged   += show =>
            {
                showPackages = show;
                UpdateFilter(new GraphFilter(_filter.SearchQuery, show, _filter.ShowBuiltIns));
            };
            _toolbar.ShowBuiltInsChanged   += show =>
            {
                showBuiltIns = show;
                UpdateFilter(new GraphFilter(_filter.SearchQuery, _filter.ShowPackages, show));
            };
            _toolbar.MiniMapToggled        += show => { showMiniMap = show; _graphView?.SetMiniMapVisible(show); };
            _toolbar.OpenSettingsRequested += () => SettingsService.OpenProjectSettings("Project/Assembly Architect");
            _toolbar.OpenDocsRequested     += () => Application.OpenURL("https://github.com");
            _toolbar.ResetLayoutRequested  += () =>
            {
                _cache?.Delete();
                _cacheData        = new LayoutCacheData();
                _viewportRestored = false;
                _positions.Clear();
                Rebuild();
            };
        }

        // ── Layout cache ──────────────────────────────────────────────────────

        private void SaveLayout()
        {
            if (_graphView == null || _cache == null) return;

            var cc = _graphView.contentViewContainer;
            var t  = cc.resolvedStyle.translate;
            var s  = cc.resolvedStyle.scale.value;

            var entries = new PositionEntry[_positions.Count];
            int i = 0;
            foreach (var kv in _positions)
                entries[i++] = new PositionEntry { Id = kv.Key, X = kv.Value.x, Y = kv.Value.y };

            _cache.Save(new LayoutCacheData
            {
                ViewportPosition = new Vector2(t.x, t.y),
                ViewportScale    = s.x,
                Positions        = entries,
            });
        }

        // ── Filter ────────────────────────────────────────────────────────────

        private void UpdateFilter(GraphFilter filter)
        {
            _filter = filter;
            _graphView?.ApplyFilter(_filter);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private UnityEditorInternal.AssemblyDefinitionAsset FindAsmDefAsset(string stableId)
        {
            if (string.IsNullOrEmpty(stableId) || _repo == null) return null;

            // Try by GUID first
            var data = _repo.FindByGuid(stableId);
            if (data == null)
            {
                // Fall back to name lookup
                foreach (var d in _repo.LoadAll())
                    if (d.StableId == stableId) { data = d; break; }
            }

            if (data == null || string.IsNullOrEmpty(data.AssetPath)) return null;
            return AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(data.AssetPath);
        }

        private static GUIContent BuildTitleContent()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            return icon != null
                ? new GUIContent("Assembly Architect", icon)
                : new GUIContent("Assembly Architect");
        }
    }
}
