using System;
using UnityEditor.Experimental.GraphView;

namespace AssemblyArchitect.Editor.Graph
{
    /// <summary>Visual state flags for an <see cref="AsmDefEdge"/>.</summary>
    [Flags]
    internal enum EdgeVisualState
    {
        None     = 0,
        InCycle  = 1 << 0,
        Filtered = 1 << 1,
    }

    /// <summary>Custom edge representing a dependency between two assemblies.</summary>
    internal sealed class AsmDefEdge : Edge
    {
        private EdgeVisualState _currentState;

        /// <summary>Current composite visual state.</summary>
        public EdgeVisualState CurrentState => _currentState;

        public AsmDefEdge()
        {
            AddToClassList("aa-edge");
        }

        /// <summary>Applies or removes visual state classes without rebuilding the edge.</summary>
        public void ApplyState(EdgeVisualState state)
        {
            _currentState = state;
            SetClass("aa-edge-cycle",    state.HasFlag(EdgeVisualState.InCycle));
            SetClass("aa-edge-filtered", state.HasFlag(EdgeVisualState.Filtered));
        }

        private void SetClass(string cls, bool enabled)
        {
            if (enabled) AddToClassList(cls);
            else         RemoveFromClassList(cls);
        }
    }
}
