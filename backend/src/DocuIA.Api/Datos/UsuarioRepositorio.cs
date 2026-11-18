using System.Data;
using Dapper;

namespace DocuIA.Api.Datos;

public interface IUsuarioRepositorio
{
    Task<int> AgregarAsync(string nombre, string? email, string? claveHash, bool esInvitado);
    Task<FilaUsuario?> ObtenerPorEmailAsync(string email);
    Task<FilaUsuario?> ObtenerPorCodigoAsync(int codUsuario);
    Task RegistrarIngresoAsync(int codUsuario);
}

public sealed class UsuarioRepositorio(IFabricaConexion fabrica) : IUsuarioRepositorio
{
    public async Task<int> AgregarAsync(string nombre, string? email, string? claveHash, bool esInvitado)
    {
        await using var conexion = fabrica.Crear();
        var resultado = await conexion.EjecutarAsync("dbo.pa_dia_aggar_usrio", new
        {
            nom_usrio = nombre,
            nom_email = email,
            nom_clave_hsh = claveHash,
            flg_ivtdo = esInvitado ? "S" : "N",
        });
        return resultado.CodResultado;
    }

    public async Task<FilaUsuario?> ObtenerPorEmailAsync(string email)
    {
        await using var conexion = fabrica.Crear();
        return await conexion.QuerySingleOrDefaultAsync<FilaUsuario>(
            "dbo.pa_dia_sel_usrio", new { nom_email = email }, commandType: CommandType.StoredProcedure);
    }

    public async Task<FilaUsuario?> ObtenerPorCodigoAsync(int codUsuario)
    {
        await using var conexion = fabrica.Crear();
        return await conexion.QuerySingleOrDefaultAsync<FilaUsuario>(
            "dbo.pa_dia_sel_usrio", new { cod_usrio = codUsuario }, commandType: CommandType.StoredProcedure);
    }

    public async Task RegistrarIngresoAsync(int codUsuario)
    {
        await using var conexion = fabrica.Crear();
        await conexion.EjecutarAsync("dbo.pa_dia_edtar_usrio", new { cod_usrio = codUsuario });
    }
}
