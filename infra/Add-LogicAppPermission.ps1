$ManagedIdentityObjectId = "00000000-0000-0000-0000-000000000000" # Replace with your Managed Identity's object ID
$ApiAppId = "00000000-0000-0000-0000-000000000000" # Replace with your API's Application (client) ID
$PermissionValue = "AI.Read"
$GraphBaseUrl = "https://graph.microsoft.com/v1.0"

# Assign an app role to a managed identity using Azure CLI and Microsoft Graph API
az login

# Get the service principal object for the target API and extract the object ID and app role ID
$url = "$($GraphBaseUrl)/servicePrincipals?`$filter=appId eq '$ApiAppId'"
$apiApp = (az rest --method GET --url $url | ConvertFrom-Json).value[0]
$apiEnterpriseAppId = $apiApp.id
$appRoleId = $apiApp.appRoles | Where-Object { $_.value -eq $PermissionValue -and $_.allowedMemberTypes -contains "Application" } | Select-Object -ExpandProperty id

# Assign the app role to the managed identity
$url = "$($GraphBaseUrl)/servicePrincipals/$($ManagedIdentityObjectId)/appRoleAssignments"
az rest --method POST --url $url --body "{'principalId': '$ManagedIdentityObjectId','resourceId': '$apiEnterpriseAppId','appRoleId': '$appRoleId'}"
