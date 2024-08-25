using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TNRD.Zeepkist.GTR.Api;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Commands;

public class WorldRecordCommandGraphQlService
{
    private const string Query
        = "fragment frag on Record{time userByIdUser{steamName}}query personalbests($hash:String,$year:Int,$quarter:Int,$month:Int,$week:Int,$day:Int){allWorldRecordGlobals(filter:{levelByIdLevel:{hash:{equalTo:$hash}}}){nodes{recordByIdRecord{...frag}}}allWorldRecordYearlies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},year:{equalTo:$year}}){nodes{recordByIdRecord{...frag}}}allWorldRecordQuarterlies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},year:{equalTo:$year},quarter:{equalTo:$quarter}}){nodes{recordByIdRecord{...frag}}}allWorldRecordMonthlies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},year:{equalTo:$year},month:{equalTo:$month}}){nodes{recordByIdRecord{...frag}}}allWorldRecordWeeklies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},year:{equalTo:$year},week:{equalTo:$week}}){nodes{recordByIdRecord{...frag}}}allWorldRecordDailies(filter:{levelByIdLevel:{hash:{equalTo:$hash}},year:{equalTo:$year},day:{equalTo:$day}}){nodes{recordByIdRecord{...frag}}}}";

    private readonly GraphQLApiHttpClient _client;

    public WorldRecordCommandGraphQlService(GraphQLApiHttpClient client)
    {
        _client = client;
    }

    public async UniTask<Result<WorldRecords>> GetWorldRecord(string levelHash)
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

        return Result.Ok(MapToWorldRecords(result.Value));
    }

    private static WorldRecords MapToWorldRecords(Root root)
    {
        return new WorldRecords
        {
            Global = MapToWorldRecord(root.Data.AllWorldRecordGlobals),
            Yearly = MapToWorldRecord(root.Data.AllWorldRecordYearlies),
            Quarterly = MapToWorldRecord(root.Data.AllWorldRecordQuarterlies),
            Monthly = MapToWorldRecord(root.Data.AllWorldRecordMonthlies),
            Weekly = MapToWorldRecord(root.Data.AllWorldRecordWeeklies),
            Daily = MapToWorldRecord(root.Data.AllWorldRecordDailies)
        };
    }

    private static WorldRecord MapToWorldRecord(RecordCollection recordCollection)
    {
        Node recordNode = recordCollection.Nodes.FirstOrDefault();
        if (recordNode?.RecordByIdRecord == null)
            return null;

        return new WorldRecord
        {
            Time = recordNode.RecordByIdRecord.Time,
            SteamName = recordNode.RecordByIdRecord.UserByIdUser.SteamName
        };
    }

    private class Root
    {
        public Data Data { get; set; }
    }

    private class Data
    {
        public RecordCollection AllWorldRecordGlobals { get; set; }
        public RecordCollection AllWorldRecordYearlies { get; set; }
        public RecordCollection AllWorldRecordQuarterlies { get; set; }
        public RecordCollection AllWorldRecordMonthlies { get; set; }
        public RecordCollection AllWorldRecordWeeklies { get; set; }
        public RecordCollection AllWorldRecordDailies { get; set; }
    }

    private class RecordCollection
    {
        public List<Node> Nodes { get; set; }
    }

    private class Node
    {
        public Record RecordByIdRecord { get; set; }
    }

    private class Record
    {
        public double Time { get; set; }
        public User UserByIdUser { get; set; }
    }

    private class User
    {
        public string SteamName { get; set; }
    }
}
