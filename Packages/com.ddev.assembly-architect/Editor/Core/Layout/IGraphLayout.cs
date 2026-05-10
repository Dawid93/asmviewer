using System.Collections.Generic;
using UnityEngine;

namespace AssemblyArchitect.Editor.Core.Layout
{
    /// <summary>Computes 2-D positions for every node in a <see cref="DependencyGraphModel"/>.</summary>
    internal interface IGraphLayout
    {
        /// <summary>
        /// Returns a position for each node id in <paramref name="graph"/>.
        /// The returned dictionary contains an entry for every node, even isolated ones.
        /// </summary>
        IReadOnlyDictionary<string, Vector2> Compute(DependencyGraphModel graph, LayoutOptions options);
    }
}
