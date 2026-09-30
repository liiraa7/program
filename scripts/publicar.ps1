<#
.SYNOPSIS
    Gera a versão distribuível do Padronizador de Estações.

.DESCRIPTION
    Publica um único PadronizadorTI.exe autocontido (não precisa do .NET instalado
    na máquina recém-formatada), copia a configuração e o README para dist\PadronizadorTI
    e cria dist\PadronizadorTI-<versão>.zip.

    Se existir config\padronizacao.json (arquivo REAL, fora do Git), ele é incluído;
    caso contrário, é incluído o arquivo de exemplo.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\publicar.ps1
#>
[CmdletBinding()]
param(
    [string]$Versao = "1.0.0"
)

$ErrorActionPreference = "Stop"
$raiz    = Split-Path -Parent $PSScriptRoot
$projeto = Join-Path $raiz "src\PadronizadorTI\PadronizadorTI.csproj"
$saida   = Join-Path $raiz "dist\PadronizadorTI"

if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }

Write-Host "Publicando versão $Versao..." -ForegroundColor Cyan
dotnet publish $projeto -p:PublishProfile=win-x64 -p:Version=$Versao
if ($LASTEXITCODE -ne 0) { throw "Falha no dotnet publish (código $LASTEXITCODE)." }

Copy-Item (Join-Path $raiz "README.md") $saida
Copy-Item (Join-Path $raiz "config\padronizacao.exemplo.json") $saida

$zip = Join-Path $raiz "dist\PadronizadorTI-$Versao.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $saida "*") -DestinationPath $zip

Write-Host ""
Write-Host "Pronto:" -ForegroundColor Green
Write-Host "  Pasta: $saida"
Write-Host "  ZIP  : $zip"
if (-not (Test-Path (Join-Path $raiz "config\padronizacao.json"))) {
    Write-Host "  ATENÇÃO: foi incluída a configuração de EXEMPLO. Edite padronizacao.json antes de usar." -ForegroundColor Yellow
}
