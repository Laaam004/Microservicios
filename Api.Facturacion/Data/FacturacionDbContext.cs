using Api.Facturacion.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Facturacion.Data;

public class FacturacionDbContext : DbContext
{
    public FacturacionDbContext(DbContextOptions<FacturacionDbContext> options) : base(options)
    {
    }

    public DbSet<ProductoProcesado> ProductosProcesados { get; set; }
}
