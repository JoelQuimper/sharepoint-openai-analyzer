using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace TestConsole;

public class Tests
{
    private string GetSystemPrompt()
    {
        return @"You are a document extraction agent. Your task is to analyze documents and extract data in JSON format.

            CRITICAL INSTRUCTION:
            Return ONLY a valid JSON object that strictly follows the provided schema. Nothing else.

            SCHEMA COMPLIANCE:
            - All fields must match the schema exactly
            - All type constraints must be respected (string, integer, number, date format)
            - All value constraints must be respected (minimum, maximum, format)
            - Required fields must be present
            - If you cannot find a value in the document, set to null
            - Do NOT hallucinate or infer values not explicitly in the document

            OUTPUT:
            Return ONLY the JSON object that validates against the schema. No explanations, no markdown, no code blocks.";
    }

    private string GetUserPrompt()
    {
        return $@"Document Type: Invoice. Extract all invoice information from the attached document";
    }

    private string GetJsonSchema()
    {
        return @"{
            ""type"": ""object"",
            ""description"": ""Invoice extraction schema with strict validation"",
            ""properties"": {
                ""invoiceNumber"": { 
                    ""type"": ""string"",
                    ""description"": ""Invoice number or ID from the document""
                },
                ""invoiceDate"": { 
                    ""type"": ""string"", 
                    ""pattern"": ""^\\d{4}-\\d{2}-\\d{2}$"",
                    ""description"": ""Invoice date in YYYY-MM-DD format""
                },
                ""purchaseOrderNumber"": { 
                    ""type"": ""integer"", 
                    ""minimum"": 160000000, 
                    ""maximum"": 160999999,
                    ""description"": ""PO number - must be integer in range [160000000, 160999999]""
                },
                ""supplierName"": { 
                    ""type"": ""string"",
                    ""description"": ""Vendor or supplier name from the invoice""
                },
                ""totalAmount"": { 
                    ""type"": ""number"", 
                    ""minimum"": 0,
                    ""description"": ""Total invoice amount - non-negative number""
                }
            },
            ""required"": [ ""invoiceNumber"", ""invoiceDate"", ""purchaseOrderNumber"", ""supplierName"", ""totalAmount"" ]
        }";
    }

    public async Task RunAIFoundryTestVectorStoreAsync(IConfigurationRoot configuration)
    {
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting document processing with Unique ID: {uniqueId}");

        var masterFilePath = configuration["LocalFilePath"];
        var copiedFilePath = Path.Combine(
            Path.GetDirectoryName(masterFilePath),
            $"{Path.GetFileNameWithoutExtension(masterFilePath)}_{uniqueId}{Path.GetExtension(masterFilePath)}");
        
        File.Copy(masterFilePath, copiedFilePath);
        Console.WriteLine($"Copied file to: {copiedFilePath}");

        // Define document type and schema
        var systemPrompt = GetSystemPrompt();
        var userPrompt = GetUserPrompt();

        var endpoint = configuration["AIFoundryEndpoint"];
        var deployment = "gpt-4.1";
        var credential = new DefaultAzureCredential();

        var projectClient = new AIProjectClient(new Uri(endpoint), credential);

        var agentsClient = projectClient.GetPersistentAgentsClient();

        var uploadedAgentFile = await agentsClient.Files.UploadFileAsync(
            filePath: copiedFilePath,
            purpose: PersistentAgentFilePurpose.Agents);
        Console.WriteLine($"Uploaded file. File ID: {uploadedAgentFile.Value.Id}, Filename: {uploadedAgentFile.Value.Filename}");

        var vectorStore = await agentsClient.VectorStores.CreateVectorStoreAsync(
            fileIds:  new List<string> { uploadedAgentFile.Value.Id },
            name: $"my_vector_store_{uniqueId}");
        Console.WriteLine($"Created Vector Store. Vector Store ID: {vectorStore.Value.Id}, Name: {vectorStore.Value.Name}");

        var fileSearchToolResource = new FileSearchToolResource();
        fileSearchToolResource.VectorStoreIds.Add(vectorStore.Value.Id);
        var toolResources = new ToolResources() { FileSearch = fileSearchToolResource };
        
        var agent = await agentsClient.Administration.CreateAgentAsync(
            model: deployment,
            name: $"My Test Agent {uniqueId}",
            instructions: systemPrompt,
            temperature: (float?)0.0
        );

        agent = await agentsClient.Administration.UpdateAgentAsync(
            agent.Value.Id,
            tools: new List<ToolDefinition> { new FileSearchToolDefinition() },
            toolResources: toolResources
        );
        Console.WriteLine($"Created Agent. Agent ID: {agent.Value.Id}, Name: {agent.Value.Name}");
        
        var thread = await agentsClient.Threads.CreateThreadAsync(
            toolResources: toolResources);
        
        await agentsClient.Messages.CreateMessageAsync(
            thread.Value.Id,
            MessageRole.User,
            userPrompt);
        Console.WriteLine($"Created Thread. Thread ID: {thread.Value.Id}");

        //await Task.Delay(TimeSpan.FromSeconds(2));

        ThreadRun run = await agentsClient.Runs.CreateRunAsync(
            thread.Value.Id,
            agent.Value.Id);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            run = await agentsClient.Runs.GetRunAsync(thread.Value.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued
            || run.Status == RunStatus.InProgress);

        Console.WriteLine($"Run completed with status: {run.Status}");
        
        var messages = agentsClient.Messages.GetMessagesAsync(
                threadId: thread.Value.Id, order: ListSortOrder.Ascending);

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

        // await agentsClient.Administration.DeleteAgentAsync(agent.Value.Id);
        // Console.WriteLine($"Deleted Agent with ID: {agent.Value.Id}");

        // await agentsClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Value.Id);
        // Console.WriteLine($"Deleted Vector Store with ID: {vectorStore.Value.Id}");
        
        // await agentsClient.Files.DeleteFileAsync(uploadedAgentFile.Value.Id);
        // Console.WriteLine($"Deleted File with ID: {uploadedAgentFile.Value.Id}");

        File.Delete(copiedFilePath);
        Console.WriteLine($"Deleted File local copy at: {copiedFilePath}");

        Console.WriteLine("Document processing completed.");
    }

    public async Task RunAIFoundryTestCodeInterpreterAsync(IConfigurationRoot configuration)
    {
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting document processing with Unique ID: {uniqueId}");

        var masterFilePath = configuration["LocalFilePath"];
        var copiedFilePath = Path.Combine(
            Path.GetDirectoryName(masterFilePath),
            $"{Path.GetFileNameWithoutExtension(masterFilePath)}_{uniqueId}{Path.GetExtension(masterFilePath)}");
        
        File.Copy(masterFilePath, copiedFilePath);
        Console.WriteLine($"Copied file to: {copiedFilePath}");

        // Define document type and schema
        var systemPrompt = GetSystemPrompt();
        var userPrompt = GetUserPrompt();

        var endpoint = configuration["AIFoundryEndpoint"];
        var deployment = "gpt-4.1";
        var credential = new DefaultAzureCredential();

        var projectClient = new AIProjectClient(new Uri(endpoint), credential);

        var agentsClient = projectClient.GetPersistentAgentsClient();

        var uploadedAgentFile = await agentsClient.Files.UploadFileAsync(
            filePath: copiedFilePath,
            purpose: PersistentAgentFilePurpose.Agents);
        Console.WriteLine($"Uploaded file. File ID: {uploadedAgentFile.Value.Id}, Filename: {uploadedAgentFile.Value.Filename}");

        List<ToolDefinition> tools = new List<ToolDefinition> { new CodeInterpreterToolDefinition() };
        
        var agent = await agentsClient.Administration.CreateAgentAsync(
            model: deployment,
            name: $"my ci Agent {uniqueId}",
            instructions: systemPrompt,
            temperature: (float?)0.0,
            tools: tools
        );

        var attachment = new MessageAttachment(
            fileId: uploadedAgentFile.Value.Id,
            tools: tools
        );

        // Configure tool resources for code interpreter
        var codeInterpreterToolResource = new CodeInterpreterToolResource();
        var toolResources = new ToolResources() { CodeInterpreter = codeInterpreterToolResource };

        // Update agent with tool resources
        agent = await agentsClient.Administration.UpdateAgentAsync(
            agent.Value.Id,
            toolResources: toolResources
        );

        Console.WriteLine($"Created Agent. Agent ID: {agent.Value.Id}, Name: {agent.Value.Name}");
        
        var thread = await agentsClient.Threads.CreateThreadAsync(
            toolResources: toolResources);
        
        await agentsClient.Messages.CreateMessageAsync(
            threadId: thread.Value.Id,
            role: MessageRole.User,
            content: userPrompt,
            attachments: new List<MessageAttachment> { attachment });
        Console.WriteLine($"Created Thread. Thread ID: {thread.Value.Id}");

        //await Task.Delay(TimeSpan.FromSeconds(2));

        ThreadRun run = await agentsClient.Runs.CreateRunAsync(
            thread.Value.Id,
            agent.Value.Id);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            run = await agentsClient.Runs.GetRunAsync(thread.Value.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued
            || run.Status == RunStatus.InProgress);

        Console.WriteLine($"Run completed with status: {run.Status}");
        
        var messages = agentsClient.Messages.GetMessagesAsync(
                threadId: thread.Value.Id, order: ListSortOrder.Ascending);

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

        // await agentsClient.Administration.DeleteAgentAsync(agent.Value.Id);
        // Console.WriteLine($"Deleted Agent with ID: {agent.Value.Id}");

        // await agentsClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Value.Id);
        // Console.WriteLine($"Deleted Vector Store with ID: {vectorStore.Value.Id}");
        
        // await agentsClient.Files.DeleteFileAsync(uploadedAgentFile.Value.Id);
        // Console.WriteLine($"Deleted File with ID: {uploadedAgentFile.Value.Id}");

        File.Delete(copiedFilePath);
        Console.WriteLine($"Deleted File local copy at: {copiedFilePath}");

        Console.WriteLine("Document processing completed.");
    }

    public async Task RunAIFoundryTestVisionAsync(IConfigurationRoot configuration)
    {
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        Console.WriteLine($"Starting document processing with Unique ID: {uniqueId}");

        var masterFilePath = configuration["LocalFilePath_PDF"];
        var copiedFilePath = Path.Combine(
            Path.GetDirectoryName(masterFilePath),
            $"{Path.GetFileNameWithoutExtension(masterFilePath)}_{uniqueId}{Path.GetExtension(masterFilePath)}");
        
        File.Copy(masterFilePath, copiedFilePath);
        Console.WriteLine($"Copied file to: {copiedFilePath}");

        var endpoint = configuration["AIFoundryEndpoint"];
        var deployment = "gpt-4.1";
        var credential = new DefaultAzureCredential();
        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
        var client = projectClient.GetPersistentAgentsClient();

        // Upload the image file first
        var uploadedAgentFile = await client.Files.UploadFileAsync(
            filePath: copiedFilePath,
            purpose: PersistentAgentFilePurpose.Agents);
        Console.WriteLine($"Uploaded file. File ID: {uploadedAgentFile.Value.Id}, Filename: {uploadedAgentFile.Value.Filename}");

        // Create the persistent agent for vision with vision tool
        var visionTools = new List<ToolDefinition> { new CodeInterpreterToolDefinition() };
        var agentResponse = await client.Administration.CreateAgentAsync(
            model: deployment,
            name: "test-vision",
            instructions: "You are an expert invoice analyzer. Using the attached pdf, extract and analyze key information including invoice number, date, vendor name, items, quantities, prices, and total amount. Provide clear, structured responses as JSON.",
            tools: visionTools
        );
        var agent = agentResponse.Value;
        Console.WriteLine($"Created Agent. Agent ID: {agent.Id}, Name: {agent.Name}");

        // Create a thread for the conversation
        var thread = await client.Threads.CreateThreadAsync();
        Console.WriteLine($"Created Thread. Thread ID: {thread.Value.Id}");

        // Create a message with the image attachment using the fileId with CodeInterpreter tool
        var attachment = new MessageAttachment(
            fileId: uploadedAgentFile.Value.Id,
            tools: visionTools
        );
        
        await client.Messages.CreateMessageAsync(
            threadId: thread.Value.Id,
            role: MessageRole.User,
            content: @$"Please analyze this invoice pdf and extract all key information including: invoice number, date, vendor name, po number and total amount. Format as JSON following this schema:
            {GetJsonSchema()}",
            attachments: new List<MessageAttachment> { attachment }
        );

        // Run the agent
        var runResponse = await client.Runs.CreateRunAsync(
            thread.Value.Id,
            agent.Id
        );
        var run = runResponse.Value;

        // Wait for the run to complete
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
        {
            await Task.Delay(1000);
            var runUpdate = await client.Runs.GetRunAsync(thread.Value.Id, run.Id);
            run = runUpdate.Value;
        }

        Console.WriteLine($"Run completed with status: {run.Status}");

        // Retrieve the response
        var messages = client.Messages.GetMessagesAsync(
            threadId: thread.Value.Id,
            order: ListSortOrder.Ascending
        );

        string result = null;
        await foreach (PersistentThreadMessage message in messages)
        {
            Console.WriteLine($"{message.CreatedAt:yyyy-MM-dd HH:mm:ss} - {message.Role}: ");
            if (message.Role == MessageRole.Agent)
            {
                foreach (MessageContent content in message.ContentItems)
                {
                    if (content is MessageTextContent textContent)
                    {
                        result = textContent.Text;
                        Console.WriteLine(textContent.Text);
                    }
                }
            }
        }

        // Cleanup
        await client.Administration.DeleteAgentAsync(agent.Id);
        Console.WriteLine($"Deleted Agent with ID: {agent.Id}");
        
        await client.Files.DeleteFileAsync(uploadedAgentFile.Value.Id);
        Console.WriteLine($"Deleted File with ID: {uploadedAgentFile.Value.Id}");

        File.Delete(copiedFilePath);
        Console.WriteLine($"Deleted File local copy at: {copiedFilePath}");

        Console.WriteLine("Document processing completed.");
    }
    
    public async Task CleanUpAIFoundryProjectAsync(IConfigurationRoot configuration)
    {
        var endpoint = configuration["AIFoundryEndpoint"];
        var credential = new DefaultAzureCredential();

        Console.WriteLine("Starting cleanup of AI Foundry project resources...");

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
    public async Task RunCallApiAsync(IConfigurationRoot configuration)
    {
        // Replace these with your actual values
        string tenantId = configuration["TenantId"];
        string clientId = configuration["ClientId"];
        string clientSecret = configuration["ClientSecret"];
        string driveId = configuration["DriveId"];
        string driveItemId = configuration["DriveItemId_JPG"];
        string scope = configuration["Scope"];
        var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

        var httpClient = new HttpClient();

        var requestBody = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "scope", scope },
            { "client_secret", clientSecret },
            { "grant_type", "client_credentials" }
        };

        var requestContent = new FormUrlEncodedContent(requestBody);

        var response = await httpClient.PostAsync(tokenEndpoint, requestContent);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        string accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();

        var apiUrl = "https://localhost:7284/DocumentAnalyzer";
        // var apiUrl = "https://app-sharepoint-analyzer-api.azurewebsites.net/DocumentAnalyzer";

        var body = new
        {
            DriveId = driveId,
            DriveItemId = driveItemId,
            UserPrompt = GetUserPrompt(),
            ExpectedJsonSchema = GetJsonSchema()
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var apiResponse = await httpClient.PostAsJsonAsync(apiUrl, body);
        Console.WriteLine($"API Response Status: {apiResponse.StatusCode}");
        var apiResponseContent = await apiResponse.Content.ReadAsStringAsync();
        Console.WriteLine("API Response Content:");
        Console.WriteLine(apiResponseContent);
    }
}