using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TestConsole.Infra;

namespace TestConsole.Tests.AnalyzerApi;

public class APITest : BaseTest
{
    protected override string TestName => "RunCallApiAsync";

    public APITest(IConfigurationRoot configuration) : base(configuration)
    {
    }

    protected override async Task TestDefinitionAsync()
    {
        string tenantId = Configuration["TenantId"];
        string clientId = Configuration["ClientId"];
        string clientSecret = Configuration["ClientSecret"];
        string driveId = Configuration["DriveId"];
        string driveItemIdPdf = Configuration["DriveItemId_PDF"];
        string driveItemIdJpg = Configuration["DriveItemId_JPG"];
        string scope = Configuration["Scope"];
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

        LogInfo($"\nCalling API with PDF at {apiUrl} with DriveId: {driveId}, DriveItemId (PDF): {driveItemIdPdf}");
        var body = new
        {
            DriveId = driveId,
            DriveItemId = driveItemIdPdf,
            UserPrompt = GetUserPrompt(),
            ExpectedJsonSchema = GetJsonSchema()
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var apiResponse = await httpClient.PostAsJsonAsync(apiUrl, body);
        LogInfo($"API Response Status: {apiResponse.StatusCode}");
        var apiResponseContent = await apiResponse.Content.ReadAsStringAsync();
        LogInfo("API Response Content:");
        LogInfo(apiResponseContent);

        LogInfo($"\nCalling API with JPG at {apiUrl} with DriveId: {driveId}, DriveItemId (JPG): {driveItemIdJpg}");
        body = new
        {
            DriveId = driveId,
            DriveItemId = driveItemIdJpg,
            UserPrompt = GetUserPrompt(),
            ExpectedJsonSchema = GetJsonSchema()
        };
        apiResponse = await httpClient.PostAsJsonAsync(apiUrl, body);
        LogInfo($"API Response Status: {apiResponse.StatusCode}");
        apiResponseContent = await apiResponse.Content.ReadAsStringAsync();
        LogInfo("API Response Content:");
        LogInfo(apiResponseContent);

    }

    private static string GetUserPrompt()
    {
        return @"Document Type: Invoice. Extract all invoice information from the attached document";
    }

    private static string GetJsonSchema()
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
}
