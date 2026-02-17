using Anthropic.SDK.Messaging;

namespace MicroXAgentLoop;

public interface ICompactionStrategy
{
    Task<List<Message>> MaybeCompactAsync(List<Message> messages);
}
