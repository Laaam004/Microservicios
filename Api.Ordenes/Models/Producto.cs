namespace Api.Ordenes.Models;

public class Producto
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public int Cantidad { get; set; }
    public string Estado { get; set; } = "Pendiente";
}
