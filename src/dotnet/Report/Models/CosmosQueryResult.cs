using System.Text.Json.Serialization;

namespace Report.Models
{
    public class CosmosQueryResult
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("LastUpdatedOn")]
        public DateTime LastUpdatedOn { get; set; }

        [JsonPropertyName("Files")]
        public List<FileInfo> Files { get; set; } = new();

        [JsonPropertyName("PackageName")]
        public string PackageName { get; set; } = string.Empty;

        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;
    }

    public class FileInfo
    {
        [JsonPropertyName("FileId")]
        public string FileId { get; set; } = string.Empty;

        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("CreationDate")]
        public DateTime CreationDate { get; set; }

        [JsonPropertyName("Name")]
        public string? FileName { get; set; }

        [JsonPropertyName("VersionString")]
        public string? VersionString { get; set; }
    }
}
