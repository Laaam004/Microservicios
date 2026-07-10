using Aspire.Hosting;
using Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(pg => pg
        .WithHostPort(5555)
        .WithLifetime(ContainerLifetime.Persistent));

var bdOrdenes = postgres.AddDatabase("bdordenes");
var bdFacturaciones = postgres.AddDatabase("bdfacturaciones");

var serviceBus = builder
    .AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator =>
    {
        emulator.WithLifetime(ContainerLifetime.Persistent);
    });

serviceBus.AddServiceBusQueue("productos");
serviceBus.AddServiceBusQueue("productos-procesados");

builder.AddAsbEmulatorUi("asb-ui", serviceBus);

builder
    .AddProject<Projects.Api_Facturacion>("api-facturacion")
    .WithReference(bdFacturaciones)
    .WithReference(serviceBus)
    .WaitFor(bdFacturaciones)
    .WithExternalHttpEndpoints();

builder
    .AddProject<Projects.Api_Ordenes>("api-ordenes")
    .WithReference(bdOrdenes)
    .WithReference(serviceBus)
    .WaitFor(bdOrdenes)
    .WithExternalHttpEndpoints();

builder.Build().Run();