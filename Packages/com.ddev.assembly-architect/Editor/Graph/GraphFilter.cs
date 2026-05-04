namespace AssemblyArchitect.Editor.Graph
{
    internal readonly struct GraphFilter
    {
        public GraphFilter(string searchQuery, bool showPackages, bool showBuiltIns)
        {
            SearchQuery = searchQuery ?? string.Empty;
            ShowPackages = showPackages;
            ShowBuiltIns = showBuiltIns;
        }

        public string SearchQuery { get; }
        public bool ShowPackages { get; }
        public bool ShowBuiltIns { get; }
    }
}
