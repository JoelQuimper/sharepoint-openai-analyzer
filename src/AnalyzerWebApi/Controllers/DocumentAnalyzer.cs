using AnalyzerWebApi.Models;
using AnalyzerWebApi.Services;
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
        private readonly IFoundryServices _foundryServices;

        public DocumentAnalyzerController(GraphServiceClient graphClient, IFoundryServices foundryServices, ILogger<DocumentAnalyzerController> logger)
        {
            _graphClient = graphClient;
            _foundryServices = foundryServices;
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
                var mimeType = fileInfo?.File?.MimeType ?? "application/octet-stream";
                var fileName = fileInfo?.Name ?? "unknown";
                _logger.LogInformation($"Retrieved file: {fileName}, Type: {mimeType}");

                // Download the file content                
                var file = await _graphClient.Drives[documentInfo.DriveId].Items[documentInfo.DriveItemId].Content.GetAsync();
                var documentBytes = BinaryData.FromStream(file!, mimeType);

                // Route to appropriate Foundry agent based on document type
                string result = await RouteDocumentToFoundryAgent(
                    documentBytes: documentBytes,
                    fileName: fileName,
                    documentMimeType: mimeType,
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

        private async Task<string> RouteDocumentToFoundryAgent(BinaryData documentBytes, string fileName, string documentMimeType, string expectedJsonSchema, string userInstructions)
        {
            _logger.LogInformation($"Routing document {fileName} with MIME type {documentMimeType}");

            return documentMimeType switch
            {
                // Images: use analyse image agent
                string type when type.StartsWith("image/") =>
                    await _foundryServices.AnalyzeImageAsync(documentBytes, fileName, documentMimeType, expectedJsonSchema, userInstructions),

                // PDFs: use analyse file agent
                "application/pdf" =>
                    await _foundryServices.AnalyzeDocumentAsync(documentBytes, fileName, documentMimeType, expectedJsonSchema, userInstructions),

                // All other types: use file search agent THIS SHOULD BE RESTRICTED SOME MIMETYPE ARE NOT SUPPORTED.  I have not implemented that check yet.
                _ =>
                    await _foundryServices.AnalyzeDocumentWithFileSearchAsync(documentBytes, fileName, documentMimeType, expectedJsonSchema, userInstructions)
            };
        }
    }
}