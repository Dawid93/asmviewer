using System;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Settings;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Graph
{
    [Flags]
    internal enum NodeVisualState
    {
        None = 0,
        InCycle = 1 << 0,
        Broken = 1 << 1,
        Filtered = 1 << 2,
    }

    internal sealed class AsmDefNode : Node
    {
        private const string UssPath = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefNode.uss";
        private readonly VisualElement originIcon;
        private NodeVisualState visualState;

        public AsmDefNode(AsmDefNodeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            AsmDefId = model.Id;
            AssetPath = model.AssetPath;
            Origin = model.Origin;
            DisplayName = model.Name ?? string.Empty;
            SearchName = DisplayName.ToLowerInvariant();
            title = model.Name;
            userData = model.Id;

            AddToClassList("aa-asmdef-node");
            AddToClassList(GetOriginClass(model.Origin));
            AddStyleSheet();

            titleButtonContainer.Clear();
            originIcon = AddOriginIcon();
            AddSubtitle(GetOriginLabel(model.Origin));
            ApplySettingsColors();

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            InputPort.AddToClassList("aa-node-port");
            inputContainer.Add(InputPort);

            OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = string.Empty;
            OutputPort.AddToClassList("aa-node-port");
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }

        public string AsmDefId { get; }
        public string AssetPath { get; }
        public AsmDefOrigin Origin { get; }
        public string DisplayName { get; }
        public string SearchName { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        public void ApplyState(NodeVisualState state)
        {
            visualState = state;
            EnableInClassList("aa-state-cycle", (state & NodeVisualState.InCycle) != 0);
            EnableInClassList("aa-state-broken", (state & NodeVisualState.Broken) != 0);
            EnableInClassList("aa-state-filtered", (state & NodeVisualState.Filtered) != 0);
            ApplySettingsColors();
        }

        public void RefreshSettings()
        {
            ApplyState(visualState);
        }

        private void AddStyleSheet()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
                styleSheets.Add(styleSheet);
        }

        private VisualElement AddOriginIcon()
        {
            var icon = new VisualElement();
            icon.AddToClassList("aa-origin-icon");
            titleContainer.Insert(0, icon);
            return icon;
        }

        private void AddSubtitle(string text)
        {
            var subtitle = new Label(text);
            subtitle.AddToClassList("aa-node-subtitle");
            mainContainer.Insert(1, subtitle);
        }

        private static string GetOriginClass(AsmDefOrigin origin)
        {
            switch (origin)
            {
                case AsmDefOrigin.ProjectAssets:
                    return "aa-origin-project";
                case AsmDefOrigin.EmbeddedPackage:
                    return "aa-origin-package-embedded";
                case AsmDefOrigin.RegistryPackage:
                    return "aa-origin-package-registry";
                case AsmDefOrigin.BuiltIn:
                    return "aa-origin-builtin";
                default:
                    return "aa-origin-package-registry";
            }
        }

        private static string GetOriginLabel(AsmDefOrigin origin)
        {
            switch (origin)
            {
                case AsmDefOrigin.ProjectAssets:
                    return "Project";
                case AsmDefOrigin.EmbeddedPackage:
                    return "Embedded Package";
                case AsmDefOrigin.RegistryPackage:
                    return "Registry Package";
                case AsmDefOrigin.BuiltIn:
                    return "Built-in";
                default:
                    return "Unknown";
            }
        }

        private void ApplySettingsColors()
        {
            var color = GetOriginColor();
            if ((visualState & NodeVisualState.InCycle) != 0)
                color = AssemblyArchitectSettings.instance.CycleEdgeColor;
            if ((visualState & NodeVisualState.Broken) != 0)
                color = AssemblyArchitectSettings.instance.BrokenColor;

            style.borderLeftColor = color;
            if (originIcon != null)
                originIcon.style.backgroundColor = color;
        }

        private Color GetOriginColor()
        {
            var settings = AssemblyArchitectSettings.instance;
            switch (Origin)
            {
                case AsmDefOrigin.ProjectAssets:
                    return settings.ProjectNodeColor;
                case AsmDefOrigin.EmbeddedPackage:
                    return settings.EmbeddedPkgColor;
                case AsmDefOrigin.RegistryPackage:
                case AsmDefOrigin.BuiltIn:
                default:
                    return settings.RegistryPkgColor;
            }
        }
    }
}
