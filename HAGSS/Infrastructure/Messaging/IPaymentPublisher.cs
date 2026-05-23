namespace HAGSS.Infrastructure.Messaging;

public interface IPaymentPublisher
{
    Task PublishAsync(PaymentMessage message, CancellationToken cancellationToken);
}
