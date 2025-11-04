using AnalyzerWebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using OpenAI.Chat;

namespace AnalyzerWebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class DocumentAnalyzerController : ControllerBase
    {
        private readonly ILogger<DocumentAnalyzerController> _logger;
        private readonly GraphServiceClient _graphClient;
        private readonly ChatClient _chatClient;

        public DocumentAnalyzerController(GraphServiceClient graphClient, ChatClient chatClient, ILogger<DocumentAnalyzerController> logger)
        {
            _graphClient = graphClient;
            _chatClient = chatClient;
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
                var file = await _graphClient.Drives[documentInfo.DriveId].Items[documentInfo.DriveItemId].Content.GetAsync();
                
                var imageBytes = BinaryData.FromStream(file!);

                var systemMessage = $"Analyze the attached document and extract the information as requested by the user. " +
                $"You must follow the JSON schema below and ensure that all required information is included. {documentInfo.ExpectedJsonSchema} ";

                var completionOptions = new ChatCompletionOptions
                {
                    ResponseFormat= ChatResponseFormat.CreateJsonObjectFormat()
                };

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemMessage),
                    new UserChatMessage(new List<ChatMessageContentPart>
                    {
                        ChatMessageContentPart.CreateTextPart(documentInfo.UserPrompt),
                        ChatMessageContentPart.CreateImagePart(imageBytes, "image/jpeg")
                    })
                };

                var result = await _chatClient.CompleteChatAsync(messages, completionOptions);

                var jsonResponse = result.Value.Content[0].Text;
                _logger.LogInformation($"JSON Response: {jsonResponse}");

                _logger.LogInformation("Document processing completed.");
                
                return Ok(jsonResponse);
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