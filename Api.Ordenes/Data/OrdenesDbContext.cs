using Api.Ordenes.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Ordenes.Data;

public class OrdenesDbContext : DbContext
{
    public OrdenesDbContext(DbContextOptions<OrdenesDbContext> options) : base(options)
    {
    }

    public DbSet<Producto> Productos { get; set; }
}
