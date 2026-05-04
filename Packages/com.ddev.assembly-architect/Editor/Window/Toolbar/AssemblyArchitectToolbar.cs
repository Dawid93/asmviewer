using System;
using AssemblyArchitect.Editor.Core.Layout;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window.Toolbar
{
    internal readonly struct AssemblyArchitectToolbarState
    {
        public AssemblyArchitectToolbarState(
            LayoutKind layoutKind,
            bool showPackages,
            bool showBuiltIns,
            bool miniMapVisible,
            string searchText)
        {
            LayoutKind = layoutKind;
            ShowPackages = showPackages;
            ShowBuiltIns = showBuiltIns;
            MiniMapVisible = miniMapVisible;
            SearchText = searchText ?? string.Empty;
        }

        public LayoutKind LayoutKind { get; }
        public bool ShowPackages { get; }
        public bool ShowBuiltIns { get; }
        public bool MiniMapVisible { get; }
        public string SearchText { get; }
    }

    internal sealed class AssemblyArchitectToolbar : VisualElement
    {
        private const string UxmlPath = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectToolbar.uxml";
        private const string UssPath = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectToolbar.uss";

        private static readonly string RefreshIconName = "Refresh";
        private static readonly string SaveIconName = "SaveAs";
        private static readonly string PackageIconName = "Package Manager";
        private static readonly string BuiltInIconName = "d_FilterByType";
        private static readonly string MiniMapIconName = "d_SceneViewVisibility";

        private readonly ToolbarMenu layoutMenu;
        private readonly ToolbarSearchField searchField;
        private readonly ToolbarToggle showPackagesToggle;
        private readonly ToolbarToggle showBuiltInsToggle;
        private readonly ToolbarToggle miniMapToggle;
        private readonly ToolbarMenu overflowMenu;

        private LayoutKind layoutKind = LayoutKind.Hierarchical;

        public AssemblyArchitectToolbar()
        {
            AddToClassList("aa-toolbar-root");

            CloneToolbarUxml();
            ApplyToolbarStyles();

            var containers = EnsureToolbarContainers();
            var leftControls = containers.left;
            var rightControls = containers.right;

            var refreshButton = CreateIconButton(RefreshIconName, "Refresh", () => RefreshRequested?.Invoke());
            layoutMenu = CreateLayoutMenu();
            var saveLayoutButton = CreateIconButton(SaveIconName, "Save Layout", () => SaveLayoutRequested?.Invoke());

            searchField = new ToolbarSearchField { name = "search-field", tooltip = "Search assemblies" };
            searchField.AddToClassList("aa-toolbar-search");
            searchField.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue ?? string.Empty));

            showPackagesToggle = CreateToggle(PackageIconName, "Show Packages", false, value => ShowPackagesChanged?.Invoke(value));
            showBuiltInsToggle = CreateToggle(BuiltInIconName, "Show Built-ins", false, value => ShowBuiltInsChanged?.Invoke(value));
            miniMapToggle = CreateToggle(MiniMapIconName, "Mini-map", true, value => MiniMapToggled?.Invoke(value));
            overflowMenu = CreateOverflowMenu();

            leftControls?.Add(refreshButton);
            leftControls?.Add(layoutMenu);
            leftControls?.Add(saveLayoutButton);

            rightControls?.Add(searchField);
            rightControls?.Add(showPackagesToggle);
            rightControls?.Add(showBuiltInsToggle);
            rightControls?.Add(miniMapToggle);
            rightControls?.Add(overflowMenu);
        }

        public event Action RefreshRequested;
        public event Action<LayoutKind> LayoutRequested;
        public event Action SaveLayoutRequested;
        public event Action<string> SearchChanged;
        public event Action<bool> ShowPackagesChanged;
        public event Action<bool> ShowBuiltInsChanged;
        public event Action<bool> MiniMapToggled;
        public event Action OpenProjectSettingsRequested;
        public event Action OpenDocumentationRequested;
        public event Action ResetLayoutRequested;

        public void LoadState(AssemblyArchitectToolbarState state)
        {
            layoutKind = state.LayoutKind;
            UpdateLayoutMenuText();
            searchField.SetValueWithoutNotify(state.SearchText);
            showPackagesToggle.SetValueWithoutNotify(state.ShowPackages);
            showBuiltInsToggle.SetValueWithoutNotify(state.ShowBuiltIns);
            miniMapToggle.SetValueWithoutNotify(state.MiniMapVisible);
        }

        public AssemblyArchitectToolbarState SaveState()
        {
            return new AssemblyArchitectToolbarState(
                layoutKind,
                showPackagesToggle.value,
                showBuiltInsToggle.value,
                miniMapToggle.value,
                searchField.value);
        }

        private void CloneToolbarUxml()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[AssemblyArchitect] Missing toolbar UXML at {UxmlPath}.");
                return;
            }

            visualTree.CloneTree(this);
        }

        private void ApplyToolbarStyles()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet == null)
            {
                Debug.LogError($"[AssemblyArchitect] Missing toolbar USS at {UssPath}.");
                return;
            }

            styleSheets.Add(styleSheet);
        }

        private (VisualElement left, VisualElement right) EnsureToolbarContainers()
        {
            var leftControls = this.Q<VisualElement>("left-controls");
            var rightControls = this.Q<VisualElement>("right-controls");
            if (leftControls != null && rightControls != null)
                return (leftControls, rightControls);

            Clear();

            var toolbarElement = new UnityEditor.UIElements.Toolbar
            {
                name = "assembly-architect-toolbar",
            };
            toolbarElement.AddToClassList("aa-toolbar");

            leftControls = new VisualElement { name = "left-controls" };
            leftControls.AddToClassList("aa-toolbar-group");

            var spacer = new VisualElement { name = "toolbar-spacer" };
            spacer.AddToClassList("aa-toolbar-spacer");

            rightControls = new VisualElement { name = "right-controls" };
            rightControls.AddToClassList("aa-toolbar-group");

            toolbarElement.Add(leftControls);
            toolbarElement.Add(spacer);
            toolbarElement.Add(rightControls);
            Add(toolbarElement);

            return (leftControls, rightControls);
        }

        private ToolbarButton CreateIconButton(string iconName, string tooltip, Action clicked)
        {
            var button = new ToolbarButton(clicked)
            {
                tooltip = tooltip,
            };

            button.AddToClassList("aa-icon-button");
            AddIcon(button, iconName);
            return button;
        }

        private ToolbarMenu CreateLayoutMenu()
        {
            var menu = new ToolbarMenu
            {
                name = "layout-menu",
                tooltip = "Layout",
            };

            menu.AddToClassList("aa-layout-menu");
            menu.menu.AppendAction(
                "Hierarchical",
                _ => RequestLayout(LayoutKind.Hierarchical),
                _ => layoutKind == LayoutKind.Hierarchical ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.menu.AppendAction(
                "Force Directed",
                _ => RequestLayout(LayoutKind.ForceDirected),
                _ => layoutKind == LayoutKind.ForceDirected ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            UpdateLayoutMenuText(menu);
            return menu;
        }

        private ToolbarToggle CreateToggle(string iconName, string tooltip, bool defaultValue, Action<bool> changed)
        {
            var toggle = new ToolbarToggle
            {
                tooltip = tooltip,
                value = defaultValue,
            };

            toggle.AddToClassList("aa-icon-toggle");
            AddIcon(toggle, iconName);
            toggle.RegisterValueChangedCallback(evt => changed?.Invoke(evt.newValue));
            return toggle;
        }

        private ToolbarMenu CreateOverflowMenu()
        {
            var menu = new ToolbarMenu
            {
                name = "overflow-menu",
                text = "...",
                tooltip = "More",
            };

            menu.AddToClassList("aa-overflow-menu");
            menu.menu.AppendAction("Open Project Settings", _ => OpenProjectSettingsRequested?.Invoke());
            menu.menu.AppendAction("Open Documentation", _ => OpenDocumentationRequested?.Invoke());
            menu.menu.AppendAction("Reset Layout", _ => ResetLayoutRequested?.Invoke());
            return menu;
        }

        private void RequestLayout(LayoutKind kind)
        {
            layoutKind = kind;
            UpdateLayoutMenuText();
            LayoutRequested?.Invoke(kind);
        }

        private void UpdateLayoutMenuText()
        {
            UpdateLayoutMenuText(layoutMenu);
        }

        private void UpdateLayoutMenuText(ToolbarMenu menu)
        {
            if (menu == null)
                return;

            menu.text = layoutKind == LayoutKind.Hierarchical ? "Hierarchical" : "Force Directed";
        }

        private static void AddIcon(VisualElement parent, string iconName)
        {
            var content = EditorGUIUtility.IconContent(iconName);
            var image = content?.image;
            if (image == null)
                return;

            var icon = new Image
            {
                image = image,
                pickingMode = PickingMode.Ignore,
            };
            icon.AddToClassList("aa-toolbar-icon");
            parent.Add(icon);
        }
    }
}
