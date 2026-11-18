namespace DocuIA.Api.Datos;

// Una clase por conjunto de resultados de los procedimientos almacenados.
// Dapper relaciona cada columna (cod_docto) con su propiedad (CodDocto).

public sealed class FilaResultado
{
    public int CodResultado { get; set; }
    public string DscMensaje { get; set; } = string.Empty;
}

public sealed class FilaUsuario
{
    public int CodUsrio { get; set; }
    public string NomUsrio { get; set; } = string.Empty;
    public string? NomEmail { get; set; }
    public string? NomClaveHsh { get; set; }
    public string FlgIvtdo { get; set; } = "N";
    public string FlgActvo { get; set; } = "S";
}

public sealed class FilaTipoDocumento
{
    public int CodTpoDocto { get; set; }
    public string NomTpoDocto { get; set; } = string.Empty;
    public string DscTpoDocto { get; set; } = string.Empty;
}

public sealed class FilaDocumento
{
    public int CodDocto { get; set; }
    public string NomArchv { get; set; } = string.Empty;
    public int? CodTpoDocto { get; set; }
    public string NomTpoDocto { get; set; } = string.Empty;
    public int CodEstdoDocto { get; set; }
    public string NomEstdoDocto { get; set; } = string.Empty;
    public int NumTamno { get; set; }
    public string DscRsmen { get; set; } = string.Empty;
    public string FecRgtro { get; set; } = string.Empty;
    public string HorRgtro { get; set; } = string.Empty;
}

public sealed class FilaCampo
{
    public int CodCampo { get; set; }
    public string NomClave { get; set; } = string.Empty;
    public string NomEtqta { get; set; } = string.Empty;
    public string DscValor { get; set; } = string.Empty;
    public string? DscValorCnfrm { get; set; }
    public decimal NumCnfnz { get; set; }
}

public sealed class FilaMensaje
{
    public int CodMnsje { get; set; }
    public int CodOrgenMnsje { get; set; }
    public string NomOrgenMnsje { get; set; } = string.Empty;
    public string DscTexto { get; set; } = string.Empty;
    public string FecRgtro { get; set; } = string.Empty;
    public string HorRgtro { get; set; } = string.Empty;
}

public sealed class FilaContenido
{
    public string NomArchv { get; set; } = string.Empty;
    public string TpoCntnd { get; set; } = string.Empty;
    public byte[] BinCntnd { get; set; } = [];
}
