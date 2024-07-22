using JetBrains.Annotations;

namespace TNRD.Zeepkist.GTR.Api;

public class JsonApiResponse<T>
{
    public JsonApiInfo JsonApi { get; set; }
    public JsonApiLinks Links { get; set; }
    public JsonApiMeta Meta { get; set; }
    public JsonApiData<T>[] Data { get; set; }
    public JsonApiIncluded[] Included { get; set; }
}

public class JsonApiLinks
{
    public string Self { get; set; }
    [CanBeNull] public string First { get; set; }
    [CanBeNull] public string Last { get; set; }
    [CanBeNull] public string Next { get; set; }
}

public class JsonApiInfo
{
    public string Version { get; set; }
}

public class JsonApiMeta
{
    public int Total { get; set; }
}

public class JsonApiData<T>
{
    public string Type { get; set; }
    public string Id { get; set; }
    public T Attributes { get; set; }
    public JsonApiLinks Links { get; set; }
}

public class JsonApiIncluded
{
    public string Type { get; set; }
    public string Id { get; set; }
    public object Attributes { get; set; }
    public JsonApiLinks Links { get; set; }

    public T AttributesAs<T>()
    {
        return (T)Attributes;
    }
}
