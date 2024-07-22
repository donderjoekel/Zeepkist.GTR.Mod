#nullable enable
using Newtonsoft.Json.Linq;

namespace TNRD.Zeepkist.GTR.Extensions;

public static class JsonExtensions
{
    public static T? GetProperty<T>(this JObject jObject, string path)
    {
        string[] splits = path.Split('/');

        JToken? token = jObject;

        foreach (string split in splits)
        {
            if (token == null)
                return default;

            token = token[split];
        }

        return token == null ? default : token.Value<T>();
    }
}
