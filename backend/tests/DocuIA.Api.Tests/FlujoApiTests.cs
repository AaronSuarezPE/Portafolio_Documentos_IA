using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocuIA.Api.Modelos;
using DocuIA.Api.Tests.Infraestructura;

namespace DocuIA.Api.Tests;

public class FlujoApiTests(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    private const string Base = "api/v1/docuia";
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient NuevoCliente() => fabrica.CreateClient();

    private static async Task<HttpClient> ConSesionAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsync($"{Base}/Autenticacion/PostLoginInvitado", null);
        respuesta.EnsureSuccessStatusCode();
        var sesion = await respuesta.Content.ReadFromJsonAsync<RespuestaSesion>(Json);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.Token);
        return cliente;
    }

    private static MultipartFormDataContent Archivo(string nombre, byte[] contenido)
    {
        var parte = new ByteArrayContent(contenido);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { parte, "archivo", nombre } };
    }

    [Fact]
    public async Task Salud_responde_y_avisa_que_esta_en_modo_demostracion()
    {
        var cliente = NuevoCliente();

        var respuesta = await cliente.GetFromJsonAsync<JsonElement>($"{Base}/Salud/Get");

        Assert.Equal("ok", respuesta.GetProperty("estado").GetString());
        Assert.True(respuesta.GetProperty("modoDemostracion").GetBoolean());
    }

    [Fact]
    public async Task Las_rutas_protegidas_exigen_sesion()
    {
        var respuesta = await NuevoCliente().GetAsync($"{Base}/Documentos/GetList");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Flujo_completo_subir_revisar_preguntar_y_eliminar()
    {
        var cliente = await ConSesionAsync(NuevoCliente());

        // subir y procesar
        var subida = await cliente.PostAsync($"{Base}/Documentos/PostProcesarDocumento", Archivo("factura.pdf", Pdf));
        Assert.Equal(HttpStatusCode.OK, subida.StatusCode);
        var detalle = (await subida.Content.ReadFromJsonAsync<DocumentoDetalleDto>(Json))!;
        Assert.Equal("Factura", detalle.Tipo);
        Assert.Equal("Procesado", detalle.Estado);
        Assert.NotEmpty(detalle.Campos);
        Assert.Matches(@"^\d{2}/\d{2}/\d{4}$", detalle.Fecha);

        // aparece en el listado
        var lista = (await cliente.GetFromJsonAsync<List<DocumentoResumenDto>>($"{Base}/Documentos/GetList", Json))!;
        Assert.Contains(lista, d => d.CodDocumento == detalle.CodDocumento && d.NombreArchivo == "factura.pdf");

        // confirmar valores corregidos
        var campo = detalle.Campos[0];
        var confirmacion = await cliente.PutAsJsonAsync(
            $"{Base}/Documentos/PutActualizarCampos?id={detalle.CodDocumento}",
            new SolicitudCamposConfirmados([new CampoConfirmado(campo.CodCampo, "Valor corregido")]));
        Assert.Equal(HttpStatusCode.OK, confirmacion.StatusCode);
        var confirmado = (await confirmacion.Content.ReadFromJsonAsync<DocumentoDetalleDto>(Json))!;
        Assert.Equal("Valor corregido", confirmado.Campos.First(c => c.CodCampo == campo.CodCampo).ValorConfirmado);

        // preguntar y ver la conversación guardada
        var pregunta = await cliente.PostAsJsonAsync(
            $"{Base}/Documentos/PostProcesarPregunta?id={detalle.CodDocumento}",
            new SolicitudPregunta("¿Cuál es el total?"));
        Assert.Equal(HttpStatusCode.OK, pregunta.StatusCode);

        var conConversacion = (await cliente.GetFromJsonAsync<DocumentoDetalleDto>(
            $"{Base}/Documentos/Get?id={detalle.CodDocumento}", Json))!;
        Assert.Equal(["Usuario", "Asistente"], conConversacion.Mensajes.Select(m => m.Origen));

        // eliminar
        var eliminar = await cliente.DeleteAsync($"{Base}/Documentos/DeleteEliminar?id={detalle.CodDocumento}");
        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);

        var luego = await cliente.GetAsync($"{Base}/Documentos/Get?id={detalle.CodDocumento}");
        Assert.Equal(HttpStatusCode.NotFound, luego.StatusCode);
    }

    [Fact]
    public async Task Una_persona_no_ve_ni_modifica_los_documentos_de_otra()
    {
        var dueno = await ConSesionAsync(NuevoCliente());
        var intruso = await ConSesionAsync(NuevoCliente());

        var subida = await dueno.PostAsync($"{Base}/Documentos/PostProcesarDocumento", Archivo("privado.pdf", Pdf));
        var detalle = (await subida.Content.ReadFromJsonAsync<DocumentoDetalleDto>(Json))!;

        Assert.Equal(HttpStatusCode.NotFound, (await intruso.GetAsync($"{Base}/Documentos/Get?id={detalle.CodDocumento}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruso.DeleteAsync($"{Base}/Documentos/DeleteEliminar?id={detalle.CodDocumento}")).StatusCode);

        var pregunta = await intruso.PostAsJsonAsync(
            $"{Base}/Documentos/PostProcesarPregunta?id={detalle.CodDocumento}", new SolicitudPregunta("hola"));
        Assert.Equal(HttpStatusCode.NotFound, pregunta.StatusCode);

        var lista = (await intruso.GetFromJsonAsync<List<DocumentoResumenDto>>($"{Base}/Documentos/GetList", Json))!;
        Assert.Empty(lista);
    }

    [Theory]
    [InlineData("programa.exe")]
    [InlineData("notas.txt")]
    public async Task Rechaza_formatos_no_permitidos(string nombre)
    {
        var cliente = await ConSesionAsync(NuevoCliente());

        var respuesta = await cliente.PostAsync($"{Base}/Documentos/PostProcesarDocumento", Archivo(nombre, Pdf));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_un_archivo_que_finge_ser_pdf()
    {
        var cliente = await ConSesionAsync(NuevoCliente());

        var respuesta = await cliente.PostAsync(
            $"{Base}/Documentos/PostProcesarDocumento", Archivo("falso.pdf", "esto no es un pdf"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_una_pregunta_vacia_o_demasiado_larga()
    {
        var cliente = await ConSesionAsync(NuevoCliente());
        var subida = await cliente.PostAsync($"{Base}/Documentos/PostProcesarDocumento", Archivo("a.pdf", Pdf));
        var detalle = (await subida.Content.ReadFromJsonAsync<DocumentoDetalleDto>(Json))!;
        var ruta = $"{Base}/Documentos/PostProcesarPregunta?id={detalle.CodDocumento}";

        var vacia = await cliente.PostAsJsonAsync(ruta, new SolicitudPregunta("   "));
        var larga = await cliente.PostAsJsonAsync(ruta, new SolicitudPregunta(new string('a', 501)));

        Assert.Equal(HttpStatusCode.BadRequest, vacia.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, larga.StatusCode);
    }

    [Fact]
    public async Task Registro_y_login_con_correo_y_clave()
    {
        var cliente = NuevoCliente();
        var email = $"persona.{Guid.NewGuid():N}@correo.com";

        var registro = await cliente.PostAsJsonAsync(
            $"{Base}/Autenticacion/PostAgregarUsuario", new SolicitudUsuario("Ana Prueba", email, "ClaveSegura123"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        var repetido = await cliente.PostAsJsonAsync(
            $"{Base}/Autenticacion/PostAgregarUsuario", new SolicitudUsuario("Otra", email.ToUpperInvariant(), "ClaveSegura123"));
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);

        var correcto = await cliente.PostAsJsonAsync($"{Base}/Autenticacion/PostLogin", new SolicitudLogin(email, "ClaveSegura123"));
        Assert.Equal(HttpStatusCode.OK, correcto.StatusCode);
        var sesion = (await correcto.Content.ReadFromJsonAsync<RespuestaSesion>(Json))!;
        Assert.False(sesion.EsInvitado);
        Assert.Equal("Ana Prueba", sesion.Nombre);

        var incorrecto = await cliente.PostAsJsonAsync($"{Base}/Autenticacion/PostLogin", new SolicitudLogin(email, "otra-clave"));
        var inexistente = await cliente.PostAsJsonAsync($"{Base}/Autenticacion/PostLogin", new SolicitudLogin("nadie@correo.com", "ClaveSegura123"));
        Assert.Equal(HttpStatusCode.Unauthorized, incorrecto.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
    }

    [Theory]
    [InlineData("A", "ana@correo.com", "ClaveSegura123")]
    [InlineData("Ana", "no-es-correo", "ClaveSegura123")]
    [InlineData("Ana", "ana@correo.com", "corta")]
    public async Task El_registro_valida_nombre_correo_y_clave(string nombre, string email, string clave)
    {
        var respuesta = await NuevoCliente().PostAsJsonAsync(
            $"{Base}/Autenticacion/PostAgregarUsuario", new SolicitudUsuario(nombre, email, clave));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Los_tipos_de_documento_salen_de_la_tabla_maestra()
    {
        var cliente = await ConSesionAsync(NuevoCliente());

        var tipos = (await cliente.GetFromJsonAsync<List<TipoDocumentoDto>>($"{Base}/TiposDocumento/GetCombo", Json))!;

        Assert.Contains(tipos, t => t.Nombre == "Factura");
        Assert.Contains(tipos, t => t.Nombre == "Otro");
    }
}
