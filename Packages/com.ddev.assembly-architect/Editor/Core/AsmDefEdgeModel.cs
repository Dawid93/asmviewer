namespace AssemblyArchitect.Editor.Core
{
    /// <summary>Immutable directed edge in the dependency graph representing an assembly reference.</summary>
    public sealed class AsmDefEdgeModel
    {
        /// <summary>Id of the node that declares the reference.</summary>
        public string SourceId { get; }

        /// <summary>Id of the node being referenced.</summary>
        public string TargetId { get; }

        internal AsmDefEdgeModel(string sourceId, string targetId)
        {
            SourceId = sourceId;
            TargetId = targetId;
        }
    }
}
