using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using TestConsole.Infra;

namespace TestConsole.Tests.ClassicAgents;

public class VectorStoreAgentTest : BaseTest
{
    protected override string TestName => "RunAIFoundryTestVectorStoreAsync";

    public VectorStoreAgentTest(IConfigurationRoot configuration) : base(configuration)
    {
    }

    protected override async Task TestDefinitionAsync()
    {
        var masterFilePath = Configuration["LocalFilePath_PDF"];
        var endpoint = Configuration["AIFoundryEndpoint"];
        var deployment = Configuration["ModelDeployement"];

        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        LogInfo($"Starting vector store test with Unique ID: {uniqueId}");

        var systemPrompt = "You are a helpful assistant. Analyze documents using file_search and provide clear responses.";
        var userPrompt = "Please analyze this document and provide a summary.";

        var credential = new DefaultAzureCredential();

        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
        var agentsClient = projectClient.GetPersistentAgentsClient();

        var uploadedAgentFile = await agentsClient.Files.UploadFileAsync(
            filePath: masterFilePath,
            purpose: PersistentAgentFilePurpose.Agents);
        LogInfo($"Uploaded file. File ID: {uploadedAgentFile.Value.Id}, Filename: {uploadedAgentFile.Value.Filename}");

        var vectorStore = await agentsClient.VectorStores.CreateVectorStoreAsync(
            fileIds: new List<string> { uploadedAgentFile.Value.Id },
            name: $"test_vector_store_{uniqueId}");
        LogInfo($"Created Vector Store. Vector Store ID: {vectorStore.Value.Id}, Name: {vectorStore.Value.Name}");

        var fileSearchToolResource = new FileSearchToolResource();
        fileSearchToolResource.VectorStoreIds.Add(vectorStore.Value.Id);
        var toolResources = new ToolResources() { FileSearch = fileSearchToolResource };

        var agent = await agentsClient.Administration.CreateAgentAsync(
            model: deployment,
            name: $"Test Vector Store Agent {uniqueId}",
            instructions: systemPrompt,
            temperature: (float?)0.0,
            tools: new List<ToolDefinition> { new FileSearchToolDefinition() },
            toolResources: toolResources
        );
        LogInfo($"Created Agent. Agent ID: {agent.Value.Id}, Name: {agent.Value.Name}");

        var thread = await agentsClient.Threads.CreateThreadAsync(toolResources: toolResources);

        await agentsClient.Messages.CreateMessageAsync(
            thread.Value.Id,
            MessageRole.User,
            userPrompt);
        LogInfo($"Created Thread. Thread ID: {thread.Value.Id}");

        ThreadRun run = await agentsClient.Runs.CreateRunAsync(
            thread.Value.Id,
            agent.Value.Id);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            run = await agentsClient.Runs.GetRunAsync(thread.Value.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        LogInfo($"Run completed with status: {run.Status}");

        var messages = agentsClient.Messages.GetMessagesAsync(
            threadId: thread.Value.Id,
            order: ListSortOrder.Ascending);

        await foreach (PersistentThreadMessage threadMessage in messages)
        {
            LogInfo($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role}: ");
            foreach (MessageContent contentItem in threadMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    LogInfo(textItem.Text);
                }
            }
        }

        // clean up resources
        await agentsClient.Administration.DeleteAgentAsync(agent.Value.Id);
        LogInfo($"Deleted Agent with ID: {agent.Value.Id}");
        await agentsClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Value.Id);
        LogInfo($"Deleted Vector Store with ID: {vectorStore.Value.Id}");
        await agentsClient.Files.DeleteFileAsync(uploadedAgentFile.Value.Id);
        LogInfo($"Deleted file with ID: {uploadedAgentFile.Value.Id}");

        LogInfo("Vector store test completed.");
    }
}
