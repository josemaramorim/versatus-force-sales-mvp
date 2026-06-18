namespace Versatus.ForcaVendas.Infrastructure.Integration.RabbitMq;

public sealed class RabbitMqTransportOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string OrderExchange { get; set; } = "pedido.enviado.v1";
    public string ResultQueue { get; set; } = "pedido.resultado.v1";
}
