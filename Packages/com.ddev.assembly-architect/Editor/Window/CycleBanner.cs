using System.Collections.Generic;
using AssemblyArchitect.Editor.Graph;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window
{
    internal sealed class CycleBanner : VisualElement
    {
        private readonly AsmDefGraphView graphView;
        private readonly Label label;
        private readonly Button focusButton;
        private IReadOnlyList<IReadOnlyList<string>> cycles = new List<IReadOnlyList<string>>();
        private int index;

        public CycleBanner(AsmDefGraphView graphView)
        {
            this.graphView = graphView;
            AddToClassList("aa-cycle-banner");

            var icon = new Label("!");
            icon.AddToClassList("aa-cycle-icon");
            label = new Label();
            focusButton = new Button(FocusNext) { text = "Focus" };

            Add(icon);
            Add(label);
            Add(focusButton);
            style.display = DisplayStyle.None;
        }

        public void Update(IReadOnlyList<IReadOnlyList<string>> cycles)
        {
            this.cycles = cycles ?? new List<IReadOnlyList<string>>();
            index = 0;

            if (this.cycles.Count == 0)
            {
                style.display = DisplayStyle.None;
                return;
            }

            style.display = DisplayStyle.Flex;
            var suffix = this.cycles.Count == 1 ? "cycle" : "cycles";
            label.text = $"{this.cycles.Count} dependency {suffix} detected - Focus next";
        }

        private void FocusNext()
        {
            if (cycles.Count == 0)
                return;

            var cycle = cycles[index];
            index = (index + 1) % cycles.Count;

            graphView.ClearSelection();
            foreach (var id in cycle)
            {
                var node = graphView.GetNodeById(id);
                if (node != null)
                    graphView.AddToSelection(node);
            }
            graphView.FrameSelection();
        }
    }
}
