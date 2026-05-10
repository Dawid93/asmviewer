using System;
using UnityEditor;

namespace AssemblyArchitect.Editor.Graph
{
    /// <summary>
    /// Delays an action until a specified period of inactivity has elapsed.
    /// Uses <see cref="EditorApplication.update"/> as the tick source.
    /// Call <see cref="Dispose"/> to stop ticking.
    /// </summary>
    internal sealed class Debouncer : IDisposable
    {
        private readonly double _delaySeconds;
        private readonly Action _callback;
        private double _targetTime = -1;
        private bool _disposed;

        /// <param name="milliseconds">Quiet period in milliseconds before the callback fires.</param>
        /// <param name="callback">Action to invoke after the quiet period.</param>
        public Debouncer(int milliseconds, Action callback)
        {
            _delaySeconds = milliseconds / 1000.0;
            _callback     = callback;
            EditorApplication.update += Tick;
        }

        /// <summary>Resets the quiet-period timer. The callback will fire <c>milliseconds</c> after the last <see cref="Bump"/> call.</summary>
        public void Bump()
        {
            if (_disposed) return;
            _targetTime = EditorApplication.timeSinceStartup + _delaySeconds;
        }

        /// <summary>Unregisters the update tick. The callback will never fire after this.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            if (_targetTime < 0 || _disposed) return;
            if (EditorApplication.timeSinceStartup >= _targetTime)
            {
                _targetTime = -1;
                _callback?.Invoke();
            }
        }
    }
}
