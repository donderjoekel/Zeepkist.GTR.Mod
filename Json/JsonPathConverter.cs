using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace TNRD.Zeepkist.GTR.Json;

public class JsonPathConverter : JsonConverter
{
    /// <inheritdoc />
    public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object existingValue,
        JsonSerializer serializer)
    {
        JObject jsonObject = JObject.Load(reader);
        object targetObject = Activator.CreateInstance(objectType);

        foreach (PropertyInfo prop in objectType.GetProperties().Where(p => p.CanRead && p.CanWrite))
        {
            JsonPropertyAttribute att = prop.GetCustomAttributes(true)
                .OfType<JsonPropertyAttribute>()
                .FirstOrDefault();

            string jsonPath = att != null ? att.PropertyName : prop.Name;

            if (string.IsNullOrEmpty(jsonPath))
            {
                // TODO: Log?
                continue;
            }

            if (serializer.ContractResolver is DefaultContractResolver resolver)
            {
                jsonPath = resolver.GetResolvedPropertyName(jsonPath);
            }

            JToken token = jsonObject.SelectToken(jsonPath);
            if (token == null || token.Type == JTokenType.Null)
                continue;

            object value = token.ToObject(prop.PropertyType, serializer);
            prop.SetValue(targetObject, value, null);
        }

        return targetObject!;
    }

    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        // CanConvert is not called when [JsonConverter] attribute is used
        return objectType.GetCustomAttributes(true).OfType<JsonPathConverter>().Any();
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
