using System.Collections.Generic;
using AssemblyArchitect.Editor.Infrastructure;

namespace AssemblyArchitect.Tests.Editor.Helpers
{
    /// <summary>In-memory <see cref="IFileSystem"/> for unit tests.</summary>
    internal sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        public void AddFile(string path, string content) => _files[path] = content;

        public bool Exists(string path)       => _files.ContainsKey(path);
        public string ReadAllText(string path) => _files[path];
        public void WriteAllText(string path, string contents) => _files[path] = contents;

        public bool Contains(string path) => _files.ContainsKey(path);
        public string GetContent(string path) => _files.TryGetValue(path, out var v) ? v : null;
    }
}
