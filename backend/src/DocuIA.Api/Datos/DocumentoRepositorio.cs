using System.Data;
using Dapper;
using DocuIA.Api.Modelos;

namespace DocuIA.Api.Datos;

public interface IDocumentoRepositorio
{
    Task<IReadOnlyList<FilaTipoDocumento>> ListarTiposAsync();
    Task<int> AgregarAsync(int codUsuario, string nombre, string tipoMime, int tamano, string hash, byte[] contenido);
    Task ActualizarAsync(int codUsuario, int codDocumento, int? codTipo, int codEstado, string? resumen);
    Task GuardarCamposAsync(int codUsuario, int codDocumento, IReadOnlyList<CampoExtraido> campos);
    Task ConfirmarCamposAsync(int codUsuario, int codDocumento, IReadOnlyList<CampoConfirmado> campos);
    Task<IReadOnlyList<FilaDocumento>> ListarAsync(int codUsuario);
    Task<(FilaDocumento? Documento, IReadOnlyList<FilaCampo> Campos)> ObtenerAsync(int codUsuario, int codDocumento);
    Task<FilaContenido?> ObtenerContenidoAsync(int codUsuario, int codDocumento);
    Task EliminarAsync(int codUsuario, int codDocumento);
    Task AgregarMensajeAsync(int codUsuario, int codDocumento, int codOrigen, string texto);
    Task<IReadOnlyList<FilaMensaje>> ListarMensajesAsync(int codUsuario, int codDocumento);
    Task<string> EliminarVencidosAsync(int dias);
}

public sealed class DocumentoRepositorio(IFabricaConexion fabrica) : IDocumentoRepositorio
{
    public const int OrigenUsuario = 1;
    public const int OrigenAsistente = 2;

    public async Task<IReadOnlyList<FilaTipoDocumento>> ListarTiposAsync()
    {
        await using var conexion = fabrica.Crear();
        var filas = await conexion.QueryAsync<FilaTipoDocumento>(
            "dbo.pa_dia_lst_tpo_docto", commandType: CommandType.StoredProcedure);
        return filas.ToList();
    }

    public async Task<int> AgregarAsync(
        int codUsuario, string nombre, string tipoMime, int tamano, string hash, byte[] contenido)
    {
        await using var conexion = fabrica.Crear();
        var resultado = await conexion.EjecutarAsync("dbo.pa_dia_aggar_docto", new
        {
            cod_usrio = codUsuario,
            nom_archv = nombre,
            tpo_cntnd = tipoMime,
            num_tamno = tamano,
            nom_hsh = hash,
            bin_cntnd = contenido,
        });
        return resultado.CodResultado;
    }

    public async Task ActualizarAsync(int codUsuario, int codDocumento, int? codTipo, int codEstado, string? resumen)
    {
        await using var conexion = fabrica.Crear();
        await conexion.EjecutarAsync("dbo.pa_dia_edtar_docto", new
        {
            cod_usrio = codUsuario,
            cod_docto = codDocumento,
            cod_tpo_docto = codTipo,
            cod_estdo_docto = codEstado,
            dsc_rsmen = resumen,
        });
    }

    public async Task GuardarCamposAsync(int codUsuario, int codDocumento, IReadOnlyList<CampoExtraido> campos)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("num_orden", typeof(int));
        tabla.Columns.Add("nom_clave", typeof(string));
        tabla.Columns.Add("nom_etqta", typeof(string));
        tabla.Columns.Add("dsc_valor", typeof(string));
        tabla.Columns.Add("num_cnfnz", typeof(decimal));

        for (var i = 0; i < campos.Count; i++)
        {
            var c = campos[i];
            tabla.Rows.Add(i + 1, c.Clave, c.Etiqueta, c.Valor, (decimal)c.Confianza);
        }

        var parametros = new DynamicParameters();
        parametros.Add("cod_usrio", codUsuario);
        parametros.Add("cod_docto", codDocumento);
        parametros.Add("tbl_campo", tabla.AsTableValuedParameter("dbo.tt_dia_campo"));

        await using var conexion = fabrica.Crear();
        await conexion.EjecutarAsync("dbo.pa_dia_aggar_campo", parametros);
    }

    public async Task ConfirmarCamposAsync(int codUsuario, int codDocumento, IReadOnlyList<CampoConfirmado> campos)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("cod_campo", typeof(int));
        tabla.Columns.Add("dsc_valor_cnfrm", typeof(string));

        foreach (var c in campos)
            tabla.Rows.Add(c.CodCampo, c.Valor ?? string.Empty);

        var parametros = new DynamicParameters();
        parametros.Add("cod_usrio", codUsuario);
        parametros.Add("cod_docto", codDocumento);
        parametros.Add("tbl_campo", tabla.AsTableValuedParameter("dbo.tt_dia_campo_cnfrm"));

        await using var conexion = fabrica.Crear();
        await conexion.EjecutarAsync("dbo.pa_dia_edtar_campo", parametros);
    }

    public async Task<IReadOnlyList<FilaDocumento>> ListarAsync(int codUsuario)
    {
        await using var conexion = fabrica.Crear();
        var filas = await conexion.QueryAsync<FilaDocumento>(
            "dbo.pa_dia_lst_docto", new { cod_usrio = codUsuario }, commandType: CommandType.StoredProcedure);
        return filas.ToList();
    }

    public async Task<(FilaDocumento? Documento, IReadOnlyList<FilaCampo> Campos)> ObtenerAsync(
        int codUsuario, int codDocumento)
    {
        await using var conexion = fabrica.Crear();
        using var multiple = await conexion.QueryMultipleAsync(
            "dbo.pa_dia_sel_docto",
            new { cod_usrio = codUsuario, cod_docto = codDocumento },
            commandType: CommandType.StoredProcedure);

        var documento = await multiple.ReadSingleOrDefaultAsync<FilaDocumento>();
        var campos = (await multiple.ReadAsync<FilaCampo>()).ToList();
        return (documento, campos);
    }

    public async Task<FilaContenido?> ObtenerContenidoAsync(int codUsuario, int codDocumento)
    {
        await using var conexion = fabrica.Crear();
        return await conexion.QuerySingleOrDefaultAsync<FilaContenido>(
            "dbo.pa_dia_sel_docto_cntnd",
            new { cod_usrio = codUsuario, cod_docto = codDocumento },
            commandType: CommandType.StoredProcedure);
    }

    public async Task EliminarAsync(int codUsuario, int codDocumento)
    {
        await using var conexion = fabrica.Crear();
        await conexion.EjecutarAsync("dbo.pa_dia_del_docto", new { cod_usrio = codUsuario, cod_docto = codDocumento });
    }

    public async Task AgregarMensajeAsync(int codUsuario, int codDocumento, int codOrigen, string texto)
    {
        await using var conexion = fabrica.Crear();
        await conexion.EjecutarAsync("dbo.pa_dia_aggar_mnsje", new
        {
            cod_usrio = codUsuario,
            cod_docto = codDocumento,
            cod_orgen_mnsje = codOrigen,
            dsc_texto = texto,
        });
    }

    public async Task<IReadOnlyList<FilaMensaje>> ListarMensajesAsync(int codUsuario, int codDocumento)
    {
        await using var conexion = fabrica.Crear();
        var filas = await conexion.QueryAsync<FilaMensaje>(
            "dbo.pa_dia_lst_mnsje",
            new { cod_usrio = codUsuario, cod_docto = codDocumento },
            commandType: CommandType.StoredProcedure);
        return filas.ToList();
    }

    public async Task<string> EliminarVencidosAsync(int dias)
    {
        await using var conexion = fabrica.Crear();
        var resultado = await conexion.EjecutarAsync("dbo.pa_dia_job_elmnr_docto", new { num_dias = dias });
        return resultado.DscMensaje;
    }
}
