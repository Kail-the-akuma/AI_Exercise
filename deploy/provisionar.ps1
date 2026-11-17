#Requires -Version 5.1

<#
.SYNOPSIS
    Creates the folders and the application pool for the IdeaBoard, and then either an
    application inside a site that already exists, which is the default, or a site of its
    own. It touches nothing that was already there.

.DESCRIPTION
    This server already serves another application to real people. Everything below is
    written around that one fact.

    IT NEVER RUNS A GLOBAL COMMAND. There is no iisreset here, the World Wide Web
    Publishing Service is never stopped, and no pool or site that this script did not create
    is started, stopped or reconfigured. A global command takes the other application down
    with it, and the person running this would have no reason to connect the two.

    IT REFUSES RATHER THAN REPAIRS. If the pool is already taken, if the site it was told
    to use does not exist, if there is already an application at that virtual path, or if
    the port is already bound, the script stops and says so instead of configuring over
    what it found. A script that quietly corrects what it meets is how the application next
    door gets broken by somebody who was trying to be helpful.

    IT HAS TWO MODES, AND THEY ARE PARAMETER SETS AND NOT A SWITCH. Giving -VirtualPath
    creates an application inside a site that already exists, which is the default and what
    this installation uses, because the port is already served by the site next door.
    Giving -Port instead creates a site of its own. PowerShell refuses both together.

    EVERY PARAMETER HAS A DEFAULT, and they are the SERVIDOR-APP installation's, so running
    it with no arguments provisions that installation. They are not universal values. The
    script prints every value before it changes anything, saying of each one whether it came
    from the default or from the command line.

    Run it in a PowerShell console as an administrator, on the server.

    Code and parameters are in English, like the rest of this repository. What the script
    prints is in Portuguese, because it is read by the person running it.

.PARAMETER PoolName
    Name of the application pool to create. It must not exist yet, in either mode. The pool
    is always its own: sharing one between applications is not supported with the in-process
    hosting model, which is what the published web.config declares.

.PARAMETER SiteName
    In the default mode, the site that already exists and is to receive the application; it
    is read, it receives an application, and nothing else about it changes. With -Port, the
    site to create, which must not exist yet.

.PARAMETER VirtualPath
    Virtual path under the existing site, such as /IdeaBoard. Giving it chooses the
    application mode. There must be no application at that path already. It is a virtual
    path and not a path on disk, and the script refuses one that looks like a disk path.

.PARAMETER Port
    HTTP port for a site of its own. Giving it chooses the site mode, and it cannot be given
    together with -VirtualPath. It must not be bound by another site. This installation does
    not use it: the application goes inside a site that already serves on port 80 and
    inherits its bindings.

.PARAMETER PhysicalPath
    Folder the published application will live in, for example C:\inetpub\IdeaBoard.

.PARAMETER DataRoot
    Root of the folders that survive a publication. The default is the path Docs/06 fixes,
    and the script prints it so that it is never silently assumed.

.PARAMETER NoPause
    Skips the pause at the end. The pause is there because the console closes with the
    script when it is started from the shell, taking the report with it. Pass this when
    another script runs this one.

.EXAMPLE
    .\provisionar.ps1

    This installation. Every value comes from the defaults: the application /IdeaBoard
    inside the Default Web Site, in its own pool IdeaBoardPool, serving C:\inetpub\IdeaBoard.

.EXAMPLE
    .\provisionar.ps1 -SiteName "Default Web Site" -VirtualPath /Ideias -PoolName IdeiasPool

    An application inside an existing site, with values of its own.

.EXAMPLE
    .\provisionar.ps1 -PoolName IdeaBoardPool -SiteName IdeaBoard -Port 8080 -PhysicalPath C:\inetpub\IdeaBoard

    A site of its own, on a port of its own. Another installation, on a server where
    port 8080 is free.
#>

<#
    EVERY DEFAULT BELOW IS THE SERVIDOR-APP INSTALLATION, measured on that machine and not
    guessed. They are written here so that the installation this repository is being
    deployed for can be provisioned by running the script with no arguments at all.

    THEY ARE NOT UNIVERSAL VALUES. An installation on another server passes its own on the
    command line, and every one of them can be overridden that way. The script prints each
    value before it does anything, saying whether it came from here or from the caller, so
    that whoever runs it without arguments can stop at the first wrong line.

    Nothing is mandatory any more, which is deliberate and which removes the net that
    stopped an accidental run. What remains between an accidental run and a changed machine
    is the guards: elevation, the site existing, the pool not existing, and no application
    already at that virtual path. They all refuse before anything is created.
#>
[CmdletBinding(DefaultParameterSetName = "Application")]
param(
    [string] $PoolName = "IdeaBoardPool",

    # The site that already exists and is to receive the application, in Application, or
    # the site to create, in NewSite.
    [string] $SiteName = "Default Web Site",

    # Giving a port is what picks the NewSite mode, where a site of its own is created. This
    # deployment does not use it: the application goes inside a site that already serves on
    # port 80, and inherits its bindings. It stays because the script is the repository's
    # and the next installation may well need a site of its own, and because the two modes
    # are parameter sets, so PowerShell itself refuses Port and VirtualPath together.
    [Parameter(ParameterSetName = "NewSite")]
    [ValidateRange(1, 65535)]
    [int] $Port,

    # The virtual path under the existing site. This is the default mode.
    [Parameter(ParameterSetName = "Application")]
    [string] $VirtualPath = "/IdeaBoard",

    [string] $PhysicalPath = "C:\inetpub\IdeaBoard",

    [string] $DataRoot = "C:\IdeaBoard",

    # Skips the pause at the end, for running this from another script.
    [switch] $NoPause
)

$ErrorActionPreference = "Stop"

# Captured here, at script scope, because inside a function $PSBoundParameters means that
# function's own arguments. It is what lets each printed value say where it came from.
$givenOnCommandLine = @($PSBoundParameters.Keys)

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


function Write-Setting
{
    param(
        [string] $Name,
        [string] $Label,
        [string] $Value)

    $origin = "(omissao desta instalacao)"

    if ($script:givenOnCommandLine -contains $Name)
    {
        $origin = "(dado na linha de comando)"
    }

    Write-Host ("  {0,-11}{1,-28}{2}" -f $Label, $Value, $origin)
}


function Stop-WithReason
{
    param([string] $Text)

    Write-Host ""
    Write-Host "PARADO. $Text" -ForegroundColor Red
    Write-Host ""
    Write-Host "Nada foi alterado a partir deste ponto. Nenhum pool ou site existente foi tocado." -ForegroundColor Red
    Write-Host ""

    Wait-ForKey

    exit 1
}

#endregion


#region Mode

<#
    Everything below works on one location inside the IIS configuration, and the only thing
    the two modes change is which location that is. A site is addressed by its name, an
    application by the site's name, a slash, and its own path. Deriving it once here is what
    keeps the pool, the permissions, the Event Log source and the authentication identical
    between the two instead of copied.
#>

$isApplication = ($PSCmdlet.ParameterSetName -eq "Application")

$applicationName = ""
$targetLocation = $SiteName
$targetDescription = "site '$SiteName'"

if ($isApplication)
{
    $applicationName = $VirtualPath.Trim("/\ ")

    if ($applicationName.Length -eq 0)
    {
        Stop-WithReason "O caminho virtual esta vazio. Para servir na raiz do site usa-se o modo de site proprio, e nao uma aplicacao."
    }

    <#
        A virtual path is not a path on disk, and this catches the one way it arrives looking
        like one. Run from a Git Bash window, the shell rewrites an argument that begins with
        a slash into a Windows path before the script ever sees it, so -VirtualPath /IdeaBoard
        arrives as something like C:/Program Files/Git/IdeaBoard. Observed, not imagined.

        Without this the application would be created, or refused, under a name nobody typed,
        and the message would be about something else entirely.
    #>
    if ($applicationName.Contains(":") -or $applicationName.Contains("\"))
    {
        Stop-WithReason "O caminho virtual '$applicationName' parece um caminho de disco e nao um caminho virtual. Se isto foi corrido a partir de uma janela Git Bash, foi a shell que reescreveu o argumento antes de o script o ver. Correr a partir do PowerShell, ou passar o valor sem a barra inicial, como -VirtualPath IdeaBoard."
    }

    $targetLocation = $SiteName + "/" + $applicationName
    $targetDescription = "aplicacao '/$applicationName' dentro do site '$SiteName'"
}

#endregion


#region Preconditions

Write-Host ""
Write-Host "Provisionamento do IdeaBoard no IIS." -ForegroundColor White
Write-Host ""
Write-Host "  Estes sao os valores que vao ser usados, e nada foi alterado ainda." -ForegroundColor White
Write-Host ""

Write-Setting "PoolName" "Pool:" $PoolName

if ($isApplication)
{
    Write-Host "  Modo:      aplicacao dentro de um site que ja existe"
    Write-Setting "SiteName" "Site pai:" $SiteName
    Write-Setting "VirtualPath" "Caminho:" ("/" + $applicationName)
}
else
{
    Write-Host "  Modo:      site proprio, com porta propria"
    Write-Setting "SiteName" "Site:" $SiteName
    Write-Setting "Port" "Porta:" $Port
}

Write-Setting "PhysicalPath" "Aplicacao:" $PhysicalPath
Write-Setting "DataRoot" "Dados:" $DataRoot

Write-Host ""
Write-Host "  As omissoes sao as da instalacao do SERVIDOR-APP, medidas nessa maquina e nao" -ForegroundColor DarkGray
Write-Host "  supostas. Nao sao valores universais: noutro servidor passam-se os de la na" -ForegroundColor DarkGray
Write-Host "  linha de comando, e cada linha acima diz de onde veio o valor que vai usar." -ForegroundColor DarkGray
Write-Host "  O caminho dos dados e o que o Docs/06 fixa." -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Se alguma linha acima estiver errada, parar aqui com Ctrl+C." -ForegroundColor White

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

if ($isApplication)
{
    # The site has to be there, and saying which ones are is the whole point of the message.
    # A virtual path created under the wrong site is among the hardest things to see
    # afterwards: it answers 404 from a place nobody is looking at.
    if ($null -eq $existingSite)
    {
        $known = (Get-Website | ForEach-Object { $_.Name }) -join ", "

        if ($known.Length -eq 0)
        {
            $known = "nenhum"
        }

        Stop-WithReason "Nao existe nenhum site chamado '$SiteName'. Os que existem nesta maquina sao: $known. Uma aplicacao criada no site errado responde 404 a partir de um sitio onde ninguem a procura."
    }

    Write-Done "O site '$SiteName' existe e vai receber a aplicacao"

    <#
        Nesting the application's folder inside the parent site's folder is legal in IIS and
        is the one arrangement that can later cost the neighbour its files. The mirrored copy
        in publicar.ps1 deletes whatever is in the destination and not in the artefact, so a
        destination typed one level too high would be the neighbour's own folder.

        It is a warning and not a refusal, because the arrangement is legitimate and the
        decision is the operator's. publicar.ps1 refuses a destination holding files that are
        not this application's, so the danger is already caught on the other side. This says
        it here, where the folder is still being chosen.
    #>
    $parentRoot = [System.IO.Path]::GetFullPath($existingSite.physicalPath)
    $applicationRoot = [System.IO.Path]::GetFullPath($PhysicalPath)

    if ($applicationRoot.StartsWith($parentRoot, [System.StringComparison]::OrdinalIgnoreCase))
    {
        Write-Host "   AVISO: a pasta da aplicacao fica DENTRO da pasta do site vizinho." -ForegroundColor Yellow
        Write-Host ("     Vizinho:   {0}" -f $parentRoot) -ForegroundColor Yellow
        Write-Host ("     Aplicacao: {0}" -f $applicationRoot) -ForegroundColor Yellow
        Write-Host "     E legitimo, mas a copia do publicar.ps1 apaga o que estiver no destino" -ForegroundColor Yellow
        Write-Host "     e nao estiver no artefacto. Um destino escrito um nivel acima seria a" -ForegroundColor Yellow
        Write-Host "     pasta do vizinho. Conferir o -DestinationPath com cuidado." -ForegroundColor Yellow
    }

    foreach ($application in (Get-WebApplication -Site $SiteName))
    {
        if ($application.path -eq ("/" + $applicationName))
        {
            Stop-WithReason "Ja existe uma aplicacao em '/$applicationName' dentro do site '$SiteName', a servir '$($application.PhysicalPath)' pelo pool '$($application.applicationPool)'. Este script nao reconfigura o que encontra. Apagar a existente a mao depois de confirmar que nao e de outra aplicacao, ou escolher outro caminho."
        }
    }

    Write-Done "Nao ha aplicacao em '/$applicationName' dentro de '$SiteName'"
}
else
{
    if ($null -ne $existingSite)
    {
        Stop-WithReason "Ja existe um site chamado '$SiteName'. Mesma razao. Se a intencao era servir dentro dele, usar -VirtualPath em vez de -Port."
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
                    Stop-WithReason "A porta $Port ja esta ligada ao site '$($site.Name)'. Dois sites na mesma porta nao arrancam os dois, e o que ja la esta serve pessoas. Se a intencao era servir dentro desse site, usar -VirtualPath em vez de -Port."
                }
            }
        }
    }

    Write-Done "A porta $Port esta livre"
}

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

<#
    Sixty four bit, set explicitly rather than left to the default.

    THIS IS AN OBSERVATION ABOUT THIS SERVER AND NOT A GENERAL FACT ABOUT IIS. The
    AplicacaoVizinhaB installation guide, in RepositorioVizinhoB, records it under the first
    real install of 2026-09-07 and with the words "confirmed the hard way": on this server
    New-WebAppPool creates the pool with 32 bit applications ENABLED. Read there, not
    repeated from somebody.

    WHAT IT WOULD DO TO US IS NOT WHAT IT DID TO THEM, and the difference is why this
    comment is longer than the line it explains. They publish self contained win-x64, which
    a 32 bit pool cannot load at all, so they got an HTTP Error 500.32 and knew at once.

    We publish framework dependent and in process, with processPath="dotnet" and
    hostingModel="inprocess". The Hosting Bundle installs the x86 runtime as well, so in a
    32 bit pool this would most likely start and serve, quietly, as a 32 bit process. Not a
    failure anybody would see, which is worse to diagnose than their error page: an
    application that works until the day something in it depends on being 64 bit.

    So the honest claim is not that it breaks. It is that it diverges in silence from what
    we think we deployed.
#>
Set-ItemProperty -Path ("IIS:\AppPools\" + $PoolName) -Name "enable32BitAppOnWin64" -Value $false
Write-Done "Aplicacoes de 32 bits: desligadas"

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


#region Site or application

if ($isApplication)
{
    Write-Step "A ler o web.config do site pai, sem lhe tocar"

    <#
        An application inherits its parent site's web.config. What is in the neighbour's is
        not known here, and nothing is assumed about it: this reads it and reports it, and
        changes nothing, because the usual cure for an unwanted inheritance is an edit to
        the PARENT file, which is the one file this script must never touch.

        If a section does turn out to need isolating, this is where that is decided, and it
        is decided by a person with the report below in front of them.
    #>

    $parentConfig = Join-Path $existingSite.physicalPath "web.config"

    if (Test-Path -LiteralPath $parentConfig)
    {
        Write-Host "   O site pai tem web.config proprio, em $parentConfig" -ForegroundColor Yellow

        try
        {
            $parentXml = [xml](Get-Content -Raw -LiteralPath $parentConfig)

            foreach ($node in $parentXml.configuration.ChildNodes)
            {
                if ($node.NodeType -eq "Element")
                {
                    $inner = ($node.ChildNodes | Where-Object { $_.NodeType -eq "Element" } | ForEach-Object { $_.Name }) -join ", "

                    Write-Host ("     {0}: {1}" -f $node.Name, $inner) -ForegroundColor Yellow
                }
            }
        }
        catch
        {
            Write-Host "     Nao foi possivel ler o ficheiro como XML. Fica por responder, e isso nao e o mesmo que estar vazio." -ForegroundColor Magenta
        }

        Write-Host "   O que a aplicacao herdar daqui vale ANTES do web.config dela." -ForegroundColor Yellow
        Write-Host "   O sintoma: a aplicacao responde 500.19 a falar de uma seccao duplicada," -ForegroundColor Yellow
        Write-Host "   ou arranca e comporta-se de forma que o web.config dela nao explica." -ForegroundColor Yellow
        Write-Host "   A cura habitual mexe no ficheiro do VIZINHO, e este script nao lhe toca." -ForegroundColor Yellow
    }
    else
    {
        Write-Done "O site pai nao tem web.config proprio, portanto nada e herdado dele"
    }

    Write-Step "A criar a aplicacao '/$applicationName' dentro do site '$SiteName'"

    New-WebApplication `
        -Site $SiteName `
        -Name $applicationName `
        -PhysicalPath $PhysicalPath `
        -ApplicationPool $PoolName | Out-Null

    Write-Done "Aplicacao criada sobre $PhysicalPath, no pool '$PoolName'"
    Write-Done "O site '$SiteName', o pool dele e as ligacoes dele ficaram como estavam"
}
else
{
    Write-Step "A criar o site '$SiteName'"

    New-Website -Name $SiteName -Port $Port -PhysicalPath $PhysicalPath -ApplicationPool $PoolName | Out-Null

    Write-Done "Site criado na porta $Port, sobre $PhysicalPath"
}

Write-Step "A destrancar a seccao de autenticacao, SO para $targetDescription"

Write-Host "   O web.config que a publicacao produz traz a seccao de autenticacao dentro." -ForegroundColor DarkGray
Write-Host "   O IIS tranca essa seccao ao nivel do servidor por omissao, e um site cujo" -ForegroundColor DarkGray
Write-Host "   web.config a traga responde 500.19 em vez de arrancar." -ForegroundColor DarkGray
Write-Host "   Isto e feito com -Location '$targetLocation', portanto escreve uma entrada so" -ForegroundColor DarkGray
Write-Host "   para este alvo e NAO altera o que vale para os outros." -ForegroundColor DarkGray

foreach ($section in @("windowsAuthentication", "anonymousAuthentication"))
{
    Set-WebConfiguration `
        -Filter ("/system.webServer/security/authentication/" + $section) `
        -PSPath "IIS:\" `
        -Location $targetLocation `
        -Metadata "overrideMode" `
        -Value "Allow"

    Write-Done ($section + " destrancada para " + $targetDescription)
}


Write-Step "A reler o destrancamento, em vez de supor que a escrita pegou"

<#
    Writing this and never reading it back was the gap. Both neighbouring applications on
    this server close it, and their guides require OverrideModeEffective to say Allow
    before anything else is done.

    THE TRAP IS THAT THIS SERVER CANNOT TELL US WHETHER OUR CALL DID ANYTHING. The global
    unlock was already run once here, by the RepositorioVizinhoA team, with appcmd unlock config.
    So the site starts whether our write worked or not, and a step that does nothing passes
    unnoticed on the one machine where it is not needed. The day it is noticed is the day
    somebody installs this on a clean server, and by then nobody remembers this block.

    The two values together are what separate the cases, which is why both are printed:

      OverrideModeEffective is what actually applies to this target.
      OverrideMode is what OUR entry says, at our own location.

    Allow and Allow means our own entry is doing the work. Allow with anything else means
    it is inherited from the global unlock and OUR call changed nothing, which is a warning
    and not a failure here, and would be a failure on a clean machine.

    The RepositorioVizinhoA guide records, dated 2026-09-07, that Set-WebConfiguration against
    "IIS:\" with no -Location silently fails to unlock, and that the target it needs is
    MACHINE/WEBROOT/APPHOST. That measurement is about a DIFFERENT call from ours: theirs
    has no -Location and ours does, and writing to a location entry is not the same as
    unlocking at the root. It may well be right as it is. This read is what will say so,
    and it is the reason not to change the call on a hunch.
#>

foreach ($section in @("windowsAuthentication", "anonymousAuthentication"))
{
    try
    {
        $unlocked = Get-WebConfiguration `
            -Filter ("/system.webServer/security/authentication/" + $section) `
            -PSPath "IIS:\" `
            -Location $targetLocation

        Write-Host ("   {0}: OverrideMode {1}, OverrideModeEffective {2}" -f `
            $section, $unlocked.OverrideMode, $unlocked.OverrideModeEffective)

        if ($unlocked.OverrideModeEffective -eq "Deny")
        {
            Stop-WithReason "A seccao $section continua trancada para '$targetLocation'. Um site cujo web.config a traga responde 500.19 em vez de arrancar, portanto continuar seria provisionar algo que nao arranca. O guia do RepositorioVizinhoA traz duas formas confirmadas de destrancar: 'appcmd unlock config /section:system.webServer/security/authentication/$section', ou Set-WebConfiguration com -PSPath MACHINE/WEBROOT/APPHOST. Correr uma delas e repetir."
        }

        if ($unlocked.OverrideMode -ne "Allow")
        {
            Write-Host "   AVISO: esta destrancada, mas NAO por causa desta escrita." -ForegroundColor Yellow
            Write-Host "   O OverrideMode da nossa entrada nao diz Allow, portanto o Allow efectivo vem" -ForegroundColor Yellow
            Write-Host "   de outro sitio, provavelmente do appcmd unlock global que outra equipa correu" -ForegroundColor Yellow
            Write-Host "   nesta maquina uma vez. Aqui isso nao faz mal nenhum e o site arranca." -ForegroundColor Yellow
            Write-Host "   NUM SERVIDOR LIMPO FARIA, porque este passo nao estaria a destrancar nada." -ForegroundColor Yellow
        }
    }
    catch
    {
        # Read failure is not evidence that the write failed, and stopping here would leave
        # a machine with the pool, the folders and the application already created. So this
        # says loudly that it could not answer, and goes on.
        Write-Host ("   NAO FOI POSSIVEL RELER o destrancamento de " + $section + ".") -ForegroundColor Magenta
        Write-Host "   Isso nao e o mesmo que estar trancada, e tambem nao e o mesmo que estar bem." -ForegroundColor Magenta
        Write-Host "   O sintoma, se estiver trancada: o site responde 500.19 a falar de uma seccao" -ForegroundColor Magenta
        Write-Host "   que nao pode ser usada neste caminho." -ForegroundColor Magenta
    }
}


Write-Step "A configurar a autenticacao de $targetDescription"

<#
    Set at the application's own location in Application mode, which is also what settles
    the one piece of inheritance that is certain: whatever the parent site says about
    authentication, what is written here is what applies to this application. The parent's
    own settings are left exactly as they were, which is what keeps the neighbour working.
#>

# The two go together. With anonymous on, IIS serves the request without challenging
# anybody, the application receives requests with no identity, and the authorisation policy
# refuses everybody instead of authenticating them.
Set-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/windowsAuthentication" `
    -Name "enabled" `
    -Value $true `
    -PSPath "IIS:\" `
    -Location $targetLocation

Write-Done "Autenticacao Windows: ativada"

Set-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
    -Name "enabled" `
    -Value $false `
    -PSPath "IIS:\" `
    -Location $targetLocation

Write-Done "Autenticacao anonima: desligada"

#endregion


#region Confirmation

Write-Step "A reler o que ficou, em vez de confiar no que foi escrito"

$pool = Get-Item -Path ("IIS:\AppPools\" + $PoolName)

Write-Host ("   Pool '{0}': CLR '{1}', maxProcesses {2}, startMode {3}" -f `
    $pool.Name, $pool.managedRuntimeVersion, $pool.processModel.maxProcesses, $pool.startMode)

<#
    The bitness is read back through Get-ItemProperty, which is the form the AplicacaoVizinhaB
    guide uses on this server, and the refusal is written to fail SAFE on purpose.

    If that read ever returns something other than a plain true or false, the comparison is
    simply false and nothing is refused. The alternative shape, treating the returned object
    as a boolean, is truthy for ANY object, so a provider that wrapped the value would make
    this refuse a pool that is perfectly correct. Blocking a good deployment on an uncertain
    read is worse than not checking, because the setting above has already been applied and
    the line below prints what was found either way.
#>
$bitness = Get-ItemProperty -Path ("IIS:\AppPools\" + $PoolName) -Name "enable32BitAppOnWin64"

Write-Host ("   Pool '{0}': 32 bits {1}" -f $PoolName, $bitness.Value)

if ($bitness.Value -eq $true)
{
    Stop-WithReason "O pool ficou com as aplicacoes de 32 bits ligadas. Nesse estado a aplicacao provavelmente arranca na mesma, como processo de 32 bits, sem ninguem reparar, que e pior do que falhar."
}

$createdSite = Get-Website | Where-Object { $_.Name -eq $SiteName }

$bindings = ($createdSite.bindings.Collection | ForEach-Object { $_.bindingInformation }) -join ", "

Write-Host ("   Site '{0}': estado {1}, pool {2}, bindings {3}" -f `
    $createdSite.Name, $createdSite.State, $createdSite.applicationPool, $bindings)

if ($isApplication)
{
    $createdApplication = Get-WebApplication -Site $SiteName | Where-Object { $_.path -eq ("/" + $applicationName) }

    if ($null -eq $createdApplication)
    {
        Stop-WithReason "A aplicacao '/$applicationName' nao aparece no site '$SiteName' depois de criada. Verificar a mao antes de continuar."
    }

    Write-Host ("   Aplicacao '{0}': pool {1}, caminho {2}" -f `
        $createdApplication.path, $createdApplication.applicationPool, $createdApplication.PhysicalPath)

    if ($createdApplication.applicationPool -ne $PoolName)
    {
        Stop-WithReason "A aplicacao ficou no pool '$($createdApplication.applicationPool)' e nao em '$PoolName'. Sem pool proprio nao ha o limite de um processo de trabalho nem a reciclagem desligada."
    }
}

$windowsAuthEnabled = (Get-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/windowsAuthentication" `
    -Name "enabled" `
    -PSPath "IIS:\" `
    -Location $targetLocation).Value

$anonymousEnabled = (Get-WebConfigurationProperty `
    -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
    -Name "enabled" `
    -PSPath "IIS:\" `
    -Location $targetLocation).Value

Write-Host ("   Autenticacao: Windows {0}, anonima {1}" -f $windowsAuthEnabled, $anonymousEnabled)

if ($windowsAuthEnabled -ne $true -or $anonymousEnabled -ne $false)
{
    Stop-WithReason "A autenticacao nao ficou como devia. Verificar a mao antes de continuar."
}

#endregion


#region What comes next

Write-Host ""
if ($isApplication)
{
    Write-Host "Provisionamento concluido. A aplicacao existe e a pasta dela ainda esta vazia." -ForegroundColor White
}
else
{
    Write-Host "Provisionamento concluido. O site existe e ainda nao tem aplicacao nenhuma." -ForegroundColor White
}

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
if ($isApplication)
{
    Write-Host ("Quando estiver publicada, responde em http://<maquina>/{0}" -f $applicationName) -ForegroundColor White
    Write-Host ("Nesta instalacao, http://SERVIDOR-APP/{0}" -f $applicationName) -ForegroundColor White
    Write-Host ""
}

Write-Host "Nenhum pool ou site alheio foi tocado por este script." -ForegroundColor White
Write-Host ""

#endregion


# The last line of the script, on purpose. Everything above has already decided what this
# run means, and this only holds the window open so it can be read.
Wait-ForKey
