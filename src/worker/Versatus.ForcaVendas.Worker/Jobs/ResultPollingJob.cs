using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Versatus.ForcaVendas.Domain.Pedidos;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Integration;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.Worker.Jobs;

public sealed class ResultPollingJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationTransport _transport;
    private readonly IConfiguration _config;
    private readonly ILogger<ResultPollingJob> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ResultPollingJob(
        IServiceProvider serviceProvider,
        IIntegrationTransport transport,
        IConfiguration config,
        ILogger<ResultPollingJob> logger)
    {
        _serviceProvider = serviceProvider;
        _transport = transport;
        _config = config;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _config.GetValue<int>("Integration:Ftp:ResultPollIntervalSeconds", 30);
        if (intervalSeconds <= 0) intervalSeconds = 30;

        _logger.LogInformation("Iniciando ResultPollingJob com intervalo de {Interval} segundos.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro fatal durante o ciclo de polling de resultados.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task PollAllTenantsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PedidosDbContext>();

        // Busca todos os tenants ativos no banco de dados
        var tenants = await db.TenantSubscriptions
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.TenantId.ToString())
            .ToListAsync(stoppingToken);

        foreach (var tenantId in tenants)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var pendingResults = await _transport.FetchPendingResultsAsync(tenantId, stoppingToken);

                if (pendingResults.Count > 0)
                {
                    _logger.LogInformation("Encontrados {Count} resultados de pedidos pendentes para o tenant {TenantId}.", pendingResults.Count, tenantId);
                }

                foreach (var result in pendingResults)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    await ProcessResultWithIdempotencyAsync(db, tenantId, result, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar polling de resultados para o tenant {TenantId}.", tenantId);
            }
        }
    }

    private async Task ProcessResultWithIdempotencyAsync(
        PedidosDbContext db,
        string tenantId,
        OrderResultPayload result,
        CancellationToken stoppingToken)
    {
        var sourceEventIdStr = result.Payload.SourceEventId == Guid.Empty
            ? result.EventId.ToString()
            : result.Payload.SourceEventId.ToString();

        // 1. Verificar idempotência na tabela eventos_integracao_pedidos
        var alreadyProcessed = await db.EventosIntegracao
            .AnyAsync(e => e.TenantId == tenantId &&
                           e.PedidoId == result.PedidoId &&
                           e.SourceEventId == sourceEventIdStr,
                      stoppingToken);

        if (alreadyProcessed)
        {
            _logger.LogWarning("Resultado do pedido {PedidoId} com evento {SourceEventId} já foi processado anteriormente. Ignorando atualização de negócio.",
                result.PedidoId, sourceEventIdStr);

            // Confirma o recebimento mesmo assim para que o arquivo seja removido/movido no transporte
            if (!string.IsNullOrEmpty(result.ResultFileId))
            {
                await _transport.AcknowledgeResultAsync(tenantId, result.ResultFileId, stoppingToken);
            }
            return;
        }

        // 2. Transição de status do pedido
        var pedido = await db.Pedidos
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == result.PedidoId, stoppingToken);

        var sucesso = string.Equals(result.Payload.Resultado, "processado", StringComparison.OrdinalIgnoreCase);

        if (pedido is not null)
        {
            var oldStatus = pedido.StatusId;
            pedido.StatusId = sucesso ? PedidoStatus.ProcessadoId : PedidoStatus.ErroId;
            pedido.AtualizadoEm = DateTimeOffset.UtcNow;

            if (!sucesso && !string.IsNullOrEmpty(result.Payload.MotivoRejeicao))
            {
                pedido.Observacao = string.IsNullOrEmpty(pedido.Observacao)
                    ? $"Rejeitado pelo ERP: {result.Payload.MotivoRejeicao}"
                    : $"{pedido.Observacao} | Rejeitado pelo ERP: {result.Payload.MotivoRejeicao}";
            }

            _logger.LogInformation("Pedido {PedidoId} atualizado de status {OldStatus} para {NewStatus} com sucesso={Sucesso}.",
                pedido.Id, oldStatus, pedido.StatusId, sucesso);
        }
        else
        {
            _logger.LogWarning("Pedido {PedidoId} não encontrado no banco de dados para o tenant {TenantId}. Evento de integração será registrado mesmo assim.",
                result.PedidoId, tenantId);
        }

        // 3. Registrar o evento de integração
        var integrationEvent = new EventoIntegracaoPedidoEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PedidoId = result.PedidoId,
            SourceEventId = sourceEventIdStr,
            Tipo = result.EventType, // "pedido.resultado"
            Payload = JsonSerializer.Serialize(result, _jsonOptions),
            CriadoEm = DateTimeOffset.UtcNow,
            ProcessadoEm = DateTimeOffset.UtcNow,
            Sucesso = sucesso
        };

        db.EventosIntegracao.Add(integrationEvent);
        await db.SaveChangesAsync(stoppingToken);

        // 4. Acknowledgment (confirmação / movimentação de arquivo)
        if (!string.IsNullOrEmpty(result.ResultFileId))
        {
            await _transport.AcknowledgeResultAsync(tenantId, result.ResultFileId, stoppingToken);
        }
    }
}
