using System;

namespace Hestia.Logging.Abstractions
{
    internal sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();

        private NullScope() { }
        public void Dispose() { }
    }
}
