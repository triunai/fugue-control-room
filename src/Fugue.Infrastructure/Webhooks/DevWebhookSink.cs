using Fugue.Application.Features.Webhooks.Sink;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>The in-memory state of the Development sink (ADR-011, D-040): bounded, never persisted, thread safe.</summary>
internal sealed class DevWebhookSink : IDevWebhookSink
{
    private const int Retained = 50;
    private const int RememberedIds = 1000;

    private readonly object _gate = new();
    private readonly LinkedList<ReceivedWebhookDto> _latest = new();
    private readonly Queue<string> _idOrder = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private string? _secret;

    public string? Secret
    {
        get { lock (_gate) { return _secret; } }
        set { lock (_gate) { _secret = value; } }
    }

    public bool TryAdd(ReceivedWebhookDto received)
    {
        lock (_gate)
        {
            if (!_ids.Add(received.EventId))
            {
                return false;
            }

            _idOrder.Enqueue(received.EventId);
            if (_idOrder.Count > RememberedIds)
            {
                _ids.Remove(_idOrder.Dequeue());
            }

            _latest.AddFirst(received);
            if (_latest.Count > Retained)
            {
                _latest.RemoveLast();
            }

            return true;
        }
    }

    public IReadOnlyList<ReceivedWebhookDto> Latest(int take)
    {
        lock (_gate)
        {
            return _latest.Take(take).ToList();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _latest.Clear();
            _idOrder.Clear();
            _ids.Clear();
        }
    }
}
