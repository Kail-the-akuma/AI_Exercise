#Requires -Version 5.1

<#
.SYNOPSIS
    Produces the published artefact, checks what is in it, and copies it to the server.

.DESCRIPTION
    Two halves that can run on different machines, because the artefact is usually built
    where the repository is and copied where the site is. Use -Action to run one half.

    WHAT THE CHECK IS FOR. The build already refuses to produce an artefact that reaches a
    host, through the guard in Directory.Build.targets, and that guard works on project
    references. This checks the folder that actually gets copied, which is a different
    question answered at a different moment: a file can arrive in an output folder without
    anybody referencing it. Two checks at two levels, and the cheap one is the one that
    runs on the day somebody publishes.

    WHAT IT NEVER TOUCHES. Nothing under the data root, which is C:\IdeaBoard by default.
    The database, the key ring and the backups live outside the application folder
    precisely so that they survive this, and the mirror copy below would delete them.

    THE MIRROR COPY IS THE DANGEROUS PART. It deletes whatever is in the destination and
    is not in the artefact, which is what a publication folder needs and what a wrong path
    turns into a disaster. The guard before it refuses a destination that does not look
    like this application's folder.

    Code and parameters are in English, like the rest of this repository. What the script
    prints is in Portuguese, because it is read by the person running it.

.PARAMETER Action
    Publish produces the artefact. Copy sends an existing artefact to the server. All does
    both, which only makes sense when the repository and the site are on the same machine.

.PARAMETER RepositoryRoot
    Root of the repository. Defaults to the folder above this script.

.PARAMETER ArtefactPath
    Folder the publication is written to and read from. It is emptied before publishing.

.PARAMETER DestinationPath
    Folder on the server the application is served from.

.PARAMETER PoolName
    Application pool to stop before copying and start afterwards. If it does not exist yet,
    which is the case on the first publication, the copy goes ahead without it.

.PARAMETER DataRoot
    Root of the folders that must never be touched. Used only as a guard.

.EXAMPLE
    .\publicar.ps1 -Action Publish -ArtefactPath C:\temp\ideaboard-publish

.EXAMPLE
    .\publicar.ps1 -Action Copy -ArtefactPath C:\temp\ideaboard-publish -DestinationPath C:\inetpub\IdeaBoard -PoolName IdeaBoard
#>

[CmdletBinding()]
param(
    [ValidateSet("Publish", "Copy", "All")]
    [string] $Action = "All",

    # Left empty here and worked out in the body, never in this default. Inside a
    # parameter default $PSScriptRoot is EMPTY when the script is started as a new process
    # with powershell.exe -file, and it holds the right path only when the script is called
    # from inside a session that is already open. Measured both ways, with this file.
    #
    # Which matters because -file is exactly how Windows starts a .ps1 from the shell: the
    # registry command behind "Executar com o PowerShell" is
    # "powershell.exe" "-file" " %1 ". So computing it here killed the script inside its own
    # parameter block, before one line of the report, in the very way of starting it that
    # the pause at the end exists to serve.
    [string] $RepositoryRoot = "",

    [Parameter(Mandatory = $true)]
    [string] $ArtefactPath,

    [string] $DestinationPath = "C:\inetpub\IdeaBoard",

    # The pool this application runs in, stopped for the copy and started again after,
    # and never any other. The default is the SERVIDOR-APP installation's, like the
    # destination above it. Another server passes its own.
    [string] $PoolName = "IdeaBoardPool",

    [string] $DataRoot = "C:\IdeaBoard",

    # Skips the pause at the end, for running this from another script.
    [switch] $NoPause
)

$ErrorActionPreference = "Stop"

# Here and not in the parameter default, for the reason written there. In the body
# $PSScriptRoot holds the script's own folder however the script was started.
if ($RepositoryRoot -eq "")
{
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

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


function Stop-WithReason
{
    param([string] $Text)

    Write-Host ""
    Write-Host "PARADO. $Text" -ForegroundColor Red
    Write-Host ""

    Wait-ForKey

    exit 1
}

#endregion


#region Publishing

function Invoke-Publish
{
    Write-Step "A confirmar o repositorio"

    $webProject = Join-Path $RepositoryRoot "src\IdeaBoard.Web\IdeaBoard.Web.csproj"

    if (-not (Test-Path -LiteralPath $webProject))
    {
        Stop-WithReason "Nao encontrei $webProject. Passar -RepositoryRoot com a raiz do repositorio."
    }

    Write-Done $webProject

    Write-Step "A esvaziar a pasta de saida"

    # A publication folder that still holds the previous one hides a file that stopped being
    # produced, and the check below would pass on a leftover.
    if (Test-Path -LiteralPath $ArtefactPath)
    {
        Remove-Item -LiteralPath $ArtefactPath -Recurse -Force

        Write-Done "Limpa $ArtefactPath"
    }

    New-Item -ItemType Directory -Path $ArtefactPath -Force | Out-Null

    Write-Step "A publicar em Release"

    & dotnet.exe publish $webProject -c Release -o $ArtefactPath

    if ($LASTEXITCODE -ne 0)
    {
        Stop-WithReason "O dotnet publish saiu com codigo $LASTEXITCODE."
    }

    Write-Done "Publicacao concluida"
}

#endregion


#region Checking the artefact

function Test-Artefact
{
    Write-Step "A verificar o que esta no artefacto"

    if (-not (Test-Path -LiteralPath $ArtefactPath))
    {
        Stop-WithReason "Nao existe $ArtefactPath. Correr primeiro com -Action Publish."
    }

    $mainAssembly = Join-Path $ArtefactPath "IdeaBoard.Web.dll"

    if (-not (Test-Path -LiteralPath $mainAssembly))
    {
        Stop-WithReason "O artefacto nao tem IdeaBoard.Web.dll. Isto nao e uma publicacao desta aplicacao."
    }

    Write-Done "IdeaBoard.Web.dll presente"

    # The invariant of CLAUDE.md, checked on the folder instead of on the references. A host
    # inside the artefact means the local identity, the person picker and a way into the
    # application without a domain, published to a server where everybody has one.
    $forbidden = Get-ChildItem -LiteralPath $ArtefactPath -Recurse -File |
        Where-Object { $_.Name -like "IdeaBoard.Local*" }

    if ($null -ne $forbidden)
    {
        foreach ($file in $forbidden)
        {
            Write-Host ("   " + $file.FullName) -ForegroundColor Red
        }

        Stop-WithReason "O artefacto leva identidade local. E a invariante do CLAUDE.md, e nao se publica assim."
    }

    Write-Done "Sem identidade local nem seletor de pessoa"

    # A database inside the application folder is deleted by the next publication. It must
    # never travel in the artefact either, or the first copy overwrites the real one.
    $databases = Get-ChildItem -LiteralPath $ArtefactPath -Recurse -File |
        Where-Object { $_.Extension -eq ".db" -or $_.Name -like "*.db-wal" -or $_.Name -like "*.db-shm" }

    if ($null -ne $databases)
    {
        foreach ($file in $databases)
        {
            Write-Host ("   " + $file.FullName) -ForegroundColor Red
        }

        Stop-WithReason "O artefacto leva uma base de dados. A base vive em $DataRoot\data e nunca na pasta da aplicacao."
    }

    Write-Done "Sem base de dados"

    $webConfigPath = Join-Path $ArtefactPath "web.config"

    if (-not (Test-Path -LiteralPath $webConfigPath))
    {
        Stop-WithReason "O artefacto nao tem web.config. Sem ele o site nao arranca e as duas variaveis nao chegam la."
    }

    [xml] $webConfig = Get-Content -LiteralPath $webConfigPath -Raw

    $aspNetCore = $webConfig.SelectSingleNode("//aspNetCore")

    if ($null -eq $aspNetCore)
    {
        Stop-WithReason "O web.config publicado nao tem elemento aspNetCore."
    }

    # The application refuses to start without these two and names them, so an artefact
    # without them is a site that stops with a message about configuration, on the server,
    # at the worst moment to find out.
    $expected = @("ConnectionStrings__IdeaBoard", "DataProtection__KeysPath")

    foreach ($name in $expected)
    {
        $variable = $aspNetCore.SelectSingleNode("environmentVariables/environmentVariable[@name='" + $name + "']")

        if ($null -eq $variable)
        {
            Stop-WithReason "Falta a variavel $name dentro do aspNetCore do web.config publicado."
        }

        if ([string]::IsNullOrWhiteSpace($variable.value))
        {
            Stop-WithReason "A variavel $name esta no web.config mas vazia, o que e o mesmo que nao estar."
        }

        Write-Done ("$name = " + $variable.value)
    }

    $fileCount = (Get-ChildItem -LiteralPath $ArtefactPath -Recurse -File | Measure-Object).Count

    Write-Done "$fileCount ficheiros no artefacto"
}

#endregion


#region Copying

function Test-Destination
{
    # The mirror copy deletes whatever is in the destination and not in the artefact. A path
    # wrong by one character turns that from the right behaviour into a disaster, so the
    # destination has to look like this application's folder before anything is deleted.
    $resolvedDestination = [System.IO.Path]::GetFullPath($DestinationPath)
    $resolvedDataRoot = [System.IO.Path]::GetFullPath($DataRoot)

    if ($resolvedDestination.StartsWith($resolvedDataRoot, [System.StringComparison]::OrdinalIgnoreCase))
    {
        Stop-WithReason "O destino $resolvedDestination esta dentro de $resolvedDataRoot. A base de dados, as chaves e os backups vivem ai e uma copia espelhada apaga os."
    }

    if ($resolvedDestination.Length -le 3)
    {
        Stop-WithReason "O destino $resolvedDestination e a raiz de uma unidade. Nao."
    }

    if (-not (Test-Path -LiteralPath $DestinationPath))
    {
        Write-Done "O destino nao existe ainda, portanto e a primeira copia"
        return
    }

    $existing = Get-ChildItem -LiteralPath $DestinationPath -Force

    if ($null -eq $existing -or @($existing).Count -eq 0)
    {
        Write-Done "O destino esta vazio, portanto e a primeira copia"
        return
    }

    if (Test-Path -LiteralPath (Join-Path $DestinationPath "IdeaBoard.Web.dll"))
    {
        Write-Done "O destino ja tem uma publicacao desta aplicacao"
        return
    }

    Stop-WithReason "O destino $DestinationPath tem ficheiros mas nao tem IdeaBoard.Web.dll. Nao parece a pasta desta aplicacao, e uma copia espelhada apagaria o que la esta. Confirmar o caminho."
}


function Invoke-Copy
{
    Write-Step "A confirmar o destino antes de apagar seja o que for"

    Test-Destination

    $poolWasRunning = $false
    $poolExists = $false

    if ($PoolName -ne "")
    {
        Write-Step "A parar o pool '$PoolName', e so esse"

        try
        {
            Import-Module WebAdministration -ErrorAction Stop
        }
        catch
        {
            Stop-WithReason "Nao foi possivel carregar o WebAdministration, e foi pedido para parar o pool '$PoolName'."
        }

        if (Test-Path -LiteralPath ("IIS:\AppPools\" + $PoolName))
        {
            $poolExists = $true

            $pool = Get-Item -Path ("IIS:\AppPools\" + $PoolName)

            if ($pool.State -eq "Started")
            {
                $poolWasRunning = $true

                # Only this pool. Never iisreset and never the service: there is another
                # application being served from this machine and a global command takes it
                # down too.
                Stop-WebAppPool -Name $PoolName

                $waited = 0

                while ((Get-WebAppPoolState -Name $PoolName).Value -ne "Stopped" -and $waited -lt 30)
                {
                    Start-Sleep -Seconds 1
                    $waited = $waited + 1
                }

                if ((Get-WebAppPoolState -Name $PoolName).Value -ne "Stopped")
                {
                    Stop-WithReason "O pool '$PoolName' nao parou em $waited segundos. Os ficheiros ficam bloqueados enquanto a aplicacao corre."
                }

                Write-Done "Pool parado ao fim de $waited segundos"
            }
            else
            {
                Write-Done ("O pool ja estava " + $pool.State)
            }
        }
        else
        {
            # The first copy happens before the first start, so a missing pool is normal
            # here and not a reason to stop.
            Write-Done "O pool '$PoolName' ainda nao existe, portanto nao ha nada para parar"
        }
    }
    else
    {
        Write-Step "Sem -PoolName, portanto nenhum pool e parado"
        Write-Host "   Se a aplicacao estiver a correr, a copia falha com ficheiros bloqueados." -ForegroundColor Yellow
    }

    Write-Step "A copiar para $DestinationPath"

    if (-not (Test-Path -LiteralPath $DestinationPath))
    {
        New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
    }

    & robocopy.exe $ArtefactPath $DestinationPath /MIR /NFL /NDL /NJH /NP /R:3 /W:5

    # Robocopy says 0 to 7 for work done or nothing to do, and 8 or more for failure.
    $robocopyExit = $LASTEXITCODE

    if ($robocopyExit -ge 8)
    {
        Stop-WithReason "O robocopy saiu com codigo $robocopyExit, que e falha. O pool ficou parado de proposito, para nao arrancar sobre uma copia a meio."
    }

    Write-Done "Copia concluida, codigo do robocopy $robocopyExit"

    if ($poolExists -and $poolWasRunning)
    {
        Write-Step "A arrancar o pool '$PoolName'"

        Start-WebAppPool -Name $PoolName

        Write-Done "Pool arrancado"
    }
    else
    {
        if ($poolExists)
        {
            Write-Step "O pool nao estava a correr antes, por isso fica como estava"
        }
    }
}

#endregion


#region Running

Write-Host ""
Write-Host "Publicacao do IdeaBoard. Accao: $Action" -ForegroundColor White
Write-Host ""
Write-Host "  Artefacto: $ArtefactPath"
Write-Host "  Destino:   $DestinationPath"
Write-Host "  Pool:      $PoolName"
Write-Host "  Intocavel: $DataRoot"

if ($Action -eq "Publish" -or $Action -eq "All")
{
    Invoke-Publish
    Test-Artefact
}

if ($Action -eq "Copy")
{
    Test-Artefact
}

if ($Action -eq "Copy" -or $Action -eq "All")
{
    Invoke-Copy
}

Write-Host ""
Write-Host "Feito. Nada dentro de $DataRoot foi tocado." -ForegroundColor White

if ($Action -eq "Publish")
{
    Write-Host "Falta copiar, com -Action Copy, na maquina do servidor." -ForegroundColor White
}

Write-Host ""

#endregion


# The last line of the script, on purpose. Everything above has already decided what this
# run means, and this only holds the window open so it can be read.
Wait-ForKey
