using DocuIA.Api.Servicios;

namespace DocuIA.Api.Tests;

public class ValidadorArchivoTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\n"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    [Theory]
    [InlineData("factura.pdf", "application/pdf")]
    [InlineData("foto.PNG", "image/png")]
    [InlineData("scan.jpeg", "image/jpeg")]
    [InlineData("scan.jpg", "image/jpeg")]
    public void Acepta_archivos_cuyo_contenido_coincide_con_la_extension(string nombre, string tipoEsperado)
    {
        var contenido = tipoEsperado switch
        {
            "application/pdf" => Pdf,
            "image/png" => Png,
            _ => Jpg,
        };

        var resultado = ValidadorArchivo.Validar(nombre, contenido, 5);

        Assert.True(resultado.EsValido);
        Assert.Equal(tipoEsperado, resultado.TipoMime);
    }

    [Fact]
    public void Rechaza_un_archivo_vacio()
    {
        var resultado = ValidadorArchivo.Validar("a.pdf", [], 5);

        Assert.False(resultado.EsValido);
    }

    [Fact]
    public void Rechaza_extensiones_no_permitidas()
    {
        var resultado = ValidadorArchivo.Validar("programa.exe", Pdf, 5);

        Assert.False(resultado.EsValido);
        Assert.Contains("Formato", resultado.Error);
    }

    [Fact]
    public void Rechaza_un_archivo_que_no_es_lo_que_dice_su_extension()
    {
        var resultado = ValidadorArchivo.Validar("falso.pdf", Png, 5);

        Assert.False(resultado.EsValido);
    }

    [Fact]
    public void Rechaza_un_archivo_mas_grande_que_el_maximo()
    {
        var grande = new byte[(1024 * 1024) + 1];
        Pdf.CopyTo(grande, 0);

        var resultado = ValidadorArchivo.Validar("grande.pdf", grande, 1);

        Assert.False(resultado.EsValido);
        Assert.Contains("1 MB", resultado.Error);
    }
}

public class ClaveServiceTests
{
    private readonly ClaveService _servicio = new();

    [Fact]
    public void Verifica_la_clave_correcta_y_rechaza_otra()
    {
        var hash = _servicio.Generar("MiClaveSegura1");

        Assert.True(_servicio.Verificar("MiClaveSegura1", hash));
        Assert.False(_servicio.Verificar("otraclave", hash));
    }

    [Fact]
    public void Dos_hashes_de_la_misma_clave_son_distintos()
    {
        Assert.NotEqual(_servicio.Generar("igual"), _servicio.Generar("igual"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("texto-sin-formato")]
    [InlineData("1.no-es-base64.tampoco")]
    public void Un_hash_guardado_invalido_nunca_es_correcto(string? hash)
    {
        Assert.False(_servicio.Verificar("cualquiera", hash));
    }
}
