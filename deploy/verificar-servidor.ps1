#Requires -Version 5.1

<#
.SYNOPSIS
    Reads what the server already has, and changes nothing.

.DESCRIPTION
    This script exists so that provisionar.ps1 is not written on guesses. It answers the
    questions whose answers change what provisioning has to do, and it answers them from
    the machine instead of from a document.

    IT WRITES NOTHING. No folder is created, no pool is touched, no service is restarted,
    and nothing is installed. Every command below reads. That matters more than usual here,
    because this server is already serving another application to real people, and a script
    that "fixes what it finds" is exactly how that application goes down.

    Run it in a PowerShell console as an administrator. Some of the checks need elevation to
    answer at all, and the report says which ones could not be answered rather than
    reporting them as absent. An unanswered check and a negative answer are different
    things, and treating the first as the second is how a prerequisite gets installed twice
    or skipped once.

    Code and parameters are in English, like the rest of this repository. What the script
    prints is in Portuguese, because it is read by the person running it.

.EXAMPLE
    .\verificar-servidor.ps1
#>

[CmdletBinding()]
param(
    # Skips the pause at the end, for running this from another script.
    [switch] $NoPause
)

$ErrorActionPreference = "Continue"

#region Closing

function Wait-ForKey
{
    <#
        The last thing the script does, after the report is printed and after the exit code
        is already decided. It reads a key and nothing else, so it cannot change what the
        script is about to exit with.

        It exists because of how these are started. Opened by double click or by "Executar
        com o PowerShell", the console belongs to the script and closes with it, taking the
        report off the screen before anybody has read it.
    #>

    if ($script:NoPause)
    {
        return
    }

    # A pause needs a console with a person in front of it. Started from another script,
    # from a build, or with its input redirected, there is nobody to press a key and this
    # would hang for ever. The -NoPause switch only helps whoever remembered to pass it,
    # and the one who gets this wrong is precisely the one who did not know to pass it.
    if ([System.Console]::IsInputRedirected)
    {
        return
    }

    Write-Host ""
    Write-Host "Carregue numa tecla para fechar esta janela." -ForegroundColor Cyan

    try
    {
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
    catch
    {
        # Not every host has a raw key reader, the ISE among them. Falling back rather than
        # turning a finished run into a failure on its very last line.
        $null = Read-Host
    }
}


# Covers the ending nobody plans for. An unexpected terminating error would otherwise end
# the script, and close the window, with the error still on screen and unread. Break
# re-throws after the pause, so the script still ends the way it was going to end.
trap
{
    Wait-ForKey

    break
}

#endregion


#region Report

$script:unanswered = 0


function Write-Section
{
    param([string] $Title)

    Write-Host ""
    Write-Host "== $Title ==" -ForegroundColor Cyan
}


function Write-Finding
{
    param(
        [string] $Label,
        [string] $Value,
        [string] $Status
    )

    $colour = "Gray"

    if ($Status -eq "ok")
    {
        $colour = "Green"
    }

    if ($Status -eq "falta")
    {
        $colour = "Yellow"
    }

    if ($Status -eq "desconhecido")
    {
        $colour = "Magenta"
        $script:unanswered = $script:unanswered + 1
    }

    Write-Host ("  {0,-34} {1}" -f $Label, $Value) -ForegroundColor $colour
}


function Write-Note
{
    param([string] $Text)

    Write-Host "      $Text" -ForegroundColor DarkGray
}

#endregion


#region Elevation

function Test-Administrator
{
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)

    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

#endregion


Write-Host ""
Write-Host "Verificacao do servidor para o IdeaBoard. Nada e alterado." -ForegroundColor White

$isAdministrator = Test-Administrator

Write-Section "Consola"

if ($isAdministrator)
{
    Write-Finding "Privilegios" "Administrador" "ok"
}
else
{
    Write-Finding "Privilegios" "Sem elevacao" "falta"
    Write-Note "Varias verificacoes abaixo nao conseguem responder sem elevacao e aparecem"
    Write-Note "como DESCONHECIDO. Correr de novo numa consola de administrador."
}

Write-Finding "Maquina" $env:COMPUTERNAME "info"
Write-Finding "Sistema" ([System.Environment]::OSVersion.VersionString) "info"


#region ASP.NET Core Module

Write-Section "ASP.NET Core Module v2"

$modulePath = Join-Path $env:SystemRoot "System32\inetsrv\aspnetcorev2.dll"

if (Test-Path -LiteralPath $modulePath)
{
    $moduleVersion = (Get-Item -LiteralPath $modulePath).VersionInfo.FileVersion

    Write-Finding "aspnetcorev2.dll" $moduleVersion "ok"
}
else
{
    Write-Finding "aspnetcorev2.dll" "Nao encontrado" "falta"
    Write-Note "Instalar o ASP.NET Core Hosting Bundle correspondente ao .NET 10."
    Write-Note "Sem ele o site responde 500.19 ou 502, que e o mesmo erro de quando falta"
    Write-Note "a Autenticacao do Windows, por isso convem confirmar as duas coisas."
}

# The file being on disk and the module being registered with IIS are two different
# things, and only the second one makes the site work.
$webAdministrationAvailable = $false

try
{
    Import-Module WebAdministration -ErrorAction Stop
    $webAdministrationAvailable = $true
}
catch
{
    Write-Finding "Modulo WebAdministration" "Indisponivel" "desconhecido"
    Write-Note "Sem ele nao se consegue ler a configuracao do IIS a partir daqui."
    Write-Note "Causa habitual: o IIS nao esta instalado, ou a consola nao tem elevacao."
}

if ($webAdministrationAvailable)
{
    try
    {
        $registered = Get-WebGlobalModule -ErrorAction Stop | Where-Object { $_.Name -eq "AspNetCoreModuleV2" }

        if ($null -ne $registered)
        {
            Write-Finding "Registado no IIS" "Sim" "ok"
        }
        else
        {
            Write-Finding "Registado no IIS" "Nao" "falta"
        }
    }
    catch
    {
        Write-Finding "Registado no IIS" "Nao foi possivel ler" "desconhecido"
    }
}

#endregion


#region .NET runtimes

Write-Section "Runtimes do .NET"

$dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue

if ($null -eq $dotnet)
{
    Write-Finding "dotnet" "Nao esta no caminho" "falta"
    Write-Note "Sem o dotnet no caminho nao se consegue listar os runtimes daqui."
    Write-Note "O Hosting Bundle instala o runtime e o modulo ao mesmo tempo."
}
else
{
    $runtimes = & dotnet.exe --list-runtimes

    $aspNetCoreTen = $runtimes | Where-Object { $_ -like "Microsoft.AspNetCore.App 10.*" }

    if ($null -ne $aspNetCoreTen)
    {
        Write-Finding "Microsoft.AspNetCore.App 10" "Presente" "ok"
    }
    else
    {
        Write-Finding "Microsoft.AspNetCore.App 10" "Ausente" "falta"
        Write-Note "A aplicacao e .NET 10. Sem este runtime o site nao arranca."
    }

    Write-Note "Lista completa:"

    foreach ($runtime in $runtimes)
    {
        Write-Note "  $runtime"
    }
}

#endregion


#region Windows Authentication

Write-Section "Autenticacao do Windows no IIS"

$windowsAuthAnswered = $false

$serverManager = Get-Module -ListAvailable -Name ServerManager

if ($null -ne $serverManager)
{
    try
    {
        Import-Module ServerManager -ErrorAction Stop

        $feature = Get-WindowsFeature -Name Web-Windows-Auth -ErrorAction Stop

        $windowsAuthAnswered = $true

        if ($feature.Installed)
        {
            Write-Finding "Servico de funcao" "Instalado" "ok"
        }
        else
        {
            Write-Finding "Servico de funcao" "Nao instalado" "falta"
            Write-Note "Servidor Web, Seguranca, Autenticacao do Windows. Nao vem por omissao."
            Write-Note "Sem ele o web.config da aplicacao traz um elemento que o servidor nao"
            Write-Note "reconhece, e a autenticacao integrada nao existe sequer como opcao."
        }
    }
    catch
    {
        Write-Finding "Servico de funcao" "Nao foi possivel ler" "desconhecido"
    }
}

if (-not $windowsAuthAnswered)
{
    # A client edition of Windows has no ServerManager. The module registration answers the
    # same question from the other side, and is worth having either way.
    if ($webAdministrationAvailable)
    {
        try
        {
            $authModule = Get-WebGlobalModule -ErrorAction Stop |
                Where-Object { $_.Name -eq "WindowsAuthenticationModule" }

            if ($null -ne $authModule)
            {
                Write-Finding "WindowsAuthenticationModule" "Registado" "ok"
            }
            else
            {
                Write-Finding "WindowsAuthenticationModule" "Nao registado" "falta"
            }
        }
        catch
        {
            Write-Finding "WindowsAuthenticationModule" "Nao foi possivel ler" "desconhecido"
        }
    }
    else
    {
        Write-Finding "Autenticacao do Windows" "Nao foi possivel ler" "desconhecido"
    }
}

#endregion


#region What is already running

Write-Section "O que a maquina ja serve"

Write-Note "Isto existe para o provisionamento nao colidir com o que ja la esta."

if (-not $webAdministrationAvailable)
{
    Write-Finding "Sites e pools" "Nao foi possivel ler" "desconhecido"
}
else
{
    try
    {
        $sites = Get-Website -ErrorAction Stop

        if ($null -eq $sites -or $sites.Count -eq 0)
        {
            Write-Finding "Sites" "Nenhum" "info"
        }
        else
        {
            Write-Finding "Sites" ("{0} encontrados" -f @($sites).Count) "info"

            foreach ($site in $sites)
            {
                $bindings = ($site.bindings.Collection | ForEach-Object { $_.bindingInformation }) -join ", "

                Write-Note ("{0}  [{1}]  pool: {2}" -f $site.Name, $site.State, $site.applicationPool)
                Write-Note ("    bindings: {0}" -f $bindings)
                Write-Note ("    caminho:  {0}" -f $site.physicalPath)
            }
        }
    }
    catch
    {
        Write-Finding "Sites" "Nao foi possivel ler" "desconhecido"
    }

    try
    {
        $pools = Get-ChildItem -Path "IIS:\AppPools" -ErrorAction Stop

        Write-Finding "Pools de aplicacoes" ("{0} encontrados" -f @($pools).Count) "info"

        foreach ($pool in $pools)
        {
            Write-Note ("{0}  [{1}]  CLR: '{2}'  arranque: {3}" -f `
                $pool.Name, $pool.State, $pool.managedRuntimeVersion, $pool.startMode)
        }
    }
    catch
    {
        Write-Finding "Pools de aplicacoes" "Nao foi possivel ler" "desconhecido"
    }
}

#endregion


#region Folders

Write-Section "Pastas"

$folders = @(
    "C:\IdeaBoard",
    "C:\IdeaBoard\data",
    "C:\IdeaBoard\keys",
    "C:\IdeaBoard\backup",
    "C:\inetpub\IdeaBoard"
)

foreach ($folder in $folders)
{
    if (Test-Path -LiteralPath $folder)
    {
        Write-Finding $folder "Existe" "ok"
    }
    else
    {
        Write-Finding $folder "Nao existe" "falta"
    }
}

$databaseFile = "C:\IdeaBoard\data\ideaboard.db"

if (Test-Path -LiteralPath $databaseFile)
{
    $database = Get-Item -LiteralPath $databaseFile

    Write-Finding "ideaboard.db" ("Existe, {0} bytes, alterado {1}" -f $database.Length, $database.LastWriteTime) "ok"
    Write-Note "ATENCAO: ja ha uma base de dados. Isto nao e uma instalacao de raiz."
    Write-Note "Nesse caso NAO se aplica o migracao.sql completo. Ver Docs/06, a tabela"
    Write-Note "que diz qual dos tres scripts aplicar."
}
else
{
    Write-Finding "ideaboard.db" "Nao existe" "falta"
    Write-Note "Base de dados nova, portanto leva o deploy\migracao.sql completo."
}

#endregion


#region Event Log source

Write-Section "Origem do Event Log"

try
{
    $sourceExists = [System.Diagnostics.EventLog]::SourceExists("IdeaBoard")

    if ($sourceExists)
    {
        Write-Finding "Origem 'IdeaBoard'" "Existe" "ok"
    }
    else
    {
        Write-Finding "Origem 'IdeaBoard'" "Nao existe" "falta"
        Write-Note "A aplicacao escreve para esta origem e NAO a cria, de proposito, porque"
        Write-Note "criar uma origem escreve em HKLM e o pool nao deve ter essa permissao."
        Write-Note "O sintoma de ficar por fazer e nao haver sintoma nenhum: as paginas"
        Write-Note "servem, as pessoas votam, e o registo de erros simplesmente nao existe."
    }
}
catch
{
    Write-Finding "Origem 'IdeaBoard'" "Nao foi possivel ler" "desconhecido"
    Write-Note "Ler o registo de origens precisa de elevacao nalgumas maquinas."
}

#endregion


#region sqlite3

Write-Section "sqlite3"

$sqlite = Get-Command sqlite3.exe -ErrorAction SilentlyContinue

if ($null -ne $sqlite)
{
    $sqliteVersion = & sqlite3.exe -version

    Write-Finding "sqlite3.exe" $sqlite.Source "ok"
    Write-Note "Versao: $sqliteVersion"
}
else
{
    Write-Finding "sqlite3.exe" "Nao esta no caminho" "falta"
    Write-Note "E preciso para aplicar as migracoes e para o backup, conforme Docs/06."
    Write-Note "Descarregar os sqlite-tools para Windows em https://sqlite.org/download.html,"
    Write-Note "extrair para uma pasta, e ou acrescentar essa pasta ao PATH ou invocar o"
    Write-Note "executavel pelo caminho completo. Nao precisa de instalacao."
    Write-Note "O comando a aplicar leva sempre -bail, conforme Docs/06."
}

#endregion


#region Summary

Write-Section "Resumo"

if ($script:unanswered -gt 0)
{
    Write-Host ("  {0} verificacao(oes) ficaram por responder." -f $script:unanswered) -ForegroundColor Magenta
    Write-Host "  Uma verificacao sem resposta NAO e a mesma coisa que uma resposta negativa." -ForegroundColor Magenta
    Write-Host "  Resolver isso antes de provisionar, em vez de assumir o pior ou o melhor." -ForegroundColor Magenta
}
else
{
    Write-Host "  Todas as verificacoes responderam." -ForegroundColor Green
}

Write-Host ""
Write-Host "  Nada foi alterado nesta maquina por este script." -ForegroundColor White
Write-Host ""

#endregion


# The last line of the script, on purpose. Everything above has already decided what this
# run means, and this only holds the window open so it can be read.
Wait-ForKey
