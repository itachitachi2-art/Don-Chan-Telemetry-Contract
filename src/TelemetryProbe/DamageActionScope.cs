using System;
using System.Threading;

namespace DonChan.TelemetryProbe
{
    // Harmony can skip a stateful prefix when another prefix skips the original.
    // A null state means this invocation never installed a scope.
    internal sealed class DamageActionScope
    {
        [ThreadStatic] private static string _current;
        private static long _serial;
        private readonly string _previous;
        private bool _ended;

        private DamageActionScope(string previous) { _previous = previous; }
        internal static string Current { get { return _current; } }

        internal static DamageActionScope Begin(string sessionId)
        {
            var state = new DamageActionScope(_current);
            _current = sessionId + ":damage:" + Interlocked.Increment(ref _serial);
            return state;
        }

        internal static Exception End(DamageActionScope state, Exception exception)
        {
            if (state != null && !state._ended)
            {
                _current = state._previous;
                state._ended = true;
            }
            return exception;
        }
    }
}
