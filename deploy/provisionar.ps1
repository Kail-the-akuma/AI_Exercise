#Requires -Version 5.1

<#
.SYNOPSIS
    Creates the folders, the application pool and the site for the IdeaBoard, and touches
    nothing that was already there.

.DESCRIPTION
    This server already serves another application to real people. Everything below is
    written around that one fact.

    IT NEVER RUNS A GLOBAL COMMAND. There is no iisreset here, the World Wide Web
    Publishing Service is never stopped, and no pool or site that this script did not create
    is started, stopped or reconfigured. A global command takes the other application down
    with it, and the person running this would have no reason to connect the two.

    IT REFUSES RATHER THAN REPAIRS. If the pool, the site or the port is already taken, the
    script stops and says so instead of configuring over what it found. A script that
    quietly corrects what it meets is how the application next door gets broken by somebody
    who was trying to be helpful.

    Run it in a PowerShell console as an administrator, on the server.

    Code and parameters are in English, like the rest of this repository. What the script
    prints is in Portuguese, because it is read by the person running it.

.PARAMETER PoolName
    Name of the application pool to create. It must not exist yet.

.PARAMETER SiteName
    Name of the site to create. It must not exist yet.

.PARAMETER Port
    HTTP port for the site. It must not be bound by another site.

.PARAMETER PhysicalPath
    Folder the published application will live in, for example C:\inetpub\IdeaBoard.

.PARAMETER DataRoot
    Root of the folders that survive a publication. The default is the path Docs/06 fixes,
    and the script prints it so that it is never silently assumed.

.EXAMPLE
    .\provisionar.ps1 -PoolName IdeaBoard -SiteName IdeaBoard -Port 8080 -PhysicalPath C:\inetpub\IdeaBoard
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PoolName,

    [Parameter(Mandatory = $true)]
    [string] $SiteName,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int] $Port,

    [Parameter(Mandatory = $true)]
    [string] $PhysicalPath,

    [string] $DataRoot = "C:\IdeaBoard"
)

$ErrorActionPreference = "Stop"


#region Output

function Write-Step
{
    param([string] $Text)

    Write-Host ""
    Write-Host "-> $Text" -ForegroundColor Cyan
}


function Write-Done
{
    param([string] $Text)

    Write-Host "   OK: $Text" -ForegroundColor Green
}


function Write-Kept
{
    param([string] $Text)

    Write-Host "   JA ESTAVA: $Text" -ForegroundColor DarkGray
}


function Stop-WithReason
{
    param([string] $Text)

    Write-Host ""
    Write-Host "PARADO. $Text" -ForegroundColor Red
    Write-Host ""
    Write-Host "Nada foi alterado a partir deste ponto. Nenhum pool ou site existente foi tocado." -ForegroundColor Red
    Write-Host ""

    exit 1
}

#endregion


#region Preconditions

Write-Host ""
Write-Host "Provisionamento do IdeaBoard no IIS." -ForegroundColor White
Write-Host ""
Write-Host "  Pool:     $PoolName"
Write-Host "  Site:     $SiteName"
Write-Host "  Porta:    $Port"
Write-Host "  Aplicacao: $PhysicalPath"
Write-Host "  Dados:    $DataRoot"
Write-Host ""
Write-Host "  O caminho dos dados nao foi adivinhado: ou veio por parametro, ou e o que o"
Write-Host "  Docs/06 fixa. Esta aqui escrito para ser lido antes de continuar."

Write-Step "A confirmar que a consola tem elevacao"

$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object System.Security.Principal.WindowsPrincipal($identity)

if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator))
{
    Stop-WithReason "Esta consola nao e de administrador. Criar um pool, um site e uma origem de Event Log precisa de elevacao."
}

Write-Done "Administrador"


Write-Step "A carregar o modulo de administracao do IIS"

try
{
    Import-Module WebAdministration -ErrorAction Stop
}
catch
{
    Stop-WithReason "Nao foi possivel carregar o WebAdministration. O IIS esta instalado nesta maquina?"
}

Write-Done "WebAdministration carregado"


Write-Step "A confirmar os pre requisitos do servidor"

# Both of these produce the same error from the site, 500.19 or 502, so a site created
# without them looks broken for a reason that has nothing to do with the application.
$modulePath = Join-Path $env:SystemRoot "System32\inetsrv\aspnetcorev2.dll"

if (-not (Test-Path -LiteralPath $modulePath))
{
    Stop-WithReason "Falta o ASP.NET Core Module v2. Instalar o Hosting Bundle do .NET 10 e correr primeiro o verificar-servidor.ps1."
}

Write-Done "ASP.NET Core Module v2 presente"

$windowsAuthModule = Get-WebGlobalModule | Where-Object { $_.Name -eq "WindowsAuthenticationModule" }

if ($null -eq $windowsAuthModule)
{
    Stop-WithReason "Falta o servico de funcao Autenticacao do Windows. Sem ele a aplicacao nao tem como autenticar ninguem. Correr primeiro o verificar-servidor.ps1."
}

Write-Done "Autenticacao do Windows registada"

#endregion


#region Refuse to reconfigure what exists

Write-Step "A confirmar que nada com estes nomes ja existe"

if (Test-Path -LiteralPath ("IIS:\AppPools\" + $PoolName))
{
    Stop-WithReason "Ja existe um pool chamado '$PoolName'. Este script nao reconfigura o que encontra. Escolher outro nome, ou apagar o existente a mao depois de confirmar que nao e de outra aplicacao."
}

Write-Done "Nao ha pool chamado '$PoolName'"

$existingSite = Get-Website | Where-Object { $_.Name -eq $SiteName }

if ($null -ne $existingSite)
{
    Stop-WithReason "Ja existe um site chamado '$SiteName'. Mesma razao."
}

Write-Done "Nao ha site chamado '$SiteName'"

# A port taken by another site is the collision that actually takes the neighbour down,
# because two sites bound to the same port cannot both start.
foreach ($site in Get-Website)
{
    foreach ($binding in $site.bindings.Collection)
    {
        $parts = $binding.bindingInformation -split ":"

        if ($parts.Length -ge 2)
        {
            if ($parts[1] -eq $Port.ToString())
            {
                Stop-WithReason "A porta $Port ja esta ligada ao site '$($site.Name)'. Dois sites na mesma porta nao arrancam os dois, e o que ja la esta serve pessoas."
            }
        }
    }
}

Write-Done "A porta $Port esta livre"

#endregion


#region Folders

Write-Step "A criar as pastas que sobrevivem a publicacao"

$dataPath = Join-Path $DataRoot "data"
$keysPath = Join-Path $DataRoot "keys"
$backupPath = Join-Path $DataRoot "backup"

$folders = @($DataRoot, $dataPath, $keysPath, $backupPath, $PhysicalPath)

foreach ($folder in $folders)
{
    if (Test-Path -LiteralPath $folder)
    {
        Write-Kept $folder
    }
    else
    {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null

        Write-Done ("Criada " + $folder)
    }
}

# Reusing an existing folder is harmless. Finding a database in it is not: it means this is
# not a fresh installation, and the full migration script would fail on the first statement.
$databaseFile = Join-Path $dataPath "ideaboard.db"

if (Test-Path -LiteralPath $databaseFile)
{
    Write-Host ""
    Write-Host "   ATENCAO: ja existe uma base de dados em $databaseFile." -ForegroundColor Yellow
    Write-Host "   Isto NAO e uma instalacao de raiz. Nao aplicar o migracao.sql completo." -ForegroundColor Yellow
    Write-Host "   Ver a tabela do Docs/06 que diz qual dos tres scripts aplicar." -ForegroundColor Yellow
    Write-Host "   Este script nao toca em ficheiro nenhum dentro de $DataRoot." -ForegroundColor Yellow
}

#endregion


#region Application pool

Write-Step "A criar o pool de aplicacoes '$PoolName'"

New-WebAppPool -Name $PoolName | Out-Null

Write-Done "Pool criado"

Write-Step "A configurar o pool"

# No managed code: the application does not use the IIS CLR.
Set-ItemProperty -Path ("IIS:\AppPools\" + $PoolName) -Name "managedRuntimeVersion" -Value ""
Write-Done "Versao do .NET CLR: Sem Codigo Gerido"

# One worker process, and this is the setting that is easiest to forget and hardest to
# diagnose afterwards. SQLite takes one writer; a second process produces locking and
# corrupted expectations. ADR-002.
Set-ItemProperty -Path ("IIS:\AppPools\" + $PoolName) -Name "processModel.maxProcesses" -Value 1
Write-Done "Maximo de processos de trabalho: 1"

Set-ItemProperty -Path ("IIS:\AppPools\" + $PoolName) -Name "startMode" -Value "AlwaysRunning"
Write-Done "Modo de arranque: AlwaysRunning"

Set-ItemProperty -Path ("IIS:\AppPools\" + $PoolName) -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
Write-Done "Tempo limite de inatividade: 0"

# The periodic restart every 29 hours lands in the middle of a working day sooner or later.
Set-ItemProperty -Path ("IIS:\AppPools\" + $PoolName) -Name "recycling.periodicRestart.time" -Value ([TimeSpan]::Zero)
Write-Done "Reciclagem por intervalo regular: 0"

#endregion


#region Permissions

Write-Step "A dar permissoes a identidade do pool"

Write-Host "   A identidade so existe depois de o pool existir, por isso este passo vem aqui" -ForegroundColor DarkGray
Write-Host "   e nao antes. 'IIS AppPool\$PoolName' nao resolve enquanto o pool nao for criado." -ForegroundColor DarkGray

$principalName = "IIS AppPool\" + $PoolName

# Modify ON THE FOLDER, not just on the file. SQLite creates and deletes the -wal and -shm
# files beside the database, and without write permission on the folder the result is a
# read-only database error although the file itself has the right permissions.
foreach ($folder in @($dataPath, $keysPath))
{
    $output = & icacls.exe $folder /grant ($principalName + ":(OI)(CI)M")

    if ($LASTEXITCODE -ne 0)
    {
        Write-Host $output -ForegroundColor Red

        Stop-WithReason "Nao foi possivel dar Modificar a '$principalName' sobre $folder."
    }

    Write-Done ("Modificar sobre a pasta " + $folder)
}

#endregion


#region Event Log source

Write-Step "A criar a origem do Event Log"

$sourceExists = $false

try
{
    $sourceExists = [System.Diagnostics.EventLog]::SourceExists("IdeaBoard")
}
catch
{
    Stop-WithReason "Nao foi possivel ler as origens do Event Log."
}

if ($sourceExists)
{
    Write-Kept "A origem 'IdeaBoard' ja existe"
}
else
{
    New-EventLog -LogName Application -Source "IdeaBoard"

    Write-Done "Origem 'IdeaBoard' criada no log Application"
}

Write-Host "   A aplicacao escreve para esta origem e NAO a cria, de proposito: criar uma" -ForegroundColor DarkGray
Write-Host "   origem escreve em HKLM, e o pool nao deve ter essa permissao." -ForegroundColor DarkGray

#endregion


#region Site

Write-Step "A criar o site '$SiteName'"

New-Website -Name $SiteName -Port $Port -PhysicalPath $PhysicalPath -ApplicationPool $PoolName | Out-Null

Write-Done "Site criado na porta $Port, sobre $PhysicalPath"

Write-Step "A destrancar a seccao de autenticacao, SO para este site"

Write-Host "   O web.config que a publicacao produz traz a seccao de autenticacao dentro." -ForegroundColor DarkGray
Write-Host "   O IIS tranca essa seccao ao nivel do servidor por omissao, e um site cujo" -ForegroundColor DarkGray
Write-Host "   web.config a traga responde 500.19 em vez de arrancar." -ForegroundColor DarkGray
Write-Host "   Isto e feito com -Location '$SiteName', portanto escreve uma entrada so para" -ForegroundColor DarkGray
Write-Host "   este site e NAO altera o que vale para os outros." -ForegroundColor DarkGray

foreach ($section in @("windowsAuthentication", "anonymousAuthentication"))
{
    Set-WebConfiguration `
        -Filter ("/system.webServer/security/authentication/" + $section) `
        -PSPath "IIS:\" `
        -Location $SiteName `
        -Metadata "overrideMode" `
        -Value "Allow"

    Write-Done ($section + " destrancada para este site")
}

Write-Host "   NAO VERIFICADO NUMA MAQUINA COM IIS a partir daqui, e por isso fica dito." -ForegroundColor Yellow
Write-Host "   Se o site responder 500.19 a falar de uma seccao que nao pode ser usada neste" -ForegroundColor Yellow
Write-Host "   caminho, e isto que falhou, e e aqui que se mexe." -ForegroundColor Yellow


Write-Step "A configurar a autenticacao do site"

# The two go together. With anonymous on, IIS serves the request without challenging
# anybody, the application receives requests with no identity, and the authorisation policy
# refuses everybody instead of authenticating them.
Set-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/windowsAuthentication" `
    -Name "enabled" `
    -Value $true `
    -PSPath "IIS:\" `
    -Location $SiteName

Write-Done "Autenticacao Windows: ativada"

Set-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
    -Name "enabled" `
    -Value $false `
    -PSPath "IIS:\" `
    -Location $SiteName

Write-Done "Autenticacao anonima: desligada"

#endregion


#region Confirmation

Write-Step "A reler o que ficou, em vez de confiar no que foi escrito"

$pool = Get-Item -Path ("IIS:\AppPools\" + $PoolName)

Write-Host ("   Pool '{0}': CLR '{1}', maxProcesses {2}, startMode {3}" -f `
    $pool.Name, $pool.managedRuntimeVersion, $pool.processModel.maxProcesses, $pool.startMode)

$createdSite = Get-Website | Where-Object { $_.Name -eq $SiteName }

$bindings = ($createdSite.bindings.Collection | ForEach-Object { $_.bindingInformation }) -join ", "

Write-Host ("   Site '{0}': estado {1}, pool {2}, bindings {3}" -f `
    $createdSite.Name, $createdSite.State, $createdSite.applicationPool, $bindings)

$windowsAuthEnabled = (Get-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/windowsAuthentication" `
    -Name "enabled" `
    -PSPath "IIS:\" `
    -Location $SiteName).Value

$anonymousEnabled = (Get-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
    -Name "enabled" `
    -PSPath "IIS:\" `
    -Location $SiteName).Value

Write-Host ("   Autenticacao: Windows {0}, anonima {1}" -f $windowsAuthEnabled, $anonymousEnabled)

if ($windowsAuthEnabled -ne $true -or $anonymousEnabled -ne $false)
{
    Stop-WithReason "A autenticacao nao ficou como devia. Verificar a mao antes de continuar."
}

#endregion


#region What comes next

Write-Host ""
Write-Host "Provisionamento concluido. O site existe e ainda nao tem aplicacao nenhuma." -ForegroundColor White
Write-Host ""
Write-Host "A seguir, por esta ordem:" -ForegroundColor White
Write-Host "  1. Criar a base de dados com o script completo, numa base NOVA:"
Write-Host "       Get-Content deploy\migracao.sql | sqlite3 -bail `"$databaseFile`""
Write-Host "       (nao usar '<': no PowerShell e operador reservado e a linha nem chega a correr)" -ForegroundColor DarkGray
Write-Host "  2. Ligar o modo WAL, uma vez, conforme Docs/06:"
Write-Host "       sqlite3 -bail `"$databaseFile`" `"PRAGMA journal_mode = WAL;`""
Write-Host "  3. Publicar e copiar com o publicar.ps1."
Write-Host "  4. Percorrer a lista de verificacao manual do Docs/07, que e a parte que"
Write-Host "     nenhum teste automatico cobre: o desafio de autenticacao, a consulta ao"
Write-Host "     diretorio, e a criacao do utilizador a partir de uma identidade verdadeira."
Write-Host ""
Write-Host "Nenhum pool ou site alheio foi tocado por este script." -ForegroundColor White
Write-Host ""

#endregion
