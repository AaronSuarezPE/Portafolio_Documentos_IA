using DocuIA.Api.Configuracion;
using DocuIA.Api.Datos;
using Microsoft.Extensions.Options;

namespace DocuIA.Api.Servicios;

/// <summary>
/// Trabajo en segundo plano: borra los documentos viejos y los invitados sin documentos
/// para no llenar la base de datos gratuita y no conservar archivos de más.
/// </summary>
public sealed class JobEliminarVencidos(
    IDocumentoRepositorio repositorio,
    IOptions<OpcionesLimpieza> opciones,
    ILogger<JobEliminarVencidos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var intervalo = TimeSpan.FromHours(Math.Max(1, opciones.Value.HorasIntervalo));
        using var temporizador = new PeriodicTimer(intervalo);

        do
        {
            try
            {
                var mensaje = await repositorio.EliminarVencidosAsync(opciones.Value.DiasRetencion);
                logger.LogInformation("Limpieza de documentos vencidos: {Mensaje}", mensaje);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falló la limpieza de documentos vencidos.");
            }
        }
        while (await SiguienteAsync(temporizador, ct));
    }

    private static async Task<bool> SiguienteAsync(PeriodicTimer temporizador, CancellationToken ct)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
