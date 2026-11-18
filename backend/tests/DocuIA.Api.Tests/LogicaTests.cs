using System.IdentityModel.Tokens.Jwt;
using DocuIA.Api.Configuracion;
using DocuIA.Api.Datos;
using DocuIA.Api.Modelos;
using DocuIA.Api.Servicios;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocuIA.Api.Tests;

// Pruebas de la lógica sin base de datos ni red (repositorios y proveedor de IA falsos).
// Cubren cada regla y cada valor límite, para que Stryker no deje mutantes vivos.

public sealed class ValidadorArchivoBordesTests
{
    private static byte[] Con(byte[] firma, int largo)
    {
        var datos = new byte[largo];
        firma.CopyTo(datos, 0);
        return datos;
    }

    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF];

    [Theory]
    [InlineData("a.pdf", "application/pdf", 4)]
    [InlineData("a.png", "image/png", 8)]
    [InlineData("a.jpg", "image/jpeg", 3)]
    [InlineData("A.JPEG", "image/jpeg", 3)]
    public void El_archivo_mas_corto_posible_con_su_firma_es_valido(string nombre, string tipo, int largo)
    {
        var firma = tipo switch { "application/pdf" => Pdf, "image/png" => Png, _ => Jpg };
        var r = ValidadorArchivo.Validar(nombre, Con(firma, largo), 5);
        Assert.True(r.EsValido, r.Error);
        Assert.Equal(tipo, r.TipoMime);
    }

    [Theory]
    [InlineData("a.pdf", 0)] [InlineData("a.pdf", 1)] [InlineData("a.pdf", 2)] [InlineData("a.pdf", 3)]
    [InlineData("a.png", 0)] [InlineData("a.png", 1)] [InlineData("a.png", 2)] [InlineData("a.png", 3)]
    [InlineData("a.jpg", 0)] [InlineData("a.jpg", 1)] [InlineData("a.jpg", 2)]
    public void Si_falla_cualquier_byte_de_la_firma_el_archivo_se_rechaza(string nombre, int posicion)
    {
        var firma = (byte[])(nombre.EndsWith("pdf") ? Pdf : nombre.EndsWith("png") ? Png : Jpg).Clone();
        firma[posicion] ^= 0x01;
        var r = ValidadorArchivo.Validar(nombre, Con(firma, 16), 5);
        Assert.False(r.EsValido);
        Assert.Contains("no corresponde a su extensión", r.Error);
    }

    [Theory]
    [InlineData("a.pdf", new byte[] { 0x25, 0x50, 0x44 })]
    [InlineData("a.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0 })]
    [InlineData("a.jpg", new byte[] { 0xFF, 0xD8 })]
    public void Un_archivo_demasiado_corto_para_tener_firma_completa_se_rechaza(string nombre, byte[] datos)
    {
        Assert.False(ValidadorArchivo.Validar(nombre, datos, 5).EsValido);
    }

    [Fact]
    public void Una_firma_de_otro_tipo_con_la_extension_equivocada_se_rechaza()
    {
        Assert.False(ValidadorArchivo.Validar("a.png", Con(Pdf, 16), 5).EsValido);
        Assert.False(ValidadorArchivo.Validar("a.pdf", Con(Jpg, 16), 5).EsValido);
    }

    [Fact]
    public void El_tamano_se_mide_en_megabytes_y_el_maximo_exacto_se_acepta()
    {
        Assert.Equal("El archivo está vacío.", ValidadorArchivo.Validar("a.pdf", [], 5).Error);
        Assert.True(ValidadorArchivo.Validar("a.pdf", Con(Pdf, 2 * 1024 * 1024), 2).EsValido);
        var r = ValidadorArchivo.Validar("a.pdf", Con(Pdf, (2 * 1024 * 1024) + 1), 2);
        Assert.Equal("El archivo supera el máximo de 2 MB.", r.Error);
    }

    [Theory]
    [InlineData("a.xlsx")]
    [InlineData("sin-extension")]
    [InlineData(null)]
    public void Una_extension_no_permitida_o_ausente_se_rechaza_con_la_ayuda_de_formatos(string? nombre)
    {
        var r = ValidadorArchivo.Validar(nombre, Con(Pdf, 16), 5);
        Assert.False(r.EsValido);
        Assert.StartsWith("Formato no permitido.", r.Error);
    }
}

public sealed class TokenServiceTests
{
    private static JwtSecurityToken Leer(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static TokenService Servicio(int minutos = 90) => new(Options.Create(new OpcionesJwt
    { Clave = new string('k', 40), Emisor = "emi", Audiencia = "aud", MinutosVigencia = minutos }));

    [Theory]
    [InlineData(true, "S")]
    [InlineData(false, "N")]
    public void El_token_lleva_codigo_nombre_y_bandera_de_invitado(bool invitado, string bandera)
    {
        var t = Leer(Servicio().Crear(42, "Ana", invitado));
        Assert.Equal("42", t.Claims.First(c => c.Type is "nameid" or System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        Assert.Equal("Ana", t.Claims.First(c => c.Type is "unique_name" or System.Security.Claims.ClaimTypes.Name).Value);
        Assert.Equal(bandera, t.Claims.First(c => c.Type == "invitado").Value);
        Assert.Equal("emi", t.Issuer);
        Assert.Contains("aud", t.Audiences);
    }

    [Fact]
    public void El_token_vence_segun_los_minutos_configurados()
    {
        var t = Leer(Servicio(90).Crear(1, "Ana", false));
        var minutos = (t.ValidTo - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(minutos, 88, 90.5);
    }
}

public sealed class ProveedorDemostracionTests
{
    private readonly ProveedorDemostracion _p = new();
    private static readonly ArchivoDocumento Archivo = new("a.pdf", "application/pdf", [1]);

    [Theory]
    [InlineData("Otro,Factura", "Factura")]
    [InlineData("otro,factura", "factura")]
    [InlineData("Boleta,Otro", "Boleta")]
    [InlineData("", "Otro")]
    public async Task Elige_Factura_si_existe_si_no_el_primero_y_si_no_hay_ninguno_Otro(string tipos, string esperado)
    {
        var lista = tipos.Length == 0 ? [] : tipos.Split(',');
        var r = await _p.ExtraerAsync(Archivo, lista, CancellationToken.None);
        Assert.Equal(esperado, r.TipoDocumento);
    }

    [Fact]
    public async Task Devuelve_seis_campos_de_ejemplo_y_avisa_que_son_de_demostracion()
    {
        var r = await _p.ExtraerAsync(Archivo, ["Factura"], CancellationToken.None);
        Assert.True(_p.EsDemostracion);
        Assert.StartsWith("Datos de ejemplo:", r.Resumen);
        Assert.Equal(
            [("emisor", "Emisor", "Comercial Andina S.A.C."), ("ruc", "RUC del emisor", "20123456789"),
             ("numero", "Número de comprobante", "F001-000245"), ("fecha_emision", "Fecha de emisión", "15/10/2026"),
             ("moneda", "Moneda", "PEN"), ("total", "Total", "1180.00")],
            r.Campos.Select(c => (c.Clave, c.Etiqueta, c.Valor)));
        Assert.All(r.Campos, c => Assert.InRange(c.Confianza, 0.9, 1));
    }

    [Fact]
    public async Task La_respuesta_del_chat_avisa_que_no_hay_clave_de_IA()
    {
        var r = await _p.PreguntarAsync(Archivo, "¿Cuánto?", [], CancellationToken.None);
        Assert.StartsWith("Modo demostración:", r);
    }
}

internal sealed class UsuariosFalsos : IUsuarioRepositorio
{
    public FilaUsuario? Existente { get; set; }
    public string? EmailBuscado { get; private set; }
    public List<(string Nombre, string? Email, string? Hash, bool Invitado)> Agregados { get; } = [];
    public List<int> Ingresos { get; } = [];

    public Task<int> AgregarAsync(string nombre, string? email, string? claveHash, bool esInvitado)
    {
        Agregados.Add((nombre, email, claveHash, esInvitado));
        return Task.FromResult(31);
    }

    public Task<FilaUsuario?> ObtenerPorEmailAsync(string email) { EmailBuscado = email; return Task.FromResult(Existente); }
    public Task<FilaUsuario?> ObtenerPorCodigoAsync(int codUsuario) => Task.FromResult(Existente);
    public Task RegistrarIngresoAsync(int codUsuario) { Ingresos.Add(codUsuario); return Task.CompletedTask; }
}

internal sealed class ClavesFalsas : IClaveService
{
    public string? HashUsado { get; private set; }
    public string Generar(string clave) => "H:" + clave;
    public bool Verificar(string clave, string? hashGuardado) { HashUsado = hashGuardado; return hashGuardado == "H:" + clave; }
}

internal sealed class TokensFalsos : ITokenService
{
    public string Crear(int codUsuario, string nombre, bool esInvitado) => $"tok|{codUsuario}|{nombre}|{esInvitado}";
}

public sealed class AutenticacionServiceTests
{
    private readonly UsuariosFalsos _repo = new();
    private readonly ClavesFalsas _claves = new();

    private AutenticacionService Servicio => new(_repo, _claves, new TokensFalsos(),
        Options.Create(new OpcionesJwt { MinutosVigencia = 77 }), NullLogger<AutenticacionService>.Instance);

    private static FilaUsuario Ana() => new() { CodUsrio = 5, NomUsrio = "Ana", NomEmail = "ana@correo.com", NomClaveHsh = "H:Clave1234" };

    [Theory]
    [InlineData("", "Clave1234")]
    [InlineData("   ", "Clave1234")]
    [InlineData("ana@correo.com", "")]
    [InlineData(null, null)]
    public async Task El_ingreso_exige_correo_y_clave(string? email, string? clave)
    {
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.LoginAsync(new(email, clave)));
        Assert.Equal("Ingresa tu correo y tu clave.", e.Message);
        Assert.Equal(400, e.Estado);
    }

    [Fact]
    public async Task Un_ingreso_correcto_normaliza_el_correo_registra_el_ingreso_y_crea_la_sesion()
    {
        _repo.Existente = Ana();
        var r = await Servicio.LoginAsync(new("  ANA@Correo.com ", "Clave1234"));

        Assert.Equal("ana@correo.com", _repo.EmailBuscado);
        Assert.Equal([5], _repo.Ingresos);
        Assert.Equal(new RespuestaSesion("tok|5|Ana|False", "Ana", false, 77), r);
    }

    [Fact]
    public async Task Clave_equivocada_y_correo_inexistente_dan_el_mismo_401_y_no_registran_ingreso()
    {
        _repo.Existente = Ana();
        var e1 = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.LoginAsync(new("ana@correo.com", "otra-clave")));
        _repo.Existente = null;
        var e2 = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.LoginAsync(new("nadie@correo.com", "Clave1234")));

        foreach (var e in new[] { e1, e2 })
            Assert.Equal((401, "El correo o la clave no son correctos."), (e.Estado, e.Message));
        Assert.Empty(_repo.Ingresos);
    }

    [Fact]
    public async Task Con_un_correo_inexistente_igual_se_verifica_contra_un_hash_de_relleno()
    {
        _repo.Existente = null;
        await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.LoginAsync(new("nadie@correo.com", "Clave1234")));
        Assert.False(string.IsNullOrEmpty(_claves.HashUsado));
    }

    [Fact]
    public async Task Una_cuenta_de_invitado_sin_clave_no_puede_ingresar_con_correo()
    {
        _repo.Existente = new FilaUsuario { CodUsrio = 9, NomUsrio = "Invitado 1234", NomEmail = "x@y.com", NomClaveHsh = null };
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.LoginAsync(new("x@y.com", "lo-que-sea")));
        Assert.Equal(401, e.Estado);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)] [InlineData(100, true)] [InlineData(101, false)]
    public async Task El_nombre_del_registro_va_de_2_a_100_caracteres(int largo, bool valido)
    {
        var solicitud = new SolicitudUsuario(new string('a', largo), "ana@correo.com", "Clave1234");
        if (valido)
            Assert.Equal(31, (await Servicio.AgregarUsuarioAsync(solicitud)).Nombre.Length > 0 ? 31 : 0);
        else
            Assert.Equal("El nombre debe tener entre 2 y 100 caracteres.", (await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.AgregarUsuarioAsync(solicitud))).Message);
    }

    [Theory]
    [InlineData(7, false)] [InlineData(8, true)] [InlineData(100, true)] [InlineData(101, false)]
    public async Task La_clave_del_registro_va_de_8_a_100_caracteres(int largo, bool valido)
    {
        var solicitud = new SolicitudUsuario("Ana", "ana@correo.com", new string('x', largo));
        if (valido)
            await Servicio.AgregarUsuarioAsync(solicitud);
        else
            Assert.Equal("La clave debe tener entre 8 y 100 caracteres.", (await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.AgregarUsuarioAsync(solicitud))).Message);
    }

    [Theory]
    [InlineData("")] [InlineData("sin-arroba")] [InlineData("a@b")] [InlineData("a b@correo.com")]
    [InlineData("Ana <ana@correo.com>")]
    public async Task Un_correo_invalido_se_rechaza(string email)
    {
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.AgregarUsuarioAsync(new("Ana", email, "Clave1234")));
        Assert.Equal("Ingresa un correo válido.", e.Message);
    }

    [Fact]
    public async Task El_correo_de_150_caracteres_es_valido_y_el_de_151_no()
    {
        var de150 = new string('a', 150 - "@b.com".Length) + "@b.com";
        await Servicio.AgregarUsuarioAsync(new("Ana", de150, "Clave1234"));
        var de151 = new string('a', 151 - "@b.com".Length) + "@b.com";
        await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.AgregarUsuarioAsync(new("Ana", de151, "Clave1234")));
    }

    [Fact]
    public async Task Un_registro_valido_guarda_el_hash_registra_el_ingreso_y_no_es_invitado()
    {
        var r = await Servicio.AgregarUsuarioAsync(new("  Ana Ruiz ", " ANA@Correo.com", "Clave1234"));

        Assert.Equal([("Ana Ruiz", "ana@correo.com", "H:Clave1234", false)], _repo.Agregados);
        Assert.Equal([31], _repo.Ingresos);
        Assert.Equal(new RespuestaSesion("tok|31|Ana Ruiz|False", "Ana Ruiz", false, 77), r);
    }

    [Fact]
    public async Task El_invitado_se_crea_sin_correo_ni_clave_y_con_nombre_numerado()
    {
        var r = await Servicio.LoginInvitadoAsync();

        var (nombre, email, hash, invitado) = Assert.Single(_repo.Agregados);
        Assert.Matches(@"^Invitado \d{4}$", nombre);
        Assert.Null(email);
        Assert.Null(hash);
        Assert.True(invitado);
        Assert.True(r.EsInvitado);
        Assert.Equal($"tok|31|{nombre}|True", r.Token);
    }
}

internal sealed class DocumentosFalsos : IDocumentoRepositorio
{
    public List<FilaTipoDocumento> Tipos { get; set; } =
        [new() { CodTpoDocto = 1, NomTpoDocto = "Factura" }, new() { CodTpoDocto = 9, NomTpoDocto = "Otro" }];
    public FilaDocumento? Documento { get; set; } = new() { CodDocto = 10, NomArchv = "a.pdf", NomTpoDocto = "Factura", NomEstdoDocto = "Procesado", NumTamno = 5, DscRsmen = "r", FecRgtro = "f", HorRgtro = "h" };
    public List<FilaCampo> Campos { get; set; } = [new() { CodCampo = 1, NomClave = "k", NomEtqta = "E", DscValor = "v", DscValorCnfrm = "c", NumCnfnz = 0.5m }];
    public List<FilaMensaje> Mensajes { get; set; } = [];
    public FilaContenido? Contenido { get; set; } = new() { NomArchv = "a.pdf", TpoCntnd = "application/pdf", BinCntnd = [1, 2] };

    public (string Nombre, string Mime, int Tamano, string Hash)? Agregado { get; private set; }
    public (int? Tipo, int Estado, string? Resumen)? Actualizado { get; private set; }
    public IReadOnlyList<CampoExtraido>? CamposGuardados { get; private set; }
    public IReadOnlyList<CampoConfirmado>? CamposConfirmados { get; private set; }
    public List<(int Origen, string Texto)> MensajesAgregados { get; } = [];
    public bool Eliminado { get; private set; }

    public Task<IReadOnlyList<FilaTipoDocumento>> ListarTiposAsync() => Task.FromResult<IReadOnlyList<FilaTipoDocumento>>(Tipos);

    public Task<int> AgregarAsync(int codUsuario, string nombre, string tipoMime, int tamano, string hash, byte[] contenido)
    {
        Agregado = (nombre, tipoMime, tamano, hash);
        return Task.FromResult(10);
    }

    public Task ActualizarAsync(int codUsuario, int codDocumento, int? codTipo, int codEstado, string? resumen)
    {
        Actualizado = (codTipo, codEstado, resumen);
        return Task.CompletedTask;
    }

    public Task GuardarCamposAsync(int codUsuario, int codDocumento, IReadOnlyList<CampoExtraido> campos) { CamposGuardados = campos; return Task.CompletedTask; }
    public Task ConfirmarCamposAsync(int codUsuario, int codDocumento, IReadOnlyList<CampoConfirmado> campos) { CamposConfirmados = campos; return Task.CompletedTask; }
    public Task<IReadOnlyList<FilaDocumento>> ListarAsync(int codUsuario) => Task.FromResult<IReadOnlyList<FilaDocumento>>(Documento is null ? [] : [Documento]);

    public Task<(FilaDocumento? Documento, IReadOnlyList<FilaCampo> Campos)> ObtenerAsync(int codUsuario, int codDocumento) =>
        Task.FromResult<(FilaDocumento?, IReadOnlyList<FilaCampo>)>((Documento, Campos));

    public Task<FilaContenido?> ObtenerContenidoAsync(int codUsuario, int codDocumento) => Task.FromResult(Contenido);
    public Task EliminarAsync(int codUsuario, int codDocumento) { Eliminado = true; return Task.CompletedTask; }
    public Task AgregarMensajeAsync(int codUsuario, int codDocumento, int codOrigen, string texto) { MensajesAgregados.Add((codOrigen, texto)); return Task.CompletedTask; }
    public Task<IReadOnlyList<FilaMensaje>> ListarMensajesAsync(int codUsuario, int codDocumento) => Task.FromResult<IReadOnlyList<FilaMensaje>>(Mensajes);
    public Task<string> EliminarVencidosAsync(int dias) => Task.FromResult("0");
}

internal sealed class IaFalsa : IProveedorIA
{
    public bool EsDemostracion { get; set; }
    public ResultadoExtraccion Resultado { get; set; } = new("Factura", "Resumen", []);
    public Exception? Falla { get; set; }
    public string Respuesta { get; set; } = "Respuesta de la IA";
    public IReadOnlyList<MensajeChat>? HistorialRecibido { get; private set; }
    public string? PreguntaRecibida { get; private set; }

    public Task<ResultadoExtraccion> ExtraerAsync(ArchivoDocumento archivo, IReadOnlyList<string> tiposPermitidos, CancellationToken ct) =>
        Falla is null ? Task.FromResult(Resultado) : Task.FromException<ResultadoExtraccion>(Falla);

    public Task<string> PreguntarAsync(ArchivoDocumento archivo, string pregunta, IReadOnlyList<MensajeChat> historial, CancellationToken ct)
    {
        PreguntaRecibida = pregunta;
        HistorialRecibido = historial;
        return Task.FromResult(Respuesta);
    }
}

public sealed class DocumentoServiceTests
{
    private readonly DocumentosFalsos _repo = new();
    private readonly IaFalsa _ia = new();
    private static readonly byte[] PdfValido = [0x25, 0x50, 0x44, 0x46, 1, 2, 3];

    private DocumentoService Servicio => new(_repo, _ia, Options.Create(new OpcionesArchivos { TamanoMaximoMb = 5 }), NullLogger<DocumentoService>.Instance);

    [Fact]
    public async Task Un_documento_inexistente_responde_404()
    {
        _repo.Documento = null;
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.ObtenerAsync(1, 10));
        Assert.Equal((404, "El documento no existe."), (e.Estado, e.Message));
    }

    [Fact]
    public async Task El_detalle_traduce_documento_campos_y_mensajes()
    {
        _repo.Mensajes = [new() { CodMnsje = 3, NomOrgenMnsje = "usuario", DscTexto = "hola", FecRgtro = "F", HorRgtro = "H" }];
        var d = await Servicio.ObtenerAsync(1, 10);
        Assert.Equal(new CampoDto(1, "k", "E", "v", "c", 0.5), Assert.Single(d.Campos));
        Assert.Equal(new MensajeDto(3, "usuario", "hola", "F", "H"), Assert.Single(d.Mensajes));
        Assert.Equal((10, "a.pdf", "Factura", "Procesado", 5, "r"), (d.CodDocumento, d.NombreArchivo, d.Tipo, d.Estado, d.TamanoBytes, d.Resumen));
    }

    [Fact]
    public async Task Listar_y_tipos_se_traducen_y_la_demostracion_se_consulta_al_proveedor()
    {
        Assert.Single(await Servicio.ListarAsync(1));
        Assert.Equal(["Factura", "Otro"], (await Servicio.ListarTiposAsync()).Select(t => t.Nombre));
        _ia.EsDemostracion = true;
        Assert.True(Servicio.EsDemostracion);
    }

    // ---------- Procesar ----------

    [Fact]
    public async Task Un_archivo_invalido_se_rechaza_sin_guardar_nada()
    {
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.ProcesarAsync(1, "a.xlsx", PdfValido, CancellationToken.None));
        Assert.StartsWith("Formato no permitido.", e.Message);
        Assert.Null(_repo.Agregado);
    }

    [Fact]
    public async Task Procesar_guarda_el_archivo_limpio_con_su_hash_y_deja_el_documento_procesado_con_su_tipo()
    {
        _ia.Resultado = new("  factura ", new string('r', 1500), [new("emisor", "Emisor", "Acme", 0.9)]);
        await Servicio.ProcesarAsync(1, "../carpeta/factura.pdf", PdfValido, CancellationToken.None);

        var (nombre, mime, tamano, hash) = _repo.Agregado!.Value;
        Assert.Equal(("factura.pdf", "application/pdf", PdfValido.Length), (nombre, mime, tamano));
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(PdfValido)).ToLowerInvariant(), hash);
        Assert.Equal((1, 2), (_repo.Actualizado!.Value.Tipo, _repo.Actualizado.Value.Estado));
        Assert.Equal(1000, _repo.Actualizado.Value.Resumen!.Length);
    }

    [Theory]
    [InlineData("Desconocido", 9)]   // no existe → «Otro»
    [InlineData("FACTURA", 1)]
    public async Task Si_la_IA_propone_un_tipo_que_no_existe_se_usa_Otro(string propuesto, int codigoEsperado)
    {
        _ia.Resultado = new(propuesto, "r", []);
        await Servicio.ProcesarAsync(1, "a.pdf", PdfValido, CancellationToken.None);
        Assert.Equal(codigoEsperado, _repo.Actualizado!.Value.Tipo);
    }

    [Fact]
    public async Task Sin_tipo_Otro_en_la_lista_se_usa_el_primero()
    {
        _repo.Tipos = [new() { CodTpoDocto = 4, NomTpoDocto = "Boleta" }, new() { CodTpoDocto = 5, NomTpoDocto = "Contrato" }];
        _ia.Resultado = new("Desconocido", "r", []);
        await Servicio.ProcesarAsync(1, "a.pdf", PdfValido, CancellationToken.None);
        Assert.Equal(4, _repo.Actualizado!.Value.Tipo);
    }

    [Fact]
    public async Task Los_campos_de_la_IA_se_limpian_recortan_y_limitan_a_50()
    {
        var campos = new List<CampoExtraido>
        {
            new("  " + new string('c', 70), " " + new string('e', 120) + " ", "  " + new string('v', 1200) + "  ", 1.5),
            new("", "Sin clave", "x", 0.5),
            new("sin_etiqueta", "   ", "x", 0.5),
            new("nulo", "Valor nulo", null!, -0.4),
            new("redondeo", "Redondeo", "x", 0.123456),
        };
        for (var i = 0; i < 60; i++) campos.Add(new($"k{i}", $"E{i}", "x", 0.5));
        _ia.Resultado = new("Factura", "r", campos);

        await Servicio.ProcesarAsync(1, "a.pdf", PdfValido, CancellationToken.None);

        var g = _repo.CamposGuardados!;
        Assert.Equal(50, g.Count);
        Assert.Equal((60, 100, 1000, 1.0), (g[0].Clave.Length, g[0].Etiqueta.Length, g[0].Valor.Length, g[0].Confianza));
        Assert.DoesNotContain(g, c => c.Clave is "" or "sin_etiqueta");
        Assert.Equal(("", 0.0), (g[1].Valor, g[1].Confianza));
        Assert.Equal(0.123, g[2].Confianza);
    }

    [Fact]
    public async Task Una_lista_de_campos_nula_se_trata_como_vacia()
    {
        _ia.Resultado = new("Factura", "r", null!);
        await Servicio.ProcesarAsync(1, "a.pdf", PdfValido, CancellationToken.None);
        Assert.Empty(_repo.CamposGuardados!);
    }

    [Fact]
    public async Task Si_falla_la_IA_el_documento_queda_con_error_y_la_falla_sigue_su_camino()
    {
        _ia.Falla = new ErrorProveedorIA("La IA no respondió.");
        var e = await Assert.ThrowsAsync<ErrorProveedorIA>(() => Servicio.ProcesarAsync(1, "a.pdf", PdfValido, CancellationToken.None));
        Assert.Equal("La IA no respondió.", e.Message);
        Assert.Equal((null, 3, null), (_repo.Actualizado!.Value.Tipo, _repo.Actualizado.Value.Estado, _repo.Actualizado.Value.Resumen));
        Assert.Null(_repo.CamposGuardados);
    }

    // ---------- Confirmar ----------

    [Fact]
    public async Task Confirmar_sin_valores_se_rechaza()
    {
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.ConfirmarCamposAsync(1, 10, []));
        Assert.Equal("No hay valores para guardar.", e.Message);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public async Task Confirmar_con_un_valor_vacio_se_rechaza(string? valor)
    {
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.ConfirmarCamposAsync(1, 10, [new(1, "ok"), new(2, valor)]));
        Assert.Equal("Completa todos los campos antes de confirmar.", e.Message);
        Assert.Null(_repo.CamposConfirmados);
    }

    [Fact]
    public async Task El_valor_confirmado_admite_1000_caracteres_y_no_1001()
    {
        await Servicio.ConfirmarCamposAsync(1, 10, [new(1, new string('x', 1000))]);
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.ConfirmarCamposAsync(1, 10, [new(1, new string('x', 1001))]));
        Assert.Equal("Un valor supera los 1000 caracteres.", e.Message);
    }

    [Fact]
    public async Task Confirmar_guarda_los_valores_sin_espacios_sobrantes()
    {
        await Servicio.ConfirmarCamposAsync(1, 10, [new(1, "  hola  ")]);
        Assert.Equal([new CampoConfirmado(1, "hola")], _repo.CamposConfirmados);
    }

    [Fact]
    public async Task Confirmar_en_un_documento_ajeno_responde_404_y_no_guarda()
    {
        _repo.Documento = null;
        await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.ConfirmarCamposAsync(1, 10, [new(1, "x")]));
        Assert.Null(_repo.CamposConfirmados);
    }

    // ---------- Preguntar ----------

    [Theory]
    [InlineData("")] [InlineData("   ")]
    public async Task Una_pregunta_vacia_se_rechaza(string pregunta)
    {
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.PreguntarAsync(1, 10, pregunta, CancellationToken.None));
        Assert.Equal("Escribe una pregunta.", e.Message);
        Assert.Null(_ia.PreguntaRecibida);
    }

    [Fact]
    public async Task La_pregunta_admite_500_caracteres_y_no_501()
    {
        await Servicio.PreguntarAsync(1, 10, new string('p', 500), CancellationToken.None);
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.PreguntarAsync(1, 10, new string('p', 501), CancellationToken.None));
        Assert.Equal("La pregunta no puede superar 500 caracteres.", e.Message);
    }

    [Fact]
    public async Task Preguntar_por_un_documento_ajeno_responde_404()
    {
        _repo.Contenido = null;
        var e = await Assert.ThrowsAsync<ErrorNegocio>(() => Servicio.PreguntarAsync(1, 10, "hola", CancellationToken.None));
        Assert.Equal((404, "El documento no existe."), (e.Estado, e.Message));
    }

    [Fact]
    public async Task El_chat_manda_a_la_IA_solo_los_ultimos_seis_mensajes_con_sus_roles_y_guarda_el_intercambio()
    {
        _repo.Mensajes = Enumerable.Range(1, 8).Select(i => new FilaMensaje
        {
            CodMnsje = i, CodOrgenMnsje = i % 2 == 0 ? DocumentoRepositorio.OrigenAsistente : DocumentoRepositorio.OrigenUsuario, DscTexto = $"m{i}",
        }).ToList();

        var r = await Servicio.PreguntarAsync(1, 10, "  ¿Cuánto es el total?  ", CancellationToken.None);

        Assert.Equal("Respuesta de la IA", r.Respuesta);
        Assert.Equal("¿Cuánto es el total?", _ia.PreguntaRecibida);
        Assert.Equal(
            [("usuario", "m3"), ("asistente", "m4"), ("usuario", "m5"), ("asistente", "m6"), ("usuario", "m7"), ("asistente", "m8")],
            _ia.HistorialRecibido!.Select(m => (m.Rol, m.Texto)));
        Assert.Equal(
            [(DocumentoRepositorio.OrigenUsuario, "¿Cuánto es el total?"), (DocumentoRepositorio.OrigenAsistente, "Respuesta de la IA")],
            _repo.MensajesAgregados);
    }

    // ---------- Eliminar ----------

    [Fact]
    public async Task Eliminar_borra_el_documento_propio_y_no_toca_uno_ajeno()
    {
        await Servicio.EliminarAsync(1, 10);
        Assert.True(_repo.Eliminado);

        var otro = new DocumentosFalsos { Documento = null };
        var servicio = new DocumentoService(otro, _ia, Options.Create(new OpcionesArchivos()), NullLogger<DocumentoService>.Instance);
        await Assert.ThrowsAsync<ErrorNegocio>(() => servicio.EliminarAsync(1, 10));
        Assert.False(otro.Eliminado);
    }
}
