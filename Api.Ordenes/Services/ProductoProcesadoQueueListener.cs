using Api.Ordenes.Data;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Ordenes.Services;

public class ProductoProcesadoQueueListener : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ProductoProcesadoQueueListener> _logger;
    private ServiceBusProcessor? _processor;

    public ProductoProcesadoQueueListener(
        IServiceProvider serviceProvider,
        ServiceBusClient serviceBusClient,
        ILogger<ProductoProcesadoQueueListener> logger)
    {
        _serviceProvider = serviceProvider;
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _serviceBusClient.CreateProcessor("productos-procesados", new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += MessageHandler;
        _processor.ProcessErrorAsync += ErrorHandler;

        _logger.LogInformation("Starting ProductoProcesadoQueueListener on 'productos-procesados' queue...");
        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _logger.LogInformation("Stopping ProductoProcesadoQueueListener...");
        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        _logger.LogInformation("Received product processed message: {Body}", body);

        try
        {
            var processedMessage = JsonSerializer.Deserialize<ProductoProcesadoMessage>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (processedMessage != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OrdenesDbContext>();

                var producto = await dbContext.Productos.FirstOrDefaultAsync(p => p.Id == processedMessage.Id);
                if (producto != null)
                {
                    producto.Estado = "Procesado";
                    producto.FechaModificacion = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Updated product {Id} state to 'Procesado'.", processedMessage.Id);
                }
                else
                {
                    _logger.LogWarning("Product {Id} not found in database to update status.", processedMessage.Id);
                }
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error in Service Bus processor: Source={ErrorSource}, Queue={EntityPath}", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}

public record ProductoProcesadoMessage(int Id);
