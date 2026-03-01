[CmdletBinding()]
param(
    [int]$MaxWebKb = 128,
    [int]$MaxMobileKb = 128,
    [string]$WebCssPath = 'src/Chop.Web/wwwroot/app.css',
    [string]$MobileCssPath = 'src/Chop.App.Mobile/wwwroot/app.css'
)

$ErrorActionPreference = 'Stop'

function Test-CssBudget {
    param(
        [string]$Path,
        [int]$MaxKb,
        [string]$Name
    )

    if (-not (Test-Path $Path)) {
        throw "CSS file not found: $Path"
    }

    $bytes = (Get-Item $Path).Length
    $kb = [math]::Round($bytes / 1KB, 2)
    Write-Host ("{0}: {1} KB (budget <= {2} KB)" -f $Name, $kb, $MaxKb)

    if ($kb -gt $MaxKb) {
        throw ("CSS budget exceeded for {0}: {1} KB > {2} KB" -f $Name, $kb, $MaxKb)
    }
}

Test-CssBudget -Path $WebCssPath -MaxKb $MaxWebKb -Name 'Web app.css'
Test-CssBudget -Path $MobileCssPath -MaxKb $MaxMobileKb -Name 'Mobile app.css'

Write-Host 'CSS budget check passed.'
