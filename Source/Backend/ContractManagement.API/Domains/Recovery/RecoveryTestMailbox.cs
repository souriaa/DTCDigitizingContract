using ContractManagement.API.Domains.Email;
using System.Collections.Concurrent;

namespace ContractManagement.API.Domains.Recovery;

// Development/Test only. No controller, console logging or disk export of secrets.
public sealed class RecoveryTestMailbox
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();
    public IReadOnlyList<EmailMessage> Messages => _messages.ToArray();
    public void Add(EmailMessage message)
    {
        _messages.Enqueue(message);
        while (_messages.Count > 20) _messages.TryDequeue(out _);
    }
}
