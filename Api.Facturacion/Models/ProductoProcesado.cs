using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Facturacion.Models;

public class ProductoProcesado
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }
    
    public DateTime FechaProcesado { get; set; } = DateTime.UtcNow;
}
