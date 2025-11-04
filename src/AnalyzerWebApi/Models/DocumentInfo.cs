namespace AnalyzerWebApi.Models
{
    public class DocumentInfo
    {
        public required string DriveId { get; set; }
        public required string DriveItemId { get; set; }
        public required string UserPrompt { get; set; }
        public required string ExpectedJsonSchema { get; set; }
    }
}