namespace AssemblyArchitect.Editor.Commands
{
    /// <summary>Data for a yes/no dialog. Passed to the injectable dialog delegate on <see cref="AddReferenceCommand"/>.</summary>
    internal readonly struct DialogPrompt
    {
        public readonly string Title;
        public readonly string Message;
        public readonly string Confirm;
        public readonly string Cancel;

        public DialogPrompt(string title, string message, string confirm, string cancel)
        {
            Title   = title;
            Message = message;
            Confirm = confirm;
            Cancel  = cancel;
        }
    }
}
