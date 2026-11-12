# SPDX-License-Identifier: Apache-2.0
# Copyright 2026 老秦进度管理软件贡献者
param([string]$OutputPath = "$PSScriptRoot/dist", [string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'LaoQin.ProgressManager.csproj'
& $DotnetPath publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -o $OutputPath
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Get-Item -LiteralPath (Join-Path $OutputPath 'LaoQinProgressManager.exe') | Select-Object FullName,Length
