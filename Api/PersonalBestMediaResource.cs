namespace TNRD.Zeepkist.GTR.Api;

public class PersonalBestMediaResource
{
    public class RecordResource
    {
        public int Id { get; set; }
    }

    public class RecordMediaResource
    {
        public int Id { get; set; }
        public string GhostUrl { get; set; }
    }

    public int Id { get; set; }
    public RecordResource Record { get; set; }
    public RecordMediaResource RecordMedia { get; set; }
}
