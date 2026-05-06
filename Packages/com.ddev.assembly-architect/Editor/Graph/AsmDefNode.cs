using System;
using AssemblyArchitect.Editor.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Graph
{
    /// <summary>Visual state flags for an <see cref="AsmDefNode"/>.</summary>
    [Flags]
    internal enum NodeVisualState
    {
        None     = 0,
        InCycle  = 1 << 0,
        Broken   = 1 << 1,
        Filtered = 1 << 2,
    }

    /// <summary>Custom GraphView node representing a single assembly definition.</summary>
    internal sealed class AsmDefNode : Node
    {
        private const string UssPath = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefNode.uss";

        /// <summary>The <see cref="AsmDefData.StableId"/> of the assembly this node represents.</summary>
        public string AsmDefId { get; }

        /// <summary>The single input port (accepts incoming references).</summary>
        public Port InputPort { get; }

        /// <summary>The single output port (declares outgoing references).</summary>
        public Port OutputPort { get; }

        private NodeVisualState _currentState;

        public AsmDefNode(AsmDefNodeModel model)
        {
            AsmDefId = model.Id;
            userData  = model.Id;
            title     = model.Name;

            // Load stylesheet
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) styleSheets.Add(uss);

            AddToClassList("aa-node");

            // Apply origin class + accent dot
            ApplyOriginClass(model.Origin);

            // Subtitle
            var subtitle = new Label(OriginLabel(model.Origin));
            subtitle.AddToClassList("aa-node-subtitle");
            titleContainer.Add(subtitle);

            // Remove default expand/collapse caret
            titleButtonContainer.Clear();

            // Ports
            InputPort = Port.Create<AsmDefEdge>(
                Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);

            OutputPort = Port.Create<AsmDefEdge>(
                Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = string.Empty;
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>Applies or removes visual state classes without rebuilding the node.</summary>
        public void ApplyState(NodeVisualState state)
        {
            SetClass("aa-state-cycle",    state.HasFlag(NodeVisualState.InCycle));
            SetClass("aa-state-broken",   state.HasFlag(NodeVisualState.Broken));
            SetClass("aa-state-filtered", state.HasFlag(NodeVisualState.Filtered));
            _currentState = state;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyOriginClass(AsmDefOrigin origin)
        {
            var cls = origin switch
            {
                AsmDefOrigin.ProjectAssets    => "aa-origin-project",
                AsmDefOrigin.EmbeddedPackage  => "aa-origin-package-embedded",
                AsmDefOrigin.RegistryPackage  => "aa-origin-package-registry",
                AsmDefOrigin.BuiltIn          => "aa-origin-builtin",
                _                             => "aa-origin-project",
            };
            AddToClassList(cls);

            var dot = new VisualElement();
            dot.AddToClassList("aa-origin-dot");
            titleContainer.Insert(0, dot);
        }

        private static string OriginLabel(AsmDefOrigin origin) => origin switch
        {
            AsmDefOrigin.ProjectAssets   => "Project",
            AsmDefOrigin.EmbeddedPackage => "Embedded Package",
            AsmDefOrigin.RegistryPackage => "Registry Package",
            AsmDefOrigin.BuiltIn         => "Built-in",
            _                            => "Unknown",
        };

        private void SetClass(string cls, bool enabled)
        {
            if (enabled) AddToClassList(cls);
            else         RemoveFromClassList(cls);
        }
    }
}
