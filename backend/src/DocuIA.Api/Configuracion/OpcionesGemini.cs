namespace DocuIA.Api.Configuracion;

public sealed class OpcionesGemini
{
    public const string Seccion = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Modelo { get; set; } = "gemini-2.5-flash";
    public string UrlBase { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public bool Habilitado => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class OpcionesArchivos
{
    public const string Seccion = "Archivos";

    public int TamanoMaximoMb { get; set; } = 5;
}

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    public string Clave { get; set; } = string.Empty;
    public string Emisor { get; set; } = "docuia";
    public string Audiencia { get; set; } = "docuia";
    public int MinutosVigencia { get; set; } = 120;
}

public sealed class OpcionesLimpieza
{
    public const string Seccion = "Limpieza";

    public int DiasRetencion { get; set; } = 7;
    public int HorasIntervalo { get; set; } = 6;
}
