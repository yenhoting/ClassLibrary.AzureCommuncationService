namespace ClassLibrary.AzureCommuncationService;

public class MailDto
{
    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Retained for compatibility. The service uses the sender configured at construction.
    /// </summary>
    public string? Sender { get; set; }

    public List<MailRecipientDto> Recipients { get; set; } = [];
}

public class MailRecipientDto
{
    public string? Name { get; set; }

    public string Address { get; set; } = string.Empty;
}
