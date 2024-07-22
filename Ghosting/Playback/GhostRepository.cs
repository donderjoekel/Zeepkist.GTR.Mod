using System.Net.Http;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Json;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostRepository
{
    private const string GetGhostByRecordIdQuery
        = "query($id:Int){allRecordMedias(filter:{recordByIdRecord:{id:{equalTo:$id}}}){nodes{ghostUrl}}}";

    [JsonConverter(typeof(JsonPathConverter))]
    private class GetGhostByRecordIdResponse
    {
        [JsonProperty("data.allRecordMedias.nodes[0].ghostUrl")]
        public string GhostUrl { get; set; }
    }

    private readonly GraphQLApiHttpClient _client;
    private readonly IModStorage _modStorage;
    private readonly GhostReaderFactory _ghostReaderFactory;
    private readonly HttpClient _httpClient;

    public GhostRepository(
        GraphQLApiHttpClient client,
        IModStorage modStorage,
        GhostReaderFactory ghostReaderFactory,
        HttpClient httpClient)
    {
        _client = client;
        _modStorage = modStorage;
        _ghostReaderFactory = ghostReaderFactory;
        _httpClient = httpClient;
    }

    public async UniTask<IGhost> GetGhost(int recordId)
    {
        if (TryGetGhostFromDisk(recordId, out IGhost ghost))
        {
            return ghost;
        }

        Result<GetGhostByRecordIdResponse> result = await _client.PostAsync<GetGhostByRecordIdResponse>(
            GetGhostByRecordIdQuery,
            new
            {
                id = recordId
            });

        if (!result.IsSuccess)
        {
            // TODO: Handle
            return null;
        }

        if (string.IsNullOrEmpty(result.Value.GhostUrl))
        {
            // TODO: Handle
            return null;
        }

        return await GetGhost(recordId, result.Value.GhostUrl);
    }

    public async UniTask<IGhost> GetGhost(int recordId, string ghostUrl)
    {
        if (TryGetGhostFromDisk(recordId, out IGhost ghost))
        {
            return ghost;
        }

        HttpResponseMessage response = await _httpClient.GetAsync(ghostUrl);
        if (!response.IsSuccessStatusCode)
        {
            // TODO: Log?
            return null;
        }

        byte[] buffer = await response.Content.ReadAsByteArrayAsync();
        _modStorage.WriteBlob("ghosts/" + recordId, buffer);
        return _ghostReaderFactory.GetReader(buffer).Read(buffer);
    }

    private bool TryGetGhostFromDisk(int recordId, out IGhost ghost)
    {
        if (!_modStorage.BlobFileExists("ghosts/" + recordId))
        {
            ghost = null;
            return false;
        }

        byte[] buffer = _modStorage.ReadBlob("ghosts/" + recordId);
        ghost = _ghostReaderFactory.GetReader(buffer).Read(buffer);
        return ghost != null;
    }
}
