using Api.Ordenes.Data;
using Api.Ordenes.Models;
using FastEndpoints;
using FluentValidation;

namespace Api.Ordenes.Endpoints;

public class CrearProductoRequest
{
    public required string Nombre { get; set; }
    public int Cantidad { get; set; }
}

public class CrearProductoResponse
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public int Cantidad { get; set; }
    public required string Estado { get; set; }
}

public class CrearProductoValidator : Validator<CrearProductoRequest>
{
    public CrearProductoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del producto es obligatorio.");
        RuleFor(x => x.Cantidad)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor que 0.");
    }
}

public class CrearProductoEndpoint : Endpoint<CrearProductoRequest, CrearProductoResponse>
{
    private readonly OrdenesDbContext _dbContext;

    public CrearProductoEndpoint(OrdenesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("/api/productos");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CrearProductoRequest req, CancellationToken ct)
    {
        var producto = new Producto
        {
            Nombre = req.Nombre,
            Cantidad = req.Cantidad,
            Estado = "Pendiente"
        };

        _dbContext.Productos.Add(producto);
        await _dbContext.SaveChangesAsync(ct);

        var response = new CrearProductoResponse
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            Cantidad = producto.Cantidad,
            Estado = producto.Estado
        };

        await Send.ResponseAsync(response, 201, ct);
    }
}
