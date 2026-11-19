using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClassLibrary.AzureCommuncationService.Tests;

[TestClass]
public class AzureCommuncationServiceTests
{
    private const string SenderAddress = "sender@example.com";

    [TestMethod]
    public async Task SendMailAsyncAwaitsAzureCompletionAndReturnsTheOperationResult()
    {
        var completion = new TaskCompletionSource<EmailSendOperation>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var expected = new CompletedOperation("operation-1", EmailSendStatus.Succeeded);
        var mail = CreateMail();
        mail.Recipients.Add(new MailRecipientDto { Address = "second@example.com" });
        var client = new TestEmailClient((wait, message, token) =>
        {
            Assert.AreEqual(WaitUntil.Completed, wait);
            Assert.AreEqual(cancellation.Token, token);
            Assert.AreEqual(SenderAddress, message.SenderAddress);
            Assert.AreEqual(mail.Subject, message.Content.Subject);
            Assert.AreEqual(mail.Body, message.Content.Html);
            Assert.AreEqual(2, message.Recipients.To.Count);
            Assert.AreEqual(mail.Recipients[0].Address, message.Recipients.To[0].Address);
            Assert.AreEqual(mail.Recipients[0].Name, message.Recipients.To[0].DisplayName);
            Assert.AreEqual(mail.Recipients[1].Address, message.Recipients.To[1].Address);
            return completion.Task;
        });
        var service = CreateService(client);

        var send = service.SendMailAsync(mail, cancellation.Token);

        Assert.IsFalse(send.IsCompleted);
        completion.SetResult(expected);
        Assert.AreSame(expected, await send);
    }

    [TestMethod]
    public async Task SendMailAsyncObservesCancellationAfterSubmissionStarts()
    {
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource<EmailSendOperation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(new TestEmailClient((_, _, token) => completion.Task.WaitAsync(token)));

        var send = service.SendMailAsync(CreateMail(), cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => send);
    }

    [TestMethod]
    public async Task SendMailAsyncRejectsInvalidRecipientsBeforeCallingAzure()
    {
        var client = new TestEmailClient((_, _, _) => throw new AssertFailedException("Azure must not be called."));
        var service = CreateService(client);
        var mail = CreateMail();
        mail.Recipients[0].Address = "recipient without a domain";

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.SendMailAsync(mail));
    }

    [TestMethod]
    public async Task SendMailReturnsFalseForAnUnsuccessfulOperationStatus()
    {
        var service = CreateService(new TestEmailClient((_, _, _) =>
            Task.FromResult<EmailSendOperation>(new CompletedOperation("operation-2", EmailSendStatus.Failed))));

        Assert.IsFalse(await service.SendMail(CreateMail()));
    }

    [TestMethod]
    public async Task ModernApiPropagatesAzureFailuresAndLegacyApiReturnsFalse()
    {
        var rejected = new RequestFailedException(429, "The service rejected the request.");
        var service = CreateService(new TestEmailClient((_, _, _) => Task.FromException<EmailSendOperation>(rejected)));

        Assert.AreSame(rejected, await Assert.ThrowsExactlyAsync<RequestFailedException>(
            () => service.SendMailAsync(CreateMail())));
        Assert.IsFalse(await service.SendMail(CreateMail()));
    }

    private static AzureCommuncationService CreateService(EmailClient client) =>
        new(client, SenderAddress, NullLogger<AzureCommuncationService>.Instance);

    private static MailDto CreateMail() => new()
    {
        Sender = "unused@example.com",
        Subject = "Order confirmation",
        Body = "<p>Order confirmed.</p>",
        Recipients = [new MailRecipientDto { Address = "recipient@example.com", Name = "Recipient" }]
    };

    private sealed class TestEmailClient(Func<WaitUntil, EmailMessage, CancellationToken, Task<EmailSendOperation>> send)
        : EmailClient
    {
        public override Task<EmailSendOperation> SendAsync(
            WaitUntil wait,
            EmailMessage message,
            CancellationToken cancellationToken = default) =>
            send(wait, message, cancellationToken);
    }

    private sealed class CompletedOperation(string id, EmailSendStatus status) : EmailSendOperation
    {
        public override string Id => id;

        public override EmailSendResult Value { get; } = EmailModelFactory.EmailSendResult(id, status);
    }
}
