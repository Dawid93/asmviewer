using System;
using System.Diagnostics;
using UnityEditor;

namespace AssemblyArchitect.Editor.Graph
{
    internal sealed class Debouncer
    {
        private readonly int milliseconds;
        private readonly Action callback;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private bool pending;

        public Debouncer(int milliseconds, Action callback)
        {
            this.milliseconds = Math.Max(1, milliseconds);
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public void Bump()
        {
            stopwatch.Restart();
            if (pending)
                return;

            pending = true;
            EditorApplication.update += Tick;
        }

        public void Cancel()
        {
            if (!pending)
                return;

            pending = false;
            stopwatch.Reset();
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            if (!pending || stopwatch.ElapsedMilliseconds < milliseconds)
                return;

            Cancel();
            callback();
        }
    }
}
