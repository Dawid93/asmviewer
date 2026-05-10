using System.IO;

namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>Production implementation of <see cref="IFileSystem"/> backed by <see cref="System.IO.File"/>.</summary>
    internal sealed class DefaultFileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public bool Exists(string path) => File.Exists(path);

        /// <inheritdoc/>
        public string ReadAllText(string path) => File.ReadAllText(path);

        /// <inheritdoc/>
        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    }
}
