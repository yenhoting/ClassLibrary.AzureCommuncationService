# Azure Communication Services Email

This .NET 10 library sends HTML email to one or more recipients through Azure Communication Services Email. It uses the official [Azure.Communication.Email SDK](https://www.nuget.org/packages/Azure.Communication.Email/1.1.0).

## Build and test

Install the [.NET 10 LTS SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) version 10.0.404 or a newer .NET 10 SDK. Run these commands from the repository root.

```sh
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

The unit tests replace the Azure client and require no Azure account or credentials. They exercise request mapping, cancellation, input validation, and failure handling.

## Configure a consuming application

Create an Azure Communication Services resource and link a verified domain from an Email Communication Services resource. Use the sender address configured for that domain. [Azure Email quickstart](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email)

Add a reference to the library from your application. The following command assumes the project and repository directories are siblings.

```sh
dotnet add reference ../ClassLibrary.AzureCommuncationService/ClassLibrary.AzureCommuncationService.csproj
```

### Microsoft Entra ID

Use a managed identity when the application runs in Azure. Enable the identity on the host and grant it an Azure RBAC role that can send email on the Communication Services resource. For local development, sign in to Azure CLI with a principal that has the required access and use `AzureCliCredential` in the local host configuration. [Azure Communication Services authentication](https://learn.microsoft.com/en-us/rest/api/communication/authentication)

Add the credential package to the consuming application.

```sh
dotnet add package Azure.Identity --version 1.21.0
```

Set the following host configuration values. Environment variables use double underscores between section and key names.

| Configuration key | Environment variable | Value |
| --- | --- | --- |
| `AzureCommunicationEmail:Endpoint` | `AzureCommunicationEmail__Endpoint` | HTTPS endpoint of the Communication Services resource |
| `AzureCommunicationEmail:SenderAddress` | `AzureCommunicationEmail__SenderAddress` | Sender email address registered on the linked domain |

Register one client and one service per resource and sender in your Generic Host or ASP.NET Core startup code. The example uses a system-assigned managed identity.

```csharp
using Azure.Communication.Email;
using Azure.Identity;
using ClassLibrary.AzureCommuncationService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var endpoint = new Uri(builder.Configuration["AzureCommunicationEmail:Endpoint"]
    ?? throw new InvalidOperationException("Configure the Azure email endpoint."));
var sender = builder.Configuration["AzureCommunicationEmail:SenderAddress"]
    ?? throw new InvalidOperationException("Configure the Azure email sender address.");

builder.Services.AddSingleton(new EmailClient(endpoint, new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)));
builder.Services.AddSingleton(sp => new AzureCommuncationService(
    sp.GetRequiredService<EmailClient>(),
    sender,
    sp.GetRequiredService<ILogger<AzureCommuncationService>>()));
```

### Existing connection-string configuration

The original namespace, interface, and configuration keys retain their historical spelling. Existing applications can continue to register `builder.Services.AddSingleton<IAzureCommuncationService, AzureCommuncationService>()` and configure `CommuncationServiceSender` and `CommuncationServiceConnectionString`. Supply the connection string through the host's secret provider, such as user secrets for local development or Azure Key Vault for deployed applications.

## Send a message

Inject `AzureCommuncationService` into the consuming class and pass its request or shutdown cancellation token.

```csharp
var message = new MailDto
{
    Subject = "Order confirmed",
    Body = "<p>Your order is confirmed.</p>",
    Recipients =
    [
        new MailRecipientDto { Address = "customer@example.com", Name = "Customer" }
    ]
};

var operation = await emailService.SendMailAsync(message, cancellationToken);
Console.WriteLine($"Azure email operation {operation.Id}: {operation.Value.Status}");
```

`EmailSendStatus.Succeeded` means Azure has processed the send operation and the email is out for delivery. Use the operation ID for tracking and use Azure Monitor or Event Grid for recipient-level delivery reports. [Azure Email delivery reports](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email#get-email-delivery-status)

The configured sender is always used. `MailDto.Sender` is retained for existing callers. A message requires a nonempty subject, an HTML body, and at least one email address. `SendMailAsync` surfaces Azure request failures and respects cancellation. The legacy `SendMail` returns true only for a successful send operation and returns false for Azure request failures. Invalid local inputs raise argument exceptions.

Cancellation stops the local wait. If Azure already accepted the request, it can continue processing the email. Check the operation or delivery record before retrying a request whose outcome is uncertain to avoid duplicate messages.
