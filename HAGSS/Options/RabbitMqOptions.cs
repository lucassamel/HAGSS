namespace HAGSS.Options;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string PaymentQueue { get; set; } = "payments.process";
    public string PaymentExchange { get; set; } = "payments";
    public string PaymentRoutingKey { get; set; } = "payment.process";
}
