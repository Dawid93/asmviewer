namespace AssemblyArchitect.Editor.Graph
{
    /// <summary>Immutable snapshot of the active graph filter state.</summary>
    internal readonly struct GraphFilter
    {
        /// <summary>Lower-cased search query; may be empty string.</summary>
        public readonly string SearchQuery;

        /// <summary>When <c>false</c>, package-origin nodes (embedded / registry) are hidden.</summary>
        public readonly bool ShowPackages;

        /// <summary>When <c>false</c>, built-in Unity module nodes are hidden.</summary>
        public readonly bool ShowBuiltIns;

        public GraphFilter(string searchQuery, bool showPackages, bool showBuiltIns)
        {
            SearchQuery  = searchQuery ?? string.Empty;
            ShowPackages = showPackages;
            ShowBuiltIns = showBuiltIns;
        }
    }
}
