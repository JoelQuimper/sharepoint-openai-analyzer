using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Files;
using OpenAI.Responses;
using OpenAI.VectorStores;

namespace TestConsole;

public class TestAgentNew
{
    public static async Task RunFileSearchToolTestAsync(IConfigurationRoot configuration)
    {
        // For an unknown reason, this test fail with pdf for now.  I am trying to fix.  It work with other txtt formats.
        string projectEndpoint = configuration["AIFoundryEndpoint"];
        var modelName = configuration["ModelDeployement"];
        var filePath = configuration["LocalFilePath_PDF"];

        string agentName = "TestVectorStoreAgent";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting new agent {agentName}...  File to be uploaded: {filePath}, run identifier: {uniqueId}");

        AIProjectClient projectClient = new(new Uri(projectEndpoint), new DefaultAzureCredential());

        // Upload a file to be used in the VectorStore tool
        var uploadedFile = await projectClient.OpenAI.GetOpenAIFileClient().UploadFileAsync(
            File.OpenRead(filePath),
            filename: $"{Path.GetFileNameWithoutExtension(filePath)}-{uniqueId}{Path.GetExtension(filePath)}",
            FileUploadPurpose.Assistants
        );
        Console.WriteLine($"Uploaded file with ID: {uploadedFile.Value.Id}");

        // Create the VectorStore and provide it with uploaded file ID.
        VectorStoreCreationOptions vectorStoreOptions = new()
        {
            Name = $"index_{agentName}_{uniqueId}",
            FileIds = { uploadedFile.Value.Id },
        };
        var vectorStore = await projectClient.OpenAI.GetVectorStoreClient().CreateVectorStoreAsync(options: vectorStoreOptions);
        Console.WriteLine($"Created VectorStore with ID: {vectorStore.Value.Id}");
        
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
        Console.WriteLine($"Created agent with name: {result.Value.Name} and version: {result.Value.Version}");

        // Optional Step: Create a conversation to use with the agent
        ProjectConversation conversation = await projectClient.OpenAI.Conversations.CreateProjectConversationAsync();

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
        Console.WriteLine(response.GetOutputText());

        // Cleanup - delete the agent and the vector store
        await projectClient.Agents.DeleteAgentAsync(result.Value.Name);
        Console.WriteLine($"Deleted agent with name: {result.Value.Name}");
        await projectClient.OpenAI.GetVectorStoreClient().DeleteVectorStoreAsync(vectorStore.Value.Id);
        Console.WriteLine($"Deleted VectorStore with ID: {vectorStore.Value.Id}");
        await projectClient.OpenAI.GetOpenAIFileClient().DeleteFileAsync(uploadedFile.Value.Id);
        Console.WriteLine($"Deleted file with ID: {uploadedFile.Value.Id}");
    }

    public static async Task RunImageAnalysisToolTestAsync(IConfigurationRoot configuration)
    {
        string projectEndpoint = configuration["AIFoundryEndpoint"];
        var modelName = configuration["ModelDeployement"];
        var filePath = configuration["LocalFilePath_JPG"];

        string agentName = "TestImageAgent";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting test for {agentName}...  File to be uploaded: {filePath}, run identifier: {uniqueId}");

        AIProjectClient projectClient = new(new Uri(projectEndpoint), new DefaultAzureCredential());

        // Get file as BinaryData for upload
        var fileData = BinaryData.FromStream(File.OpenRead(filePath));

        // Create the agent with the file-search tool that uses the VectorStore created above.
        var agentDefinition = new PromptAgentDefinition(model: modelName)
        {
            Instructions = @"You are a helpful assistant that can answer questions about the content of an image provided 
                by the user. Answer the user's question based on that image information."
        };

        var result = await projectClient.Agents.CreateAgentVersionAsync(
            agentName: $"{agentName}-{uniqueId}",
            options: new AgentVersionCreationOptions(agentDefinition)
        );
        Console.WriteLine($"Created agent with name: {result.Value.Name} and version: {result.Value.Version}");

        // Optional Step: Create a conversation to use with the agent
        ProjectConversation conversation = await projectClient.OpenAI.Conversations.CreateProjectConversationAsync();

        ProjectResponsesClient responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
            defaultAgent: result.Value.Name,
            defaultConversationId: conversation.Id);
        
        CreateResponseOptions options = new CreateResponseOptions
        {
            InputItems =
            {
                ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart("Please analyze and summarize the attached image."),
                    ResponseContentPart.CreateInputImagePart(fileData, "image/jpeg")
                ])
            },
        };

        // Chat with the agent to answer questions
        ResponseResult response = await responsesClient.CreateResponseAsync(options);
        Console.WriteLine(response.GetOutputText());

        // Cleanup - delete the agent and the vector store
        await projectClient.Agents.DeleteAgentAsync(result.Value.Name);
        Console.WriteLine($"Deleted agent with name: {result.Value.Name}");
    }

    public static async Task RunFileAnalysisToolTestAsync(IConfigurationRoot configuration)
    {
        string projectEndpoint = configuration["AIFoundryEndpoint"];
        var modelName = configuration["ModelDeployement"];
        var filePath = configuration["LocalFilePath_PDF"];
        // In my test, this was only working using pdf. Maybe other models/config would behave differently, 
        // but I am hardcoding this for now to make sure the test runs. We can make this more dynamic later.
        var fileType = "application/pdf";

        string agentName = "TestFileAgent";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting test for {agentName}...  File to be uploaded: {filePath}, run identifier: {uniqueId}");

        AIProjectClient projectClient = new(new Uri(projectEndpoint), new DefaultAzureCredential());

        // Get file as BinaryData for upload
        var fileData = BinaryData.FromStream(File.OpenRead(filePath));
               
        // Create the agent with the file-search tool that uses the VectorStore created above.
        var agentDefinition = new PromptAgentDefinition(model: modelName)
        {
            Instructions = @"You are a helpful assistant that can answer questions about the content of the file provided 
                by the user. Answer the user's question based on that file information."
        };

        var result = await projectClient.Agents.CreateAgentVersionAsync(
            agentName: $"{agentName}-{uniqueId}",
            options: new AgentVersionCreationOptions(agentDefinition)
        );
        Console.WriteLine($"Created agent with name: {result.Value.Name} and version: {result.Value.Version}");

        // Optional Step: Create a conversation to use with the agent
        ProjectConversation conversation = await projectClient.OpenAI.Conversations.CreateProjectConversationAsync();

        ProjectResponsesClient responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
            defaultAgent: result.Value.Name,
            defaultConversationId: conversation.Id);
        
        CreateResponseOptions options = new CreateResponseOptions
        {
            InputItems =
            {
                ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart("Please analyze and summarize the attached file available."),
                    ResponseContentPart.CreateInputFilePart(fileData, fileType, Path.GetFileName(filePath))
                ])
            },
        };

        // Chat with the agent to answer questions
        ResponseResult response = await responsesClient.CreateResponseAsync(options);
        Console.WriteLine(response.GetOutputText());

        // Cleanup - delete the agent and the vector store
        await projectClient.Agents.DeleteAgentAsync(result.Value.Name);
        Console.WriteLine($"Deleted agent with name: {result.Value.Name}");
    }    
}
