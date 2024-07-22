using ZeepSDK.Messaging;

namespace TNRD.Zeepkist.GTR.Messaging;

public class MessengerService
{
    private readonly ITaggedMessenger _taggedMessenger = MessengerApi.CreateTaggedMessenger("GTR");

    public void Log(string message, float duration = MessengerApi.DEFAULT_DURATION)
    {
        _taggedMessenger.Log(message, duration);
    }

    public void LogWarning(string message, float duration = MessengerApi.DEFAULT_DURATION)
    {
        _taggedMessenger.LogWarning(message, duration);
    }

    public void LogError(string message, float duration = MessengerApi.DEFAULT_DURATION)
    {
        _taggedMessenger.LogError(message, duration);
    }

    public void LogSuccess(string message, float duration = MessengerApi.DEFAULT_DURATION)
    {
        _taggedMessenger.LogSuccess(message, duration);
    }
}
