using System.Threading.Channels;

namespace Axiom.Atlas.Infrastructure.Services.ServiceDesk
{
    public sealed class GlpiImprovementTicketSynchronizationQueue
    {
        private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });
        private int _pending;
        private int _processing;

        public bool IsSynchronizationPending => Volatile.Read(ref _pending) == 1 || Volatile.Read(ref _processing) == 1;

        public void RequestSynchronization()
        {
            // A refresh request while a reconciliation is already running must reuse it.
            // Otherwise page polling could enqueue an endless sequence of full GLPI scans.
            if (Volatile.Read(ref _processing) == 1 ||
                Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
            {
                return;
            }

            if (!_requests.Writer.TryWrite(true))
            {
                Interlocked.Exchange(ref _pending, 0);
            }
        }

        public bool TryDequeue()
        {
            if (!_requests.Reader.TryRead(out _))
            {
                return false;
            }

            Interlocked.Exchange(ref _pending, 0);
            return true;
        }

        public void SetProcessing(bool processing) => Interlocked.Exchange(ref _processing, processing ? 1 : 0);
    }
}
