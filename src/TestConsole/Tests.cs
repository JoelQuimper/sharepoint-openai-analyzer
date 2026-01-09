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
        return @"You are a document metadata extraction agent. Your task is to analyze 
        documents from the vector store and extract structured data in JSON format.

        CRITICAL INSTRUCTIONS - MUST FOLLOW EXACTLY:
        1. YOU MUST use the file_search tool FIRST and ALWAYS to retrieve the actual document content
        2. ONLY extract data that you explicitly find in the retrieved document content
        3. NEVER make up, guess, or hallucinate any values - if a field is not in the document, return null
        4. When you cannot find a required field in the document, you MUST still set it to null rather than inventing data
        5. Do NOT rely on training data or examples - extract ONLY from the actual document retrieved
        6. If the document does not contain a field, state it is null even if you think you know what it should be
        7. Validate all extracted values strictly according to the schema constraints provided

        OUTPUT REQUIREMENTS:
        - Return ONLY valid JSON - no explanations, markdown formatting, or additional text
        - Do not wrap the JSON in code blocks or quotes
        - Include only the fields specified in the schema
        - Use null for any field not found in the actual document content

        ZERO TOLERANCE FOR HALLUCINATION:
        - This is non-negotiable: extract only from retrieved content
        - Do not infer or predict values
        - Do not use knowledge that is not in the document";
    }

    private string GetUserPrompt()
    {
        return $@"Document Type: Invoice

        Please analyze the document and extract all information according to the following JSON schema:

        JSON SCHEMA:
        {GetJsonSchema()}

        EXTRACTION INSTRUCTIONS:
        1. Use file_search to retrieve the document content
        2. Map the document fields to the schema fields
        3. Return ONLY the JSON object with no additional text or formatting";
    }

    private string GetJsonSchema()
    {
        return @"{
            ""type"": ""object"",
            ""properties"": {
                ""invoiceNumber"": { ""type"": ""string"" },
                ""invoiceDate"": { ""type"": ""string"", ""format"": ""date"" },
                ""purchaseOrderNumber"": { ""type"": ""integer"", ""minimum"": 160000000, ""maximum"": 160999999 },
                ""supplierName"": { ""type"": ""string"" },
                ""totalAmount"": { ""type"": ""number"", ""minimum"": 0 }
            },
            ""required"": [ ""invoiceNumber"", ""invoiceDate"", ""purchaseOrderNumber"", ""supplierName"", ""totalAmount"" ]
        }";
    }

    public async Task RunAIFoundryTestAsync(IConfigurationRoot configuration)
    {
        var uniqueId = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8);
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
        string driveItemId = configuration["DriveItemId"];
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