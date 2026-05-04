using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace AssemblyArchitect.Editor.Graph
{
    internal sealed class AsmDefSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private Action<Vector2> createRequested;

        public void Initialize(Action<Vector2> createRequested)
        {
            this.createRequested = createRequested;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            return new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Assembly Architect"), 0),
                new SearchTreeEntry(new GUIContent("Create Assembly Definition...")) { level = 1, userData = "create" },
            };
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (!string.Equals(searchTreeEntry.userData as string, "create", StringComparison.Ordinal))
                return false;

            createRequested?.Invoke(context.screenMousePosition);
            return true;
        }
    }
}
