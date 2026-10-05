<#
.SYNOPSIS
    Persists the local PostgreSQL password for ClubStack into the user-scope environment.

.DESCRIPTION
    ClubStack resolves the `${CLUBSTACK_DB_PASSWORD}` token in the committed connection
    string from the process environment. This script stores that value once in
    HKCU\Environment, so every new shell, `dotnet` invocation and application start picks
    it up automatically and the user never has to supply it again.

    The value is never printed, logged, or written to the repository.

.PARAMETER Password
    The password to store. Omit to be prompted without echo.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Setup-LocalSecrets.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Setup-LocalSecrets.ps1 -Password '<password>'
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Password
)

$ErrorActionPreference = 'Stop'
$variableName = 'CLUBSTACK_DB_PASSWORD'

if ([string]::IsNullOrWhiteSpace($Password)) {
    $secure = Read-Host -Prompt "$variableName" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "No password supplied; $variableName was not changed."
}

[Environment]::SetEnvironmentVariable($variableName, $Password, 'User')

$stored = [Environment]::GetEnvironmentVariable($variableName, 'User')

# Report presence and length only. Never the value.
Write-Host "$variableName saved to the user-scope environment (HKCU\Environment)."
Write-Host ("Verified: present={0}, length={1}" -f (-not [string]::IsNullOrEmpty($stored)), $stored.Length)
Write-Host "Open a new terminal before starting the application, so it inherits the value."
Write-Host "See docs\local-setup.md for the requirements on the PostgreSQL role (needs CREATEDB)."
