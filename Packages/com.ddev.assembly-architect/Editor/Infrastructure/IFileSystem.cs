namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>Seam over disk I/O so unit tests can substitute a fake implementation.</summary>
    internal interface IFileSystem
    {
        /// <summary>Returns <c>true</c> if the file at <paramref name="path"/> exists.</summary>
        bool Exists(string path);

        /// <summary>Reads all text from the file at <paramref name="path"/>.</summary>
        string ReadAllText(string path);

        /// <summary>Writes <paramref name="contents"/> to the file at <paramref name="path"/>.</summary>
        void WriteAllText(string path, string contents);
    }
}
