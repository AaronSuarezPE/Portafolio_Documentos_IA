<#
    Instala la base de datos de DocuIA ejecutando los scripts en orden.

    Local (LocalDB):
        .\instalar.ps1 -CrearBd

    Hosting o servidor remoto (la base ya existe):
        .\instalar.ps1 -Servidor "servidor,1433" -BaseDatos "mi_base" -Usuario "mi_usuario" -Clave "mi_clave"

    Se puede repetir sin problema: los scripts solo crean lo que falta.
#>
param(
    [string]$Servidor = "(localdb)\MSSQLLocalDB",
    [string]$BaseDatos = "docuia",
    [string]$Usuario,
    [string]$Clave,
    [switch]$CrearBd,
    [switch]$ConPruebas
)

$ErrorActionPreference = "Stop"
$carpeta = $PSScriptRoot

function Invoke-Script([string]$archivo, [string]$bd) {
    $argumentos = @("-S", $Servidor, "-d", $bd, "-b", "-I", "-f", "65001", "-i", $archivo)
    if ($Usuario) { $argumentos += @("-U", $Usuario, "-P", $Clave, "-C") }
    else { $argumentos += "-E" }

    Write-Host "-> $(Split-Path $archivo -Leaf)"
    & sqlcmd @argumentos
    if ($LASTEXITCODE -ne 0) { throw "Falló la ejecución de $archivo" }
}

if ($CrearBd) { Invoke-Script (Join-Path $carpeta "00_crear_bd.sql") "master" }

$scripts = "01_tablas.sql", "02_tipos_tabla.sql", "03_datos_maestros.sql",
           "04_pa_usuario.sql", "05_pa_documento.sql", "06_pa_mensaje_job.sql"
foreach ($s in $scripts) { Invoke-Script (Join-Path $carpeta $s) $BaseDatos }

if ($ConPruebas) { Invoke-Script (Join-Path $carpeta "pruebas\prueba_procedimientos.sql") $BaseDatos }

Write-Host "Base de datos lista."
