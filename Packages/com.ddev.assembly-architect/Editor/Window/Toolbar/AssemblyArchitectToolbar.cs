using System;
using AssemblyArchitect.Editor.Core.Layout;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window.Toolbar
{
    /// <summary>Toolbar for the Assembly Architect window. Exposes events for all controls.</summary>
    internal sealed class AssemblyArchitectToolbar : VisualElement
    {
        private const string UxmlPath = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectToolbar.uxml";
        private const string UssPath  = "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectToolbar.uss";

        // ── Icon names ────────────────────────────────────────────────────────
        private static readonly string IconRefresh    = "Refresh";
        private static readonly string IconSaveLayout = "d_SaveAs";

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when the Refresh button is clicked.</summary>
        public event Action RefreshRequested;

        /// <summary>Fired when the user selects a layout algorithm.</summary>
        public event Action<LayoutKind> LayoutRequested;

        /// <summary>Fired when the Save Layout button is clicked.</summary>
        public event Action SaveLayoutRequested;

        /// <summary>Fired when the search query changes.</summary>
        public event Action<string> SearchChanged;

        /// <summary>Fired when the Show Packages toggle changes.</summary>
        public event Action<bool> ShowPackagesChanged;

        /// <summary>Fired when the Show Built-ins toggle changes.</summary>
        public event Action<bool> ShowBuiltInsChanged;

        /// <summary>Fired when the Mini-map toggle changes.</summary>
        public event Action<bool> MiniMapToggled;

        /// <summary>Fired when "Open Project Settings" is selected from the overflow menu.</summary>
        public event Action OpenSettingsRequested;

        /// <summary>Fired when "Open Documentation" is selected from the overflow menu.</summary>
        public event Action OpenDocsRequested;

        /// <summary>Fired when "Reset Layout" is selected from the overflow menu.</summary>
        public event Action ResetLayoutRequested;

        // ── Child references ──────────────────────────────────────────────────

        private ToolbarToggle _togglePackages;
        private ToolbarToggle _toggleBuiltIns;
        private ToolbarToggle _toggleMiniMap;

        // ── Constructor ───────────────────────────────────────────────────────

        public AssemblyArchitectToolbar()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml != null) uxml.CloneTree(this);

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) styleSheets.Add(uss);

            BindControls();
        }

        // ── State persistence ─────────────────────────────────────────────────

        /// <summary>Restores toggle states from serialized window fields.</summary>
        public void LoadState(AssemblyArchitectWindow window)
        {
            if (_togglePackages != null) _togglePackages.SetValueWithoutNotify(window.showPackages);
            if (_toggleBuiltIns != null) _toggleBuiltIns.SetValueWithoutNotify(window.showBuiltIns);
            if (_toggleMiniMap  != null) _toggleMiniMap.SetValueWithoutNotify(window.showMiniMap);
        }

        /// <summary>Writes current toggle states back to the window for serialization.</summary>
        public void SaveState(AssemblyArchitectWindow window)
        {
            if (_togglePackages != null) window.showPackages = _togglePackages.value;
            if (_toggleBuiltIns != null) window.showBuiltIns = _toggleBuiltIns.value;
            if (_toggleMiniMap  != null) window.showMiniMap  = _toggleMiniMap.value;
        }

        // ── Binding ───────────────────────────────────────────────────────────

        private void BindControls()
        {
            // Refresh
            var btnRefresh = this.Q<ToolbarButton>("btn-refresh");
            if (btnRefresh != null)
            {
                btnRefresh.iconImage = EditorGUIUtility.IconContent(IconRefresh).image as UnityEngine.Texture2D;
                btnRefresh.clicked += () => RefreshRequested?.Invoke();
            }

            // Layout dropdown
            var menuLayout = this.Q<ToolbarMenu>("menu-layout");
            if (menuLayout != null)
            {
                menuLayout.menu.AppendAction("Hierarchical",  _ => LayoutRequested?.Invoke(LayoutKind.Hierarchical));
                menuLayout.menu.AppendAction("Force Directed", _ => LayoutRequested?.Invoke(LayoutKind.ForceDirected));
            }

            // Save Layout
            var btnSave = this.Q<ToolbarButton>("btn-save-layout");
            if (btnSave != null)
            {
                btnSave.iconImage = EditorGUIUtility.IconContent(IconSaveLayout).image as UnityEngine.Texture2D;
                btnSave.clicked += () => SaveLayoutRequested?.Invoke();
            }

            // Search field
            var search = this.Q<ToolbarSearchField>("search-field");
            if (search != null)
                search.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue));

            // Toggles (defaults: packages=false, builtins=false, minimap=true)
            _togglePackages = this.Q<ToolbarToggle>("toggle-packages");
            if (_togglePackages != null)
            {
                _togglePackages.SetValueWithoutNotify(false);
                _togglePackages.RegisterValueChangedCallback(evt => ShowPackagesChanged?.Invoke(evt.newValue));
            }

            _toggleBuiltIns = this.Q<ToolbarToggle>("toggle-builtins");
            if (_toggleBuiltIns != null)
            {
                _toggleBuiltIns.SetValueWithoutNotify(false);
                _toggleBuiltIns.RegisterValueChangedCallback(evt => ShowBuiltInsChanged?.Invoke(evt.newValue));
            }

            _toggleMiniMap = this.Q<ToolbarToggle>("toggle-minimap");
            if (_toggleMiniMap != null)
            {
                _toggleMiniMap.SetValueWithoutNotify(true);
                _toggleMiniMap.RegisterValueChangedCallback(evt => MiniMapToggled?.Invoke(evt.newValue));
            }

            // Overflow menu
            var menuOverflow = this.Q<ToolbarMenu>("menu-overflow");
            if (menuOverflow != null)
            {
                menuOverflow.menu.AppendAction("Open Project Settings", _ => OpenSettingsRequested?.Invoke());
                menuOverflow.menu.AppendAction("Open Documentation",    _ => OpenDocsRequested?.Invoke());
                menuOverflow.menu.AppendSeparator();
                menuOverflow.menu.AppendAction("Reset Layout",          _ => ResetLayoutRequested?.Invoke());
            }
        }
    }
}
