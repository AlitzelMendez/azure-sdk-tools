using System.Text.Json.Serialization;

namespace Report.Models
{
    public class ApiViewDocument
    {
        [JsonPropertyName("ReviewLines")]
        public List<ReviewLine> ReviewLines { get; set; } = new();

        [JsonPropertyName("CrossLanguagePackageId")]
        public string? CrossLanguagePackageId { get; set; }

        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("PackageName")]
        public string PackageName { get; set; } = string.Empty;
    }

    public class ReviewLine
    {
        [JsonPropertyName("LineId")]
        public string LineId { get; set; } = string.Empty;

        [JsonPropertyName("CrossLanguageId")]
        public string? CrossLanguageId { get; set; }

        [JsonPropertyName("Tokens")]
        public List<Token> Tokens { get; set; } = new();

        [JsonPropertyName("Children")]
        public List<ReviewLine> Children { get; set; } = new();

        [JsonPropertyName("RelatedToLine")]
        public string? RelatedToLine { get; set; }
    }

    public class Token
    {
        [JsonPropertyName("Kind")]
        public int Kind { get; set; }

        [JsonPropertyName("Value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("SkipDiff")]
        public bool? SkipDiff { get; set; }

        [JsonPropertyName("HasSuffixSpace")]
        public bool HasSuffixSpace { get; set; }

        [JsonPropertyName("HasPrefixSpace")]
        public bool HasPrefixSpace { get; set; }

        [JsonPropertyName("RenderClasses")]
        public List<string> RenderClasses { get; set; } = new();
    }
}
