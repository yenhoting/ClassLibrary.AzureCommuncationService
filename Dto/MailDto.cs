namespace ClassLibrary.AzureCommuncationService
{
    public class MailDto
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public string Sender { get; set; }
        public List<MailRecipientDto> Recipients { get; set; }
    }

    public class MailRecipientDto
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}
