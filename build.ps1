# SPDX-License-Identifier: Apache-2.0
# Copyright 2026 老秦进度管理软件贡献者
param([string]$OutputPath = "$PSScriptRoot/dist")
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) { $framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319' }
$compiler = Join-Path $framework 'csc.exe'
$referenceNames = @('System.dll','System.Core.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.Runtime.Serialization.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $OutputPath 'LaoQinProgressManager.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
$icon = Join-Path $PSScriptRoot 'app.ico'
if (Test-Path -LiteralPath $icon) { $arguments += '/win32icon:' + $icon }
foreach ($referenceName in $referenceNames) { $arguments += '/reference:' + (Join-Path $framework $referenceName) }
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Get-Item -LiteralPath (Join-Path $OutputPath 'LaoQinProgressManager.exe') | Select-Object FullName,Length
