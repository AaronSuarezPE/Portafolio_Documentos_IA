using System.Data;
using Dapper;
using DocuIA.Api.Servicios;
using Microsoft.Data.SqlClient;

namespace DocuIA.Api.Datos;

public interface IFabricaConexion
{
    SqlConnection Crear();
}

public sealed class FabricaConexion(IConfiguration configuracion) : IFabricaConexion
{
    private readonly string _cadena = configuracion.GetConnectionString("DocuIA")
        ?? throw new InvalidOperationException("Falta la cadena de conexión «DocuIA».");

    public SqlConnection Crear() => new(_cadena);
}

public static class ExtensionesDapper
{
    /// <summary>
    /// Ejecuta un procedimiento que responde con cod_resultado y dsc_mensaje.
    /// Un código negativo se convierte en un error de negocio con el mensaje del procedimiento.
    /// </summary>
    public static async Task<FilaResultado> EjecutarAsync(this SqlConnection conexion, string procedimiento, object? parametros)
    {
        var resultado = await conexion.QuerySingleAsync<FilaResultado>(
            procedimiento, parametros, commandType: CommandType.StoredProcedure);

        if (resultado.CodResultado < 0)
            throw new ErrorNegocio(resultado.DscMensaje);

        return resultado;
    }
}
