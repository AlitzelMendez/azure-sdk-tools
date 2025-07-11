namespace Report.Models;

public class Report
{
    public List<RowReport> Row { get; set; } = [];
}

public class RowReport
{
    public string RevisionId { get; set; }
    public string ReviewName { get; set; }
    public bool IsTypeSpecBase { get; set; }
}
