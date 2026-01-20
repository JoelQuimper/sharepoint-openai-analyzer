namespace AnalyzerWebApi.Service
{
    public interface IDocumentService
    {
        Task<string> AnalyzeDocumentAsync(BinaryData documentBytes, string documentMimeType, string expectedJsonSchema, string userInstructions);
    }
}