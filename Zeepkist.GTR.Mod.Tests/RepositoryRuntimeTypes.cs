// Storage/result adapters keep repository tests independent of Unity and ZeepSDK initialization.
namespace ZeepSDK.Storage
{
    public interface IModStorage
    {
        bool BlobFileExists(string key);
        byte[] ReadBlob(string key);
        void WriteBlob(string key, byte[] bytes);
        void DeleteBlob(string key);
        bool JsonFileExists(string key);
        T LoadFromJson<T>(string key);
        void SaveToJson<T>(string key, T value);
        void DeleteJsonFile(string key);
    }
}
namespace ZeepSDK.External.FluentResults
{
    public class ExceptionalError(Exception error) { public Exception Exception { get; } = error; }
    public class Result
    {
        public static Result<T> Ok<T>(T value) => new() { Value = value, IsSuccess = true };
        public static Result Fail(object error) => new();
    }
    public class Result<T>
    {
        public bool IsSuccess { get; set; }
        public bool IsFailed => !IsSuccess;
        public T Value { get; set; }
        public static implicit operator Result<T>(Result result) => new();
    }
}
namespace TNRD.Zeepkist.GTR.Configuration
{
    public static class ConfigService { public const string CdnUrl = "https://fixture.invalid/"; }
}
namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts
{
    public abstract class GhostBase : IGhost
    {
        internal abstract GhostBase CreatePlayback();
    }
}
namespace TNRD.Zeepkist.GTR.Ghosting.Readers
{
    public sealed class GhostReaderFactory(Func<byte[], Ghosts.IGhost> decode)
    {
        public Ghosts.IGhost Read(byte[] bytes) => decode(bytes);
    }
}
