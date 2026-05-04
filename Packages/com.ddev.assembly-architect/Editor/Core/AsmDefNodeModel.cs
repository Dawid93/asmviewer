namespace AssemblyArchitect.Editor.Core
{
    /// <summary>Immutable node in the dependency graph representing a single assembly definition.</summary>
    public sealed class AsmDefNodeModel
    {
        /// <summary>Stable identifier — equals <see cref="AsmDefData.StableId"/> of the source asset.</summary>
        public string Id { get; }

        /// <summary>Assembly name, e.g. <c>MyCompany.MyFeature</c>.</summary>
        public string Name { get; }

        /// <summary>Where this assembly originates within the Unity project layout.</summary>
        public AsmDefOrigin Origin { get; }

        /// <summary>Project-relative asset path, e.g. <c>Assets/Foo/Foo.asmdef</c>.</summary>
        public string AssetPath { get; }

        internal AsmDefNodeModel(string id, string name, AsmDefOrigin origin, string assetPath)
        {
            Id        = id;
            Name      = name;
            Origin    = origin;
            AssetPath = assetPath;
        }
    }
}
