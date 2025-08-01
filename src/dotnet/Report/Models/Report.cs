namespace Report.Models;

public class Report
{
    public List<RowReport> Row { get; set; } = [];
}

public class RowReport
{
    public string RevisionId { get; set; }
    public string PackageName { get; set; }
    public string PackageVersion { get; set; }
    public string ParserVersion { get; set; }
    public string Language { get; set; }
    public int TotalLines { get; set; }
    public int HandwrittenLines { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class LinesAnalysis
{
    public int TotalLines { get; set; }
    public int HandwrittenLines { get; set; }
}
