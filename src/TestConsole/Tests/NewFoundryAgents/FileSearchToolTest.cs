using System.ClientModel.Primitives;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Files;
using OpenAI.Responses;
using OpenAI.VectorStores;
using TestConsole.Infra;

namespace TestConsole.Tests.NewFoundryAgents;

public class FileSearchToolTest : BaseTest
{
    protected override string TestName => "RunFileSearchToolTestAsync";

    public FileSearchToolTest(IConfigurationRoot configuration) : base(configuration)
    {
    }

    protected override async Task TestDefinitionAsync()
    {
        string projectEndpoint = Configuration["AIFoundryEndpoint"];
        var modelName = Configuration["ModelDeployement"];
        var filePath = Configuration["LocalFilePath_PDF"];

        string agentName = "TestVectorStoreAgent";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        LogInfo($"Starting new agent {agentName}...  File to be uploaded: {filePath}, run identifier: {uniqueId}");

        AIProjectClientOptions clientOptions = new();
        clientOptions.AddPolicy(new LoggingPolicy(), PipelinePosition.PerCall);
        AIProjectClient projectClient = new(new Uri(projectEndpoint), new DefaultAzureCredential(), clientOptions);

        // Upload a file to be used in the VectorStore tool
        var uploadedFile = await projectClient.OpenAI.GetOpenAIFileClient().UploadFileAsync(
            File.OpenRead(filePath),
            filename: $"{Path.GetFileNameWithoutExtension(filePath)}-{uniqueId}{Path.GetExtension(filePath)}",
            FileUploadPurpose.Assistants
        );
        LogInfo($"Uploaded file with ID: {uploadedFile.Value.Id}");

        // Create the VectorStore and provide it with uploaded file ID.
        VectorStoreCreationOptions vectorStoreOptions = new()
        {
            Name = $"index_{agentName}_{uniqueId}",
            FileIds = { uploadedFile.Value.Id },
        };
        var vectorStore = await projectClient.OpenAI.GetVectorStoreClient().CreateVectorStoreAsync(options: vectorStoreOptions);
        LogInfo($"Created VectorStore with ID: {vectorStore.Value.Id}");

        // Create the agent with the file-search tool that uses the VectorStore created above.
        var agentDefinition = new PromptAgentDefinition(model: modelName)
        {
            Instructions = @"You are a helpful assistant that can answer questions about the content of the file provided 
                via the file_search tool. Use the file_search tool to find relevant information in the file and answer the 
                user's question based on that information.",
            Tools =
            {
                ResponseTool.CreateFileSearchTool(
                    vectorStoreIds: new List<string> { vectorStore.Value.Id }
                )
            }
        };

        var result = await projectClient.Agents.CreateAgentVersionAsync(
            agentName: $"{agentName}-{uniqueId}",
            options: new AgentVersionCreationOptions(agentDefinition)
        );
        LogInfo($"Created agent with name: {result.Value.Name} and version: {result.Value.Version}");

        // Optional Step: Create a conversation to use with the agent
        ProjectConversation conversation = await projectClient.OpenAI.GetProjectConversationsClient().CreateProjectConversationAsync();

        ProjectResponsesClient responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
            defaultAgent: result.Value.Name,
            defaultConversationId: conversation.Id);

        CreateResponseOptions options = new CreateResponseOptions
        {
            InputItems =
            {
                ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart("Please analyze and summarize the file available via the file-search tool."),
                ])
            },
        };

        // Chat with the agent to answer questions
        ResponseResult response = await responsesClient.CreateResponseAsync(options);
        LogInfo(response.GetOutputText());

        // Cleanup - delete the agent and the vector store
        await projectClient.Agents.DeleteAgentAsync(result.Value.Name);
        LogInfo($"Deleted agent with name: {result.Value.Name}");
        await projectClient.OpenAI.GetVectorStoreClient().DeleteVectorStoreAsync(vectorStore.Value.Id);
        LogInfo($"Deleted VectorStore with ID: {vectorStore.Value.Id}");
        await projectClient.OpenAI.GetOpenAIFileClient().DeleteFileAsync(uploadedFile.Value.Id);
        LogInfo($"Deleted file with ID: {uploadedFile.Value.Id}");
    }
}
