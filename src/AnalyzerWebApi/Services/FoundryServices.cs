using AnalyzerWebApi.Services;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using OpenAI.Files;
using OpenAI.Responses;
using OpenAI.VectorStores;

public class FoundryServices : IFoundryServices
{
    private readonly string _endpoint;
    private readonly string _baseAgentName;
    private readonly AIProjectClient _projectClient;
    private readonly ProjectResponsesClient _baseResponseClient;
    private readonly ILogger<FoundryServices> _logger;

    public FoundryServices(string endpoint, string baseAgentName, ILogger<FoundryServices> logger)
    {
        _endpoint = endpoint;
        _baseAgentName = baseAgentName;
        _logger = logger;

        _logger.LogDebug("Initializing FoundryServices with Endpoint: {Endpoint}, BaseAgentName: {BaseAgentName}", _endpoint, _baseAgentName);

        _projectClient = new AIProjectClient(new Uri(_endpoint), new DefaultAzureCredential());

        _baseResponseClient = _projectClient.OpenAI.GetProjectResponsesClientForAgent(_baseAgentName);
        _logger.LogDebug("BaseResponseClient initialized for agent {BaseAgentName}", _baseAgentName);
    }

    public async Task<string> AnalyzeDocumentWithFileSearchAsync(BinaryData documentBytes, string fileName, string documentMimeType, string expectedJsonSchema, string userInstructions)
    {
        _logger.LogInformation("Starting AnalyzeDocumentWithFileSearchAsync");
        
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting new agent from {_baseAgentName}...  File to be uploaded: {fileName}, run identifier: {uniqueId}");

        // Upload a file to be used in the VectorStore tool
        var uploadedFile = await _projectClient.OpenAI.GetOpenAIFileClient().UploadFileAsync(
            documentBytes.ToStream(),
            filename: $"{Path.GetFileNameWithoutExtension(fileName)}-{uniqueId}{Path.GetExtension(fileName)}",
            FileUploadPurpose.Assistants
        );
        _logger.LogDebug("Uploaded file with ID: {FileId}", uploadedFile.Value.Id);

        // Create the VectorStore and provide it with uploaded file ID.
        VectorStoreCreationOptions vectorStoreOptions = new()
        {
            Name = $"index_{_baseAgentName}_{uniqueId}",
            FileIds = { uploadedFile.Value.Id },
        };
        var vectorStore = await _projectClient.OpenAI.GetVectorStoreClient().CreateVectorStoreAsync(options: vectorStoreOptions);
        _logger.LogDebug("Created VectorStore with ID: {VectorStoreId}", vectorStore.Value.Id);
        
        // Create the agent with the file-search tool that uses the VectorStore created above.
        var baseAgentRecord = await _projectClient.Agents.GetAgentAsync(_baseAgentName);
        var baseAgentDefinition = (PromptAgentDefinition)baseAgentRecord.Value.GetLatestVersion().Definition;
        var newAgentDefinition = new PromptAgentDefinition(model: baseAgentDefinition.Model)
        {
            Instructions = baseAgentDefinition.Instructions,
            Temperature = baseAgentDefinition.Temperature,
            Tools =
            {
                ResponseTool.CreateFileSearchTool(
                    vectorStoreIds: new List<string> { vectorStore.Value.Id }
                )
            }
        };

        var newAgentVersion = await _projectClient.Agents.CreateAgentVersionAsync(
            agentName: $"{_baseAgentName}-{uniqueId}",
            options: new AgentVersionCreationOptions(newAgentDefinition)
        );
        var newAgentRecord = await _projectClient.Agents.GetAgentAsync(newAgentVersion.Value.Name);
        Console.WriteLine($"Created agent with name: {newAgentVersion.Value.Name} and version: {newAgentVersion.Value.Version}");

        ProjectResponsesClient responsesClient = _projectClient.OpenAI.GetProjectResponsesClientForAgent(newAgentVersion.Value.Name);
        
        var prompt = @$"You are a helpful assistant that can answer questions about the content of the image you have in your knowledge and 
            that you can access using the file_search tool. Use the file_search tool to find relevant information in the file and answer the 
            user's question based on that information.
            The image is called {fileName} and is in the following format: {documentMimeType}.
            Here are the user instructions for analyzing the image: {userInstructions}.
            Please provide your answer in a JSON format that adheres to the following schema: {expectedJsonSchema}";
        
        CreateResponseOptions options = new CreateResponseOptions
        {
            InputItems =
            {
                ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart(prompt),
                ])
            },
        };

        // Chat with the agent to answer questions
        ResponseResult response = await responsesClient.CreateResponseAsync(options);

        // Cleanup - delete the agent and the vector store
        await _projectClient.Agents.DeleteAgentAsync(newAgentVersion.Value.Name);
        Console.WriteLine($"Deleted agent with name: {newAgentVersion.Value.Name}");
        await _projectClient.OpenAI.GetVectorStoreClient().DeleteVectorStoreAsync(vectorStore.Value.Id);
        Console.WriteLine($"Deleted VectorStore with ID: {vectorStore.Value.Id}");
        await _projectClient.OpenAI.GetOpenAIFileClient().DeleteFileAsync(uploadedFile.Value.Id);
        Console.WriteLine($"Deleted file with ID: {uploadedFile.Value.Id}");

        return response.GetOutputText();
    }

    public async Task<string> AnalyzeDocumentAsync(BinaryData documentBytes, string fileName, string documentMimeType, string expectedJsonSchema, string userInstructions)
    {
        _logger.LogInformation("Starting AnalyzeDocumentAsync");
        var prompt = @$"You are a helpful assistant that can analyze the content of the document provided in the input and answer questions about it.
                The document is called {fileName} and is in the following format: {documentMimeType}.
                Here are the user instructions for analyzing the document: {userInstructions}.
                Please provide your answer in a JSON format that adheres to the following schema: {expectedJsonSchema}";
        CreateResponseOptions options = new CreateResponseOptions
        {
            InputItems =
            {
                ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart(prompt),
                    ResponseContentPart.CreateInputFilePart(documentBytes, documentMimeType, fileName)
                ])
            },
        };

        // Chat with the agent to answer questions
        ResponseResult response = await _baseResponseClient.CreateResponseAsync(options);
        _logger.LogInformation("AnalyzeDocumentAsync completed");

        return response.GetOutputText();
    }

    public async Task<string> AnalyzeImageAsync(BinaryData documentBytes, string fileName, string documentMimeType, string expectedJsonSchema, string userInstructions)
    {
        _logger.LogInformation("Starting AnalyzeImageAsync");
        var prompt = @$"You are a helpful assistant that can analyze the content of the image provided in the input and answer questions about it.
                The image is called {fileName} and is in the following format: {documentMimeType}.
                Here are the user instructions for analyzing the image: {userInstructions}.
                Please provide your answer in a JSON format that adheres to the following schema: {expectedJsonSchema}";
        CreateResponseOptions options = new CreateResponseOptions
        {
            InputItems =
            {
                ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart(prompt),
                    ResponseContentPart.CreateInputImagePart(documentBytes)
                ])
            },
        };

        // Chat with the agent to answer questions
        ResponseResult response = await _baseResponseClient.CreateResponseAsync(options);
        _logger.LogInformation("AnalyzeImageAsync completed");

        return response.GetOutputText();
    }
}