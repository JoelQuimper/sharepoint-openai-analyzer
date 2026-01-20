using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;

namespace AnalyzerWebApi.Service
{
    public class DocumentService : IDocumentService, IDisposable
    {
        private readonly ILogger<DocumentService> _logger;
        private readonly PersistentAgentsClient _persistentAgentsClient;
        private PersistentAgent _agent;
        private readonly string _deploymentName;
        private readonly string _instanceId = Guid.NewGuid().ToString().Substring(0, 8);

        public DocumentService(string endpoint, string deploymentName, ILogger<DocumentService> logger)
        {
            _logger = logger;
            _deploymentName = deploymentName;
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);
            _persistentAgentsClient = projectClient.GetPersistentAgentsClient();
            _agent = _persistentAgentsClient.Administration.CreateAgent(
                model: _deploymentName,
                name: $"document-agent-{_instanceId}",
                instructions: LoadPrompt("document-system-prompt.txt"),
                temperature: (float?)0.0
            );
        }

        // Use VectorDB to analyze documents
        public async Task<string> AnalyzeDocumentAsync(BinaryData documentBytes, string documentMimeType, string expectedJsonSchema, string userInstructions)
        {
            _logger.LogInformation("{InstanceId} : Starting AnalyzeDocumentAsync", _instanceId);

            var uploadedAgentFile = default(Azure.Response<PersistentAgentFileInfo>);
            string resultJson = null;

            try
            {
                uploadedAgentFile = await _persistentAgentsClient.Files.UploadFileAsync(
                    data: documentBytes.ToStream(),
                    filename: $"document_{_instanceId}.pdf",
                    purpose: PersistentAgentFilePurpose.Agents);
                _logger.LogInformation("{InstanceId} : Uploaded file. File ID: {FileId}, Filename: {Filename}", _instanceId, uploadedAgentFile.Value.Id, uploadedAgentFile.Value.Filename);

                var codeInterpreterTools = new List<ToolDefinition> { new CodeInterpreterToolDefinition() };

                _agent = await _persistentAgentsClient.Administration.UpdateAgentAsync(
                    _agent.Id,
                    tools: codeInterpreterTools
                );
                _logger.LogInformation("{InstanceId} : Updated Agent with Code Interpreter tool. Agent ID: {AgentId}", _instanceId, _agent.Id);

                var thread = await _persistentAgentsClient.Threads.CreateThreadAsync();

                // Create a message with the image attachment using the fileId with CodeInterpreter tool
                var attachment = new MessageAttachment(
                    fileId: uploadedAgentFile.Value.Id,
                    tools: codeInterpreterTools
                );

                await _persistentAgentsClient.Messages.CreateMessageAsync(
                    threadId: thread.Value.Id,
                    role: MessageRole.User,
                    content: @$"The attached document is of type: {documentMimeType}

                        Extract all key information in json according to this schema: {expectedJsonSchema}

                        Here are some additional user's instructions to help: {userInstructions}",
                    attachments: new List<MessageAttachment> { attachment }
                );
                _logger.LogInformation("{InstanceId} : Created message with attachment in thread ID: {ThreadId}", _instanceId, thread.Value.Id);

                ThreadRun run = await _persistentAgentsClient.Runs.CreateRunAsync(
                    threadId: thread.Value.Id,
                    assistantId: _agent.Id
                );

                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    run = await _persistentAgentsClient.Runs.GetRunAsync(thread.Value.Id, run.Id);
                }
                while (run.Status == RunStatus.Queued
                    || run.Status == RunStatus.InProgress);

                _logger.LogInformation("{InstanceId} : Run completed with status: {Status}", _instanceId, run.Status);

                var messages = _persistentAgentsClient.Messages.GetMessagesAsync(
                    threadId: thread.Value.Id, order: ListSortOrder.Ascending);

                await foreach (PersistentThreadMessage threadMessage in messages)
                {
                    _logger.LogInformation("{InstanceId} : Thread message created at {Timestamp} - {Role}: ", _instanceId, threadMessage.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), threadMessage.Role);
                    foreach (MessageContent contentItem in threadMessage.ContentItems)
                    {
                        if (contentItem is MessageTextContent textItem)
                        {
                            _logger.LogInformation("{InstanceId} : {Text}", _instanceId, textItem.Text);
                            if (threadMessage.Role == MessageRole.Agent)
                            {
                                // TODO Should add logic here to make sure it follows the actual json schema
                                resultJson = textItem.Text;
                            }
                        }
                    }
                }
                _logger.LogInformation("{InstanceId} : AnalyzeDocumentAsync completed", _instanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{InstanceId} : Error during AnalyzeDocumentAsync", _instanceId);
                throw;
            }
            finally
            {
                if (uploadedAgentFile != null && uploadedAgentFile.Value != null)
                {
                    await _persistentAgentsClient.Files.DeleteFileAsync(uploadedAgentFile.Value.Id);
                    _logger.LogInformation("{InstanceId} : Deleted File with ID: {FileId}", _instanceId, uploadedAgentFile.Value.Id);
                }
            }

            return resultJson;
        }



        private string LoadPrompt(string fileName)
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(basePath, "Prompts", fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Prompt file not found: {path}");
            }
            return File.ReadAllText(path);
        }

        public void Dispose()
        {
            try
            {
                if (_agent != null)
                {
                    _persistentAgentsClient.Administration.DeleteAgentAsync(_agent.Id).GetAwaiter().GetResult();
                    _logger.LogInformation("{InstanceId} : Deleted Agent with ID: {AgentId}", _instanceId, _agent.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{InstanceId} : Error deleting agent during Dispose", _instanceId);
            }
        }
    }
}