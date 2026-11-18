using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DocuIA.Api.Configuracion;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocuIA.Api.Servicios;

public interface ITokenService
{
    string Crear(int codUsuario, string nombre, bool esInvitado);
}

public sealed class TokenService(IOptions<OpcionesJwt> opciones) : ITokenService
{
    private readonly OpcionesJwt _opciones = opciones.Value;

    public string Crear(int codUsuario, string nombre, bool esInvitado)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, codUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, nombre),
            new Claim("invitado", esInvitado ? "S" : "N"),
        };

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _opciones.Emisor,
            _opciones.Audiencia,
            claims,
            expires: DateTime.UtcNow.AddMinutes(_opciones.MinutosVigencia),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
