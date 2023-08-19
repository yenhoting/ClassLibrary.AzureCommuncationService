namespace ClassLibrary.AzureCommuncationService
{
    public interface IAzureCommuncationService
    {
        Task<bool> SendMail(MailDto email);
    }
}
