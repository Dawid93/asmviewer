using System;
using System.Collections.Generic;
using AssemblyArchitect.Editor.Graph;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window
{
    /// <summary>Sticky strip shown below the toolbar when dependency cycles are detected.</summary>
    internal sealed class CycleBanner : VisualElement
    {
        private readonly AsmDefGraphView _graphView;
        private IReadOnlyList<IReadOnlyList<string>> _cycles;
        private int _currentIndex;
        private readonly Label _label;

        public CycleBanner(AsmDefGraphView graphView)
        {
            _graphView = graphView;
            _cycles    = Array.Empty<IReadOnlyList<string>>();

            AddToClassList("aa-cycle-banner");
            style.display = DisplayStyle.None;

            _label = new Label();
            _label.AddToClassList("aa-cycle-banner-label");
            _label.style.flexGrow = 1;
            Add(_label);

            var btn = new Button(FocusNext) { text = "Focus" };
            btn.AddToClassList("aa-cycle-banner-btn");
            Add(btn);
        }

        /// <summary>Refreshes banner visibility and text based on the current cycle list.</summary>
        public void Update(IReadOnlyList<IReadOnlyList<string>> cycles)
        {
            _cycles       = cycles ?? Array.Empty<IReadOnlyList<string>>();
            _currentIndex = 0;

            if (_cycles.Count == 0)
            {
                style.display = DisplayStyle.None;
                return;
            }

            style.display = DisplayStyle.Flex;
            int n = _cycles.Count;
            _label.text = $"⚠ {n} dependency cycle{(n == 1 ? "" : "s")} detected — Focus next";
        }

        private void FocusNext()
        {
            if (_cycles == null || _cycles.Count == 0) return;

            var cycle = _cycles[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _cycles.Count;

            _graphView.ClearSelection();
            foreach (var id in cycle)
            {
                var node = _graphView.GetNodeById(id);
                if (node != null) _graphView.AddToSelection(node);
            }
            _graphView.FrameSelection();
        }
    }
}
