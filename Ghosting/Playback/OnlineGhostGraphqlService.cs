using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using Steamworks;
using TNRD.Zeepkist.GTR.Api;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using Result = ZeepSDK.External.FluentResults.Result;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OnlineGhostGraphqlService
{
    private const string Query
        = "fragment frag on Record{id,userByIdUser{steamName}recordMediasByIdRecord{nodes{ghostUrl}}}query personalbests($steamId:BigFloat,$hash:String,$year:Int,$quarter:Int,$month:Int,$week:Int,$day:Int){allPersonalBestGlobals(filter:{levelByIdLevel:{hash:{equalTo:$hash}},userByIdUser:{steamId:{equalTo:$steamId}}}){nodes{recordByIdRecord{...frag}}}allPersonalBestYearlies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},userByIdUser:{steamId:{equalTo:$steamId}},year:{equalTo:$year}}){nodes{recordByIdRecord{...frag}}}allPersonalBestQuarterlies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},userByIdUser:{steamId:{equalTo:$steamId}},year:{equalTo:$month},quarter:{equalTo:$quarter}}){nodes{recordByIdRecord{...frag}}}allPersonalBestMonthlies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},userByIdUser:{steamId:{equalTo:$steamId}},year:{equalTo:$year},month:{equalTo:$month}}){nodes{recordByIdRecord{...frag}}}allPersonalBestWeeklies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},userByIdUser:{steamId:{equalTo:$steamId}},year:{equalTo:$year},week:{equalTo:$week}}){nodes{recordByIdRecord{...frag}}}allPersonalBestDailies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},userByIdUser:{steamId:{equalTo:$steamId}},year:{equalTo:$year},day:{equalTo:$day}}){nodes{recordByIdRecord{...frag}}}}";

    private readonly GraphQLApiHttpClient _client;

    protected GraphQLApiHttpClient Client => _client;

    public OnlineGhostGraphqlService(GraphQLApiHttpClient client)
    {
        _client = client;
    }

    public async UniTask<Result<List<PersonalBest>>> GetPersonalBests(string levelHash)
    {
        DateTime now = DateTime.UtcNow;
        Calendar calendar = CultureInfo.InvariantCulture.Calendar;
        int year = calendar.GetYear(now);
        int quarter = (calendar.GetMonth(now) - 1) / 3 + 1;
        int month = calendar.GetMonth(now);
        int week = calendar.GetWeekOfYear(now, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        int day = calendar.GetDayOfYear(now);

        Result<Root> result = await _client.PostAsync<Root>(
            Query,
            new
            {
                steamId = SteamClient.SteamId.ToString(),
                hash = levelHash,
                year,
                quarter,
                month,
                week,
                day
            });


        if (result.IsFailed)
        {
            return result.ToResult();
        }

        return Result.Ok(GetUniquePersonalBests(Map(result.Value)));
    }

    private static List<PersonalBest> GetUniquePersonalBests(PersonalBests personalBests)
    {
        List<PersonalBest> uniquePersonalBests = [];

        if (personalBests.Global != null)
        {
            uniquePersonalBests.Add(personalBests.Global);
        }

        if (personalBests.Quarterly != null && uniquePersonalBests.All(x => x.Id != personalBests.Quarterly.Id))
        {
            uniquePersonalBests.Add(personalBests.Quarterly);
        }

        if (personalBests.Monthly != null && uniquePersonalBests.All(x => x.Id != personalBests.Monthly.Id))
        {
            uniquePersonalBests.Add(personalBests.Monthly);
        }

        if (personalBests.Weekly != null && uniquePersonalBests.All(x => x.Id != personalBests.Weekly.Id))
        {
            uniquePersonalBests.Add(personalBests.Weekly);
        }

        if (personalBests.Daily != null && uniquePersonalBests.All(x => x.Id != personalBests.Daily.Id))
        {
            uniquePersonalBests.Add(personalBests.Daily);
        }

        return uniquePersonalBests;
    }

    private class PersonalBests
    {
        public PersonalBest Global { get; set; }
        public PersonalBest Yearly { get; set; }
        public PersonalBest Quarterly { get; set; }
        public PersonalBest Monthly { get; set; }
        public PersonalBest Weekly { get; set; }
        public PersonalBest Daily { get; set; }
    }

    public class PersonalBest
    {
        public int Id { get; set; }
        public string SteamName { get; set; }
        public string GhostUrl { get; set; }
    }

    private static PersonalBests Map(Root root)
    {
        return Map(root.Data);
    }

    private static PersonalBests Map(Data data)
    {
        return new PersonalBests
        {
            Global = Map(data.AllPersonalBestGlobals),
            Yearly = Map(data.AllPersonalBestYearlies),
            Quarterly = Map(data.AllPersonalBestQuarterlies),
            Monthly = Map(data.AllPersonalBestMonthlies),
            Weekly = Map(data.AllPersonalBestWeeklies),
            Daily = Map(data.AllPersonalBestDailies)
        };
    }

    private static PersonalBest Map(AllPersonalBestGlobals globals)
    {
        return Map(globals.Nodes.FirstOrDefault()?.RecordByIdRecord);
    }

    private static PersonalBest Map(AllPersonalBestYearlies yearlies)
    {
        return Map(yearlies.Nodes.FirstOrDefault()?.RecordByIdRecord);
    }

    private static PersonalBest Map(AllPersonalBestQuarterlies quarterlies)
    {
        return Map(quarterlies.Nodes.FirstOrDefault()?.RecordByIdRecord);
    }

    private static PersonalBest Map(AllPersonalBestMonthlies monthlies)
    {
        return Map(monthlies.Nodes.FirstOrDefault()?.RecordByIdRecord);
    }

    private static PersonalBest Map(AllPersonalBestWeeklies weeklies)
    {
        return Map(weeklies.Nodes.FirstOrDefault()?.RecordByIdRecord);
    }

    private static PersonalBest Map(AllPersonalBestDailies dailies)
    {
        return Map(dailies.Nodes.FirstOrDefault()?.RecordByIdRecord);
    }

    protected static PersonalBest Map(RecordByIdRecord record)
    {
        if (record == null)
            return null;

        return new PersonalBest
        {
            Id = record.Id,
            SteamName = Map(record.UserByIdUser),
            GhostUrl = Map(record.RecordMediasByIdRecord)
        };
    }

    protected static string Map(UserByIdUser userByIdUser)
    {
        return userByIdUser.SteamName;
    }

    protected static string Map(RecordMediasByIdRecord recordMediasByIdRecord)
    {
        return Map(recordMediasByIdRecord.Nodes.FirstOrDefault());
    }

    protected static string Map(RecordMediaNode recordMediaNode)
    {
        return recordMediaNode == null ? string.Empty : recordMediaNode.GhostUrl;
    }

    [UsedImplicitly]
    private class Root
    {
        public Data Data { get; set; }
    }

    [UsedImplicitly]
    private class Data
    {
        public AllPersonalBestGlobals AllPersonalBestGlobals { get; set; }
        public AllPersonalBestYearlies AllPersonalBestYearlies { get; set; }
        public AllPersonalBestQuarterlies AllPersonalBestQuarterlies { get; set; }
        public AllPersonalBestMonthlies AllPersonalBestMonthlies { get; set; }
        public AllPersonalBestWeeklies AllPersonalBestWeeklies { get; set; }
        public AllPersonalBestDailies AllPersonalBestDailies { get; set; }
    }

    [UsedImplicitly]
    protected class AllPersonalBestGlobals
    {
        [UsedImplicitly] public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    private class AllPersonalBestYearlies
    {
        [UsedImplicitly] public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    private class AllPersonalBestQuarterlies
    {
        [UsedImplicitly] public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    private class AllPersonalBestMonthlies
    {
        [UsedImplicitly] public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    private class AllPersonalBestWeeklies
    {
        [UsedImplicitly] public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    private class AllPersonalBestDailies
    {
        [UsedImplicitly] public List<Node> Nodes { get; set; }
    }

    [UsedImplicitly]
    protected class Node
    {
        public RecordByIdRecord RecordByIdRecord { get; set; }
    }

    [UsedImplicitly]
    protected class RecordByIdRecord
    {
        public int Id { get; set; }
        public UserByIdUser UserByIdUser { get; set; }
        public RecordMediasByIdRecord RecordMediasByIdRecord { get; set; }
    }

    [UsedImplicitly]
    protected class UserByIdUser
    {
        public string SteamName { get; set; }
    }

    [UsedImplicitly]
    protected class RecordMediasByIdRecord
    {
        [UsedImplicitly] public List<RecordMediaNode> Nodes { get; set; }
    }

    [UsedImplicitly]
    protected class RecordMediaNode
    {
        public string GhostUrl { get; set; }
    }
}
