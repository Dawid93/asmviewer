namespace AssemblyArchitect.Editor.Core
{
    /// <summary>Describes a reference entry in an <c>.asmdef</c> that could not be resolved to a known assembly.</summary>
    public sealed class MissingReferenceInfo
    {
        /// <summary>Id of the node that declares the unresolvable reference.</summary>
        public string SourceId { get; }

        /// <summary>Raw reference string from the <c>.asmdef</c>, e.g. <c>GUID:abc…</c> or <c>Foo</c>.</summary>
        public string MissingReference { get; }

        /// <summary>Human-readable reason: <c>"guid not found"</c> or <c>"name not found"</c>.</summary>
        public string Reason { get; }

        internal MissingReferenceInfo(string sourceId, string missingReference, string reason)
        {
            SourceId         = sourceId;
            MissingReference = missingReference;
            Reason           = reason;
        }
    }
}
