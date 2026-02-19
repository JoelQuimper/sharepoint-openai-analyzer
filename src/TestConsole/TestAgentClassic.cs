using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace TestConsole;

public static class TestAgentClassic
{
    public static async Task RunAIFoundryTestVectorStoreAsync(IConfigurationRoot configuration)
    {
        var masterFilePath = configuration["LocalFilePath_PDF"];
        var endpoint = configuration["AIFoundryEndpoint"];
        var deployment = configuration["ModelDeployement"];
        
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting vector store test with Unique ID: {uniqueId}");

        var systemPrompt = "You are a helpful assistant. Analyze documents using file_search and provide clear responses.";
        var userPrompt = "Please analyze this document and provide a summary.";

        var credential = new DefaultAzureCredential();

        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
        var agentsClient = projectClient.GetPersistentAgentsClient();

        var uploadedAgentFile = await agentsClient.Files.UploadFileAsync(
            filePath: masterFilePath,
            purpose: PersistentAgentFilePurpose.Agents);
        Console.WriteLine($"Uploaded file. File ID: {uploadedAgentFile.Value.Id}, Filename: {uploadedAgentFile.Value.Filename}");

        var vectorStore = await agentsClient.VectorStores.CreateVectorStoreAsync(
            fileIds: new List<string> { uploadedAgentFile.Value.Id },
            name: $"test_vector_store_{uniqueId}");
        Console.WriteLine($"Created Vector Store. Vector Store ID: {vectorStore.Value.Id}, Name: {vectorStore.Value.Name}");

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
        Console.WriteLine($"Created Agent. Agent ID: {agent.Value.Id}, Name: {agent.Value.Name}");
        
        var thread = await agentsClient.Threads.CreateThreadAsync(toolResources: toolResources);
        
        await agentsClient.Messages.CreateMessageAsync(
            thread.Value.Id,
            MessageRole.User,
            userPrompt);
        Console.WriteLine($"Created Thread. Thread ID: {thread.Value.Id}");

        ThreadRun run = await agentsClient.Runs.CreateRunAsync(
            thread.Value.Id,
            agent.Value.Id);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            run = await agentsClient.Runs.GetRunAsync(thread.Value.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        Console.WriteLine($"Run completed with status: {run.Status}");
        
        var messages = agentsClient.Messages.GetMessagesAsync(
            threadId: thread.Value.Id,
            order: ListSortOrder.Ascending);

        await foreach (PersistentThreadMessage threadMessage in messages)
        {
            Console.WriteLine($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role}: ");
            foreach (MessageContent contentItem in threadMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    Console.WriteLine(textItem.Text);
                }
            }
        }

        // clean up resources
        await agentsClient.Administration.DeleteAgentAsync(agent.Value.Id);
        Console.WriteLine($"Deleted Agent with ID: {agent.Value.Id}");
        await agentsClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Value.Id);
        Console.WriteLine($"Deleted Vector Store with ID: {vectorStore.Value.Id}");
        await agentsClient.Files.DeleteFileAsync(uploadedAgentFile.Value.Id);
        Console.WriteLine($"Deleted file with ID: {uploadedAgentFile.Value.Id}");

        Console.WriteLine("Vector store test completed.");
    }

    public static async Task RunAIFoundryTestCodeInterpreterAsync(IConfigurationRoot configuration)
    {
        var masterFilePath = configuration["LocalFilePath_PDF"];
        var endpoint = configuration["AIFoundryEndpoint"];
        var deployment = configuration["ModelDeployement"];
        
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting code interpreter test with Unique ID: {uniqueId}");

        var systemPrompt = "You are a helpful assistant. Analyze documents using configured tools and provide clear responses.";
        var userPrompt = "Please analyze this document and provide a summary.";

        var credential = new DefaultAzureCredential();

        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
        var agentsClient = projectClient.GetPersistentAgentsClient();

        var uploadedAgentFile = await agentsClient.Files.UploadFileAsync(
            filePath: masterFilePath,
            purpose: PersistentAgentFilePurpose.Agents);
        Console.WriteLine($"Uploaded file. File ID: {uploadedAgentFile.Value.Id}, Filename: {uploadedAgentFile.Value.Filename}");

        var toolResources = new ToolResources() { CodeInterpreter = new CodeInterpreterToolResource() };
        var tools = new List<ToolDefinition> { new CodeInterpreterToolDefinition() };
        
        var agent = await agentsClient.Administration.CreateAgentAsync(
            model: deployment,
            name: $"Test Code Interpreter Agent {uniqueId}",
            instructions: systemPrompt,
            temperature: (float?)0.0,
            tools: tools,
            toolResources: toolResources
        );
        Console.WriteLine($"Created Agent. Agent ID: {agent.Value.Id}, Name: {agent.Value.Name}");

        var attachment = new MessageAttachment(
            fileId: uploadedAgentFile.Value.Id,
            tools: tools
        );       
        
        var thread = await agentsClient.Threads.CreateThreadAsync(toolResources: toolResources);
        
        await agentsClient.Messages.CreateMessageAsync(
            threadId: thread.Value.Id,
            role: MessageRole.User,
            content: userPrompt,
            attachments: new List<MessageAttachment> { attachment });
        Console.WriteLine($"Created Thread. Thread ID: {thread.Value.Id}");

        ThreadRun run = await agentsClient.Runs.CreateRunAsync(
            thread.Value.Id,
            agent.Value.Id);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            run = await agentsClient.Runs.GetRunAsync(thread.Value.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        Console.WriteLine($"Run completed with status: {run.Status}");
        
        var messages = agentsClient.Messages.GetMessagesAsync(
            threadId: thread.Value.Id,
            order: ListSortOrder.Ascending);

        await foreach (PersistentThreadMessage threadMessage in messages)
        {
            Console.WriteLine($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role}: ");
            foreach (MessageContent contentItem in threadMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    Console.WriteLine(textItem.Text);
                }
            }
        }

        // clean up resources
        await agentsClient.Administration.DeleteAgentAsync(agent.Value.Id);
        Console.WriteLine($"Deleted Agent with ID: {agent.Value.Id}");
        await agentsClient.Files.DeleteFileAsync(uploadedAgentFile.Value.Id);
        Console.WriteLine($"Deleted file with ID: {uploadedAgentFile.Value.Id}");

        Console.WriteLine("Code interpreter test completed.");
    }

    public static async Task CleanUpAIFoundryProjectAsync(IConfigurationRoot configuration)
    {
        var endpoint = configuration["AIFoundryEndpoint"];
        Console.WriteLine($"Starting cleanup of AI Foundry project resources in {endpoint}...");

        var credential = new DefaultAzureCredential();
        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
        var agentsClient = projectClient.GetPersistentAgentsClient();

        await foreach (var agent in agentsClient.Administration.GetAgentsAsync())
        {
            Console.WriteLine($"Deleting Agent: {agent.Id}, Name: {agent.Name}");
            await agentsClient.Administration.DeleteAgentAsync(agent.Id);
        }

        await foreach (var vectorStore in agentsClient.VectorStores.GetVectorStoresAsync())
        {
            Console.WriteLine($"Deleting Vector Store: {vectorStore.Id}, Name: {vectorStore.Name}");
            await agentsClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Id);
        }

        var files = await agentsClient.Files.GetFilesAsync();
        foreach (var file in files.Value)
        {
            Console.WriteLine($"Deleting File: {file.Id}, Filename: {file.Filename}");
            await agentsClient.Files.DeleteFileAsync(file.Id);
        }

        Console.WriteLine("Cleanup completed.");
    }
}
