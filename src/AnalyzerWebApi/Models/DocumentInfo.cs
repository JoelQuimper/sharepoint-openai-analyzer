namespace AnalyzerWebApi.Models
{
    public class DocumentInfo
    {
        public string DriveId { get; set; }
        public string DriveItemId { get; set; }
        public string UserPrompt { get; set; }
        public string ExpectedJsonSchema { get; set; }
    }
}