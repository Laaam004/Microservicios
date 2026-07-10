using Api.Facturacion.Data;
using Api.Facturacion.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Facturacion.Services;

public class ProductoQueueListener : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ProductoQueueListener> _logger;
    private ServiceBusProcessor? _processor;

    public ProductoQueueListener(
        IServiceProvider serviceProvider,
        ServiceBusClient serviceBusClient,
        ILogger<ProductoQueueListener> logger)
    {
        _serviceProvider = serviceProvider;
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _serviceBusClient.CreateProcessor("productos", new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += MessageHandler;
        _processor.ProcessErrorAsync += ErrorHandler;

        _logger.LogInformation("Starting ProductoQueueListener on 'productos' queue...");
        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _logger.LogInformation("Stopping ProductoQueueListener...");
        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        _logger.LogInformation("Received product message: {Body}", body);

        try
        {
            var productMessage = JsonSerializer.Deserialize<ProductoMessage>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (productMessage != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<FacturacionDbContext>();

                // Check if already processed
                var exists = await dbContext.ProductosProcesados.AnyAsync(p => p.Id == productMessage.Id);
                if (!exists)
                {
                    var procesado = new ProductoProcesado
                    {
                        Id = productMessage.Id,
                        FechaProcesado = DateTime.UtcNow
                    };
                    dbContext.ProductosProcesados.Add(procesado);
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Product {Id} saved to database.", productMessage.Id);
                }
                else
                {
                    _logger.LogInformation("Product {Id} was already processed.", productMessage.Id);
                }

                // Send message to 'productos-procesados' queue
                await using var sender = _serviceBusClient.CreateSender("productos-procesados");
                var responseBody = JsonSerializer.Serialize(new { Id = productMessage.Id });
                var responseMessage = new ServiceBusMessage(responseBody)
                {
                    ContentType = "application/json"
                };
                await sender.SendMessageAsync(responseMessage);
                _logger.LogInformation("Published product processed message for {Id}", productMessage.Id);
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

public record ProductoMessage(int Id, string Nombre, int Cantidad, string Estado);
