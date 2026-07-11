namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal struct KeyRepeatTracker
{
    public const float InitialDelay = 0.4f;
    public const float RepeatInterval = 0.08f;

    private float _holdTime;
    private float _nextRepeatAt;

    public bool TryConsumeRepeat(bool isDown, bool isDownThisFrame, float deltaTime)
    {
        if (!isDown)
        {
            Reset();
            return false;
        }

        if (isDownThisFrame)
        {
            _holdTime = 0f;
            _nextRepeatAt = InitialDelay;
            return true;
        }

        _holdTime += deltaTime;
        if (_holdTime < _nextRepeatAt)
            return false;

        _nextRepeatAt += RepeatInterval;
        return true;
    }

    private void Reset()
    {
        _holdTime = 0f;
        _nextRepeatAt = 0f;
    }
}
