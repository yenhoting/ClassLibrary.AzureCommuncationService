using System.Net.Mail;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClassLibrary.AzureCommuncationService;

public partial class AzureCommuncationService : IAzureCommuncationService
{
    private readonly EmailClient _client;
    private readonly string _senderAddress;
    private readonly ILogger<AzureCommuncationService> _logger;

    public AzureCommuncationService(IConfiguration configuration, ILogger<AzureCommuncationService> logger)
        : this(
            new EmailClient(GetRequiredSetting(configuration, "CommuncationServiceConnectionString")),
            GetRequiredSetting(configuration, "CommuncationServiceSender"),
            logger)
    {
    }

    public AzureCommuncationService(
        EmailClient client,
        string senderAddress,
        ILogger<AzureCommuncationService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        ValidateAddress(senderAddress, nameof(senderAddress));

        _client = client;
        _senderAddress = senderAddress;
        _logger = logger;
    }

    public async Task<bool> SendMail(MailDto email)
    {
        try
        {
            var operation = await SendMailAsync(email).ConfigureAwait(false);
            return operation.Value.Status == EmailSendStatus.Succeeded;
        }
        catch (RequestFailedException exception)
        {
            LogSendFailure(_logger, exception.Status, exception.ErrorCode);
            return false;
        }
    }

    /// <summary>
    /// Queues an email and asynchronously waits for Azure to finish its send operation.
    /// </summary>
    /// <remarks>
    /// A successful send operation means the email is out for delivery.
    /// Cancellation stops the local wait. Azure may continue a send it has already accepted.
    /// </remarks>
    public Task<EmailSendOperation> SendMailAsync(MailDto email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.Body);

        var submittedRecipients = email.Recipients;
        ArgumentNullException.ThrowIfNull(submittedRecipients);

        if (submittedRecipients.Count == 0)
        {
            throw new ArgumentException("At least one recipient is required.", nameof(email));
        }

        var recipients = new List<EmailAddress>(submittedRecipients.Count);
        foreach (var recipient in submittedRecipients)
        {
            ArgumentNullException.ThrowIfNull(recipient, nameof(email.Recipients));
            ValidateAddress(recipient.Address, nameof(MailRecipientDto.Address));
            recipients.Add(new EmailAddress(recipient.Address, recipient.Name));
        }

        var content = new EmailContent(email.Subject) { Html = email.Body };
        var message = new EmailMessage(_senderAddress, new EmailRecipients(recipients), content);
        return _client.SendAsync(WaitUntil.Completed, message, cancellationToken);
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[key];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Configuration setting '{key}' is required.");
    }

    private static void ValidateAddress(string address, string parameterName)
    {
        if (!MailAddress.TryCreate(address, out var parsed) || parsed.Address != address)
        {
            throw new ArgumentException("A valid email address is required.", parameterName);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Azure email send failed with HTTP status {Status} and error code {ErrorCode}.")]
    private static partial void LogSendFailure(ILogger logger, int status, string? errorCode);
}
