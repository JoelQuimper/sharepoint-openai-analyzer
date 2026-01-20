using AnalyzerWebApi.Models;
using AnalyzerWebApi.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;

namespace AnalyzerWebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class DocumentAnalyzerController : ControllerBase
    {
        private readonly ILogger<DocumentAnalyzerController> _logger;
        private readonly GraphServiceClient _graphClient;
        private readonly IDocumentService _documentService;

        public DocumentAnalyzerController(GraphServiceClient graphClient, IDocumentService documentService, ILogger<DocumentAnalyzerController> logger)
        {
            _graphClient = graphClient;
            _documentService = documentService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitDocument([FromBody] DocumentInfo documentInfo)
        {
            _logger.LogInformation("Document submitted for analysis.");
            _logger.LogDebug($"DriveId: {documentInfo.DriveId}, DriveItemId: {documentInfo.DriveItemId}");
            _logger.LogDebug($"UserPrompt: {documentInfo.UserPrompt}");
            _logger.LogDebug($"ExpectedJsonSchema: {documentInfo.ExpectedJsonSchema}");

            try
            {
                // Retrieve the file metadata from SharePoint using Microsoft Graph
                var fileInfo = await _graphClient.Drives[documentInfo.DriveId].Items[documentInfo.DriveItemId].GetAsync();
                var mimeType = fileInfo?.File?.MimeType;
                _logger.LogInformation($"Retrieved file: {fileInfo?.Name}, Type: {mimeType}");

                // Download the file content                
                var file = await _graphClient.Drives[documentInfo.DriveId].Items[documentInfo.DriveItemId].Content.GetAsync();
                var imageBytes = BinaryData.FromStream(file!);

                // Call the DocumentService to analyze the document or image based on its mime type
                var result = await _documentService.AnalyzeDocumentAsync(
                    documentBytes: imageBytes,
                    documentMimeType: mimeType ?? "application/octet-stream",
                    expectedJsonSchema: documentInfo.ExpectedJsonSchema,
                    userInstructions: documentInfo.UserPrompt
                );
                
                _logger.LogDebug($"Analysis Result: {result}");
                _logger.LogInformation("Document processing completed.");
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                // output the exception class received
                _logger.LogError(ex, $"An error of type {ex.GetType()} occurred while processing the document.");
                if (ex is Microsoft.Graph.Models.ODataErrors.ODataError)
                    return NotFound("The specified document was not found in SharePoint.");
                return StatusCode(500, "An unknown error occurred while processing the document.");
            }
        }
    }
}