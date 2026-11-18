using DocuIA.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocuIA.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/docuia/[controller]")]
public sealed class SaludController(IProveedorIA proveedor) : ControllerBase
{
    [HttpGet("Get")]
    public IActionResult Get() => Ok(new { estado = "ok", modoDemostracion = proveedor.EsDemostracion });
}
