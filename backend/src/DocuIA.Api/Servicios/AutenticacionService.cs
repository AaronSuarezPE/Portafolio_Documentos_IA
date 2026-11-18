using System.Net.Mail;
using System.Security.Cryptography;
using DocuIA.Api.Configuracion;
using DocuIA.Api.Datos;
using DocuIA.Api.Modelos;
using Microsoft.Extensions.Options;

namespace DocuIA.Api.Servicios;

public interface IAutenticacionService
{
    Task<RespuestaSesion> LoginAsync(SolicitudLogin solicitud);
    Task<RespuestaSesion> AgregarUsuarioAsync(SolicitudUsuario solicitud);
    Task<RespuestaSesion> LoginInvitadoAsync();
}

public sealed class AutenticacionService(
    IUsuarioRepositorio usuarios,
    IClaveService claves,
    ITokenService tokens,
    IOptions<OpcionesJwt> opcionesJwt,
    ILogger<AutenticacionService> logger) : IAutenticacionService
{
    private const int LargoMinimoClave = 8;
    private const string MensajeCredenciales = "El correo o la clave no son correctos.";

    // Se verifica contra un hash falso cuando el correo no existe, para que el tiempo de respuesta no delate si existe.
    private static readonly string HashFalso = new ClaveService().Generar("clave-de-relleno");

    public async Task<RespuestaSesion> LoginAsync(SolicitudLogin solicitud)
    {
        var email = Normalizar(solicitud.Email);
        var clave = solicitud.Clave ?? string.Empty;

        if (email.Length == 0 || clave.Length == 0)
            throw new ErrorNegocio("Ingresa tu correo y tu clave.");

        var usuario = await usuarios.ObtenerPorEmailAsync(email);
        var correcta = claves.Verificar(clave, usuario?.NomClaveHsh ?? HashFalso);

        if (usuario is null || !correcta)
        {
            logger.LogWarning("Intento de ingreso rechazado para un correo registrado: {Existe}.", usuario is not null);
            throw new ErrorNegocio(MensajeCredenciales, StatusCodes.Status401Unauthorized);
        }

        await usuarios.RegistrarIngresoAsync(usuario.CodUsrio);
        return CrearSesion(usuario.CodUsrio, usuario.NomUsrio, esInvitado: false);
    }

    public async Task<RespuestaSesion> AgregarUsuarioAsync(SolicitudUsuario solicitud)
    {
        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var email = Normalizar(solicitud.Email);
        var clave = solicitud.Clave ?? string.Empty;

        if (nombre.Length < 2 || nombre.Length > 100)
            throw new ErrorNegocio("El nombre debe tener entre 2 y 100 caracteres.");

        if (!EsCorreoValido(email))
            throw new ErrorNegocio("Ingresa un correo válido.");

        if (clave.Length < LargoMinimoClave || clave.Length > 100)
            throw new ErrorNegocio($"La clave debe tener entre {LargoMinimoClave} y 100 caracteres.");

        var codigo = await usuarios.AgregarAsync(nombre, email, claves.Generar(clave), esInvitado: false);
        await usuarios.RegistrarIngresoAsync(codigo);

        logger.LogInformation("Usuario registrado con código {Codigo}.", codigo);
        return CrearSesion(codigo, nombre, esInvitado: false);
    }

    public async Task<RespuestaSesion> LoginInvitadoAsync()
    {
        var nombre = $"Invitado {RandomNumberGenerator.GetInt32(1000, 10000)}";
        var codigo = await usuarios.AgregarAsync(nombre, null, null, esInvitado: true);

        logger.LogInformation("Sesión de invitado creada con código {Codigo}.", codigo);
        return CrearSesion(codigo, nombre, esInvitado: true);
    }

    private RespuestaSesion CrearSesion(int codigo, string nombre, bool esInvitado) =>
        new(tokens.Crear(codigo, nombre, esInvitado), nombre, esInvitado, opcionesJwt.Value.MinutosVigencia);

    private static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static bool EsCorreoValido(string email)
    {
        if (email.Length is 0 or > 150)
            return false;

        return MailAddress.TryCreate(email, out var direccion) && direccion.Address == email && email.Contains('.');
    }
}
