namespace AnalyzerWebApi.Services
{
    public interface IFoundryServices
    {
        Task<string> AnalyzeDocumentAsync(BinaryData documentBytes, string fileName, string documentMimeType, string expectedJsonSchema, string userInstructions);
        Task<string> AnalyzeImageAsync(BinaryData documentBytes, string fileName, string documentMimeType, string expectedJsonSchema, string userInstructions);
        Task<string> AnalyzeDocumentWithFileSearchAsync(BinaryData documentBytes, string fileName, string documentMimeType, string expectedJsonSchema, string userInstructions);
    }
}
