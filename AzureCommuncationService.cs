using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClassLibrary.AzureCommuncationService
{
    public class AzureCommuncationService : IAzureCommuncationService
    {
        private readonly string? _communcationServiceConnectionString;
        private readonly string? _communcationServiceSender;
        private readonly ILogger<AzureCommuncationService> _logger;

        public AzureCommuncationService(IConfiguration configuration, ILogger<AzureCommuncationService> logger)
        {
            _communcationServiceConnectionString = configuration["CommuncationServiceConnectionString"];
            _communcationServiceSender = configuration["CommuncationServiceSender"];
            _logger = logger;
        }

        public async Task<bool> SendMail(MailDto email)
        {
            bool result = true;

            EmailClient emailClient = new EmailClient(_communcationServiceConnectionString);

            // 加入本文
            var emailContent = new EmailContent(email.Subject)
            {
                Html = email.Body
            };

            // 加入多個收件者
            List<EmailAddress> emailAddresses = email.Recipients.Select(x => new EmailAddress(x.Address, x.Name)).ToList();

            EmailRecipients emailRecipients = new EmailRecipients(emailAddresses);

            try
            {
                // 寄送信件
                var emailMessage = new EmailMessage(_communcationServiceSender, emailRecipients, emailContent);
                EmailSendOperation emailSendOperation = emailClient.Send(Azure.WaitUntil.Completed, emailMessage);
                EmailSendResult statusMonitor = emailSendOperation.Value;
                string operationId = emailSendOperation.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                result = false;
            }

            return result;
        }
    }
}