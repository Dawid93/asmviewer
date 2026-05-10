using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace AssemblyArchitect.Editor.Graph
{
    /// <summary>
    /// Search window provider (Spacebar shortcut) with a single "Create Assembly Definition…" entry.
    /// Triggered from <see cref="AsmDefGraphView"/> keyboard handling.
    /// </summary>
    internal sealed class AsmDefSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        /// <summary>Called when the user selects the create entry. Parameter is the screen-space mouse position.</summary>
        private Action<Vector2> _onCreateSelected;

        // ── Factory ───────────────────────────────────────────────────────────

        /// <summary>Creates a ready-to-use provider. Destroy it when the graph is torn down.</summary>
        public static AsmDefSearchProvider Create(Action<Vector2> onCreateSelected)
        {
            var provider              = CreateInstance<AsmDefSearchProvider>();
            provider._onCreateSelected = onCreateSelected;
            return provider;
        }

        // ── ISearchWindowProvider ─────────────────────────────────────────────

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            return new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Assembly Architect"), 0),
                new SearchTreeEntry(new GUIContent("Create Assembly Definition…"))
                {
                    level    = 1,
                    userData = "create",
                },
            };
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            _onCreateSelected?.Invoke(context.screenMousePosition);
            return true;
        }
    }
}
