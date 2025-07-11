using System.Text.Json.Serialization;

namespace Report.Models
{
    public class CosmosQueryResult
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("Files")]
        public List<FileInfo> Files { get; set; } = new();
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
    }
}
