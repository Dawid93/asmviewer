using AssemblyArchitect.Editor.Core.Layout;
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

        private AssemblyArchitectToolbar toolbar;
        private TwoPaneSplitView splitView;
        private VisualElement inspectorHost;
        private SerializedObject serializedWindowState;
        private SerializedProperty inspectorPaneDimensionProperty;

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
        }

        private void OnDisable()
        {
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

            RestoreInspectorPaneDimension();
            AttachToolbar();

            splitView?.RegisterCallback<GeometryChangedEvent>(OnSplitGeometryChanged);
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
            // Implemented in Task 3.4.
        }

        private void OnLayoutRequested(LayoutKind kind)
        {
            toolbarLayoutKind = kind;
            // Implemented in Task 3.4.
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
            // Implemented in Task 5.2.
        }

        private void OnShowBuiltInsChanged(bool value)
        {
            showBuiltIns = value;
            // Implemented in Task 5.2.
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

        private sealed class AssemblyArchitectWindowState : ScriptableObject
        {
#pragma warning disable 0414
            [SerializeField] private float _inspectorPaneDimension = 320f;
#pragma warning restore 0414
        }
    }
}
