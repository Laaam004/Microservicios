var builder = DistributedApplication.CreateBuilder(args);


var postgres = builder
    .AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(o => o.WithHostPort(5555).WithLifetime(ContainerLifetime.Persistent));

var bdOrdenes = postgres.AddDatabase("bdordenes");
var bdFacturaciones = postgres.AddDatabase("bdfacturaciones");

builder
    .AddProject<Projects.Api_Facturacion>("api-facturacion")
    .WithReference(bdFacturaciones)
    .WaitFor(bdFacturaciones)
    .WithExternalHttpEndpoints();

builder 
    .AddProject<Projects.Api_Ordenes>("api-ordenes")
    .WithReference(bdOrdenes)
    .WaitFor(bdOrdenes)
    .WithExternalHttpEndpoints();

builder.Build().Run();
