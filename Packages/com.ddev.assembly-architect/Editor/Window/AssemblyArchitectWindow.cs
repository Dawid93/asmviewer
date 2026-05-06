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

        [SerializeField] private string lastSelectedNodeId;

        private AssemblyArchitectToolbar.AssemblyArchitectToolbar _toolbar;

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
        }

        private void OnDisable()
        {
            if (_toolbar != null)
                _toolbar.SaveState(this);
        }

        private void CreateGUI()
        {
            // Load and clone UXML
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[AssemblyArchitect] UXML not found at {UxmlPath}");
                return;
            }
            uxml.CloneTree(rootVisualElement);

            // Apply USS
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null)
                rootVisualElement.styleSheets.Add(uss);

            // Skin class on graph host
            var graphHost = rootVisualElement.Q<VisualElement>("graph-host");
            graphHost?.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");

            // Toolbar
            var toolbarHost = rootVisualElement.Q<VisualElement>("toolbar");
            _toolbar = new AssemblyArchitectToolbar.AssemblyArchitectToolbar();
            _toolbar.LoadState(this);
            toolbarHost?.Add(_toolbar);

            WireToolbarStubs();
        }

        // ── Toolbar stubs ─────────────────────────────────────────────────────

        private void WireToolbarStubs()
        {
            if (_toolbar == null) return;

            _toolbar.RefreshRequested      += () => { /* TODO Task 3.4 */ };
            _toolbar.LayoutRequested       += kind => { /* TODO Task 3.4 */ };
            _toolbar.SaveLayoutRequested   += () => { /* TODO Task 5.4 */ };
            _toolbar.SearchChanged         += query => { /* TODO Task 5.2 */ };
            _toolbar.ShowPackagesChanged   += show => { /* TODO Task 5.2 */ };
            _toolbar.ShowBuiltInsChanged   += show => { /* TODO Task 5.2 */ };
            _toolbar.MiniMapToggled        += show => { /* TODO Task 5.3 */ };
            _toolbar.OpenSettingsRequested += () => SettingsService.OpenProjectSettings("Project/Assembly Architect");
            _toolbar.OpenDocsRequested     += () => Application.OpenURL("https://github.com");
            _toolbar.ResetLayoutRequested  += () => { /* TODO Task 5.4 */ };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GUIContent BuildTitleContent()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            return icon != null
                ? new GUIContent("Assembly Architect", icon)
                : new GUIContent("Assembly Architect");
        }

        // ── State helpers (used by toolbar) ──────────────────────────────────

        [SerializeField] internal bool showPackages;
        [SerializeField] internal bool showBuiltIns;
        [SerializeField] internal bool showMiniMap = true;
    }
}
