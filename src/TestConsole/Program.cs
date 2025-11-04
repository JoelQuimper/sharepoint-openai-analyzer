using System.Net.Http.Headers;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
IConfigurationRoot configuration = builder.Build();

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
    UserPrompt = "The provided document is an invoice. You are a manager and must analyze the invoice and extract the information according to the provided JSON schema.",
    ExpectedJsonSchema = @"
    {
        ""type"": ""object"",
        ""properties"": {
            ""invoiceNumber"": { ""type"": ""string"" },
            ""invoiceDate"": { ""type"": ""string"", ""format"": ""date"" },
            ""purchaseOrderNumber"": { ""type"": ""integer"", ""minimum"": 160000000, ""maximum"": 160999999 },
            ""supplierName"": { ""type"": ""string"" },
            ""totalAmount"": { ""type"": ""number"", ""minimum"": 0 }
        },
        ""required"": [ ""invoiceNumber"", ""invoiceDate"", ""purchaseOrderNumber"", ""supplierName"", ""totalAmount"" ]
    }
    "
};
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
var apiResponse = await httpClient.PostAsJsonAsync(apiUrl, body);
Console.WriteLine($"API Response Status: {apiResponse.StatusCode}");
var apiResponseContent = await apiResponse.Content.ReadAsStringAsync();
Console.WriteLine("API Response Content:");
Console.WriteLine(apiResponseContent);
