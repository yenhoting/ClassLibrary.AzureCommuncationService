namespace ClassLibrary.AzureCommuncationService;

public interface IAzureCommuncationService
{
    /// <summary>
    /// Returns true when Azure reports a successful send operation.
    /// </summary>
    Task<bool> SendMail(MailDto email);
}
