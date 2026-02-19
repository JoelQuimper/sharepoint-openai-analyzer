using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace TestConsole;

public static class TestAPI
{
    private static string GetUserPrompt()
    {
        return $@"Document Type: Invoice. Extract all invoice information from the attached document";
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

    public static async Task RunCallApiAsync(IConfigurationRoot configuration)
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
