using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace AssemblyArchitect.Editor.Graph
{
    [Flags]
    internal enum EdgeVisualState
    {
        None = 0,
        InCycle = 1 << 0,
        Filtered = 1 << 1,
    }

    internal sealed class AsmDefEdge : Edge
    {
        public AsmDefEdge()
        {
            AddToClassList("aa-asmdef-edge");
            ApplyState(EdgeVisualState.None);
        }

        public void ApplyState(EdgeVisualState state)
        {
            EnableInClassList("aa-edge-cycle", (state & EdgeVisualState.InCycle) != 0);
            EnableInClassList("aa-edge-filtered", (state & EdgeVisualState.Filtered) != 0);

            var color = (state & EdgeVisualState.InCycle) != 0
                ? new Color(1f, 0.55f, 0.26f)
                : new Color(0.5f, 0.5f, 0.5f);
            edgeControl.inputColor = color;
            edgeControl.outputColor = color;
            edgeControl.edgeWidth = 2;
        }
    }
}
