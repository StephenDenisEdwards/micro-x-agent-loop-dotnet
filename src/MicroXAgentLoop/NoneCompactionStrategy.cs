using Anthropic.SDK.Messaging;

namespace MicroXAgentLoop;

public class NoneCompactionStrategy : ICompactionStrategy
{
    public Task<List<Message>> MaybeCompactAsync(List<Message> messages, CancellationToken ct = default) =>
        Task.FromResult(messages);
}
