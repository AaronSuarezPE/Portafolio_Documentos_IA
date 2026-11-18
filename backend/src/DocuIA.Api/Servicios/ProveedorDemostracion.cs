using DocuIA.Api.Modelos;

namespace DocuIA.Api.Servicios;

/// <summary>
/// Se usa cuando no hay clave de IA configurada. Devuelve datos de ejemplo para poder probar
/// la interfaz y el flujo completo sin costo ni cuentas externas.
/// </summary>
public sealed class ProveedorDemostracion : IProveedorIA
{
    public bool EsDemostracion => true;

    public Task<ResultadoExtraccion> ExtraerAsync(
        ArchivoDocumento archivo,
        IReadOnlyList<string> tiposPermitidos,
        CancellationToken ct)
    {
        var tipo = tiposPermitidos.FirstOrDefault(t => t.Equals("Factura", StringComparison.OrdinalIgnoreCase))
                   ?? (tiposPermitidos.Count > 0 ? tiposPermitidos[0] : null)
                   ?? "Otro";

        var resultado = new ResultadoExtraccion(
            tipo,
            "Datos de ejemplo: el archivo no se leyó porque el servidor está en modo demostración.",
            [
                new("emisor", "Emisor", "Comercial Andina S.A.C.", 0.98),
                new("ruc", "RUC del emisor", "20123456789", 0.95),
                new("numero", "Número de comprobante", "F001-000245", 0.97),
                new("fecha_emision", "Fecha de emisión", "15/10/2026", 0.96),
                new("moneda", "Moneda", "PEN", 0.99),
                new("total", "Total", "1180.00", 0.94),
            ]);

        return Task.FromResult(resultado);
    }

    public Task<string> PreguntarAsync(
        ArchivoDocumento archivo,
        string pregunta,
        IReadOnlyList<MensajeChat> historial,
        CancellationToken ct) =>
        Task.FromResult(
            "Modo demostración: el servidor no tiene una clave de IA configurada, por eso no puedo responder sobre el contenido real del documento.");
}
