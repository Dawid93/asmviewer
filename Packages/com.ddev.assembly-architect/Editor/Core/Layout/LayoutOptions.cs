namespace AssemblyArchitect.Editor.Core.Layout
{
    /// <summary>Tuning parameters shared by all <see cref="IGraphLayout"/> implementations.</summary>
    internal sealed class LayoutOptions
    {
        /// <summary>Node width in pixels. Default: <c>220</c>.</summary>
        public float NodeWidth { get; set; } = 220f;

        /// <summary>Node height in pixels. Default: <c>80</c>.</summary>
        public float NodeHeight { get; set; } = 80f;

        /// <summary>Horizontal gap between columns. Default: <c>80</c>.</summary>
        public float HorizontalSpacing { get; set; } = 80f;

        /// <summary>Vertical gap between layers/rows. Default: <c>60</c>.</summary>
        public float VerticalSpacing { get; set; } = 60f;

        /// <summary>Random seed for deterministic layouts. Default: <c>42</c>.</summary>
        public int Seed { get; set; } = 42;

        /// <summary>Number of iterations for iterative layouts (e.g. force-directed). Default: <c>200</c>.</summary>
        public int Iterations { get; set; } = 200;
    }
}
