param(
    [switch]$ExactGrand,
    [switch]$ExactMasters,
    [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data'
)
# Static compilation + managed NUnit + production-rule authoring searches. Never launches Unity.
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$previousLocation = Get-Location
$previousMonoPath = $env:MONO_PATH
try {
    Set-Location -LiteralPath $project
    $output = Join-Path $project 'Temp/M46Validation/out'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $framework = @(Get-ChildItem "$UnityData/UnityReferenceAssemblies/unity-4.8-api" -Filter *.dll -File) + @(Get-ChildItem "$UnityData/UnityReferenceAssemblies/unity-4.8-api/Facades" -Filter *.dll -File)
    $engine = @(Get-ChildItem "$UnityData/Managed/UnityEngine" -Filter UnityEngine*.dll -File)
    $editor = @(Get-ChildItem "$UnityData/Managed" -Filter UnityEditor*.dll -File) + @(Get-ChildItem "$UnityData/Managed/UnityEngine" -Filter UnityEditor*.dll -File)
    $packages = @('Unity.InputSystem','UnityEngine.UI','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime') | ForEach-Object { Join-Path $project "Library/ScriptAssemblies/$_.dll" }
    $nunit = (Get-ChildItem 'Library/PackageCache' -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1).FullName + '/net40/unity-custom/nunit.framework.dll'
    foreach ($dependency in $packages + @($nunit)) {
        if (-not (Test-Path -LiteralPath $dependency -PathType Leaf)) { throw "Missing imported dependency: $dependency. Let this project compile in the Editor first." }
    }
    function Compile-Assembly($name, $sources, $references, $defines, $target = 'library') {
        $options = @('/nologo',"/target:$target",'/langversion:9','/nostdlib+','/warn:4','/warnaserror+',('/define:' + $defines),('/out:"' + (Join-Path $output $name) + '"'))
        $options += @($references | Sort-Object -Unique | ForEach-Object { '/reference:"' + $_ + '"' })
        $options += @($sources | ForEach-Object { '"' + $_ + '"' })
        $response = Join-Path $output "$name.rsp"
        $options | Set-Content -LiteralPath $response
        & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$response"
        if ($LASTEXITCODE -ne 0) { throw "$name compilation failed" }
        Write-Output "$name compiled"
    }
    $base = @($framework.FullName) + @($engine.FullName) + $packages
    $runtime = Join-Path $output 'ColorSlide.Runtime.dll'
    $editorAssembly = Join-Path $output 'ColorSlide.Editor.dll'
    $tests = Join-Path $output 'ColorSlide.Tests.EditMode.dll'
    Compile-Assembly 'ColorSlide.Runtime.PlayerCheck.dll' @(Get-ChildItem 'Assets/_ColorSlide/Runtime' -Recurse -Filter *.cs -File).FullName $base 'UNITY_ANDROID;UNITY_6000_3_OR_NEWER;ENABLE_INPUT_SYSTEM'
    Compile-Assembly 'ColorSlide.Runtime.dll' @(Get-ChildItem 'Assets/_ColorSlide/Runtime' -Recurse -Filter *.cs -File).FullName $base 'UNITY_EDITOR;UNITY_6000_3_OR_NEWER;ENABLE_INPUT_SYSTEM'
    Compile-Assembly 'ColorSlide.Editor.dll' @(Get-ChildItem 'Assets/_ColorSlide/Editor' -Recurse -Filter *.cs -File).FullName ($base + $editor.FullName + $runtime) 'UNITY_EDITOR;UNITY_6000_3_OR_NEWER;ENABLE_INPUT_SYSTEM'
    $testRefs = $base + $editor.FullName + @($runtime,$editorAssembly,$nunit,(Join-Path $project 'Library/ScriptAssemblies/UnityEngine.TestRunner.dll'),(Join-Path $project 'Library/ScriptAssemblies/UnityEditor.TestRunner.dll'))
    Compile-Assembly 'ColorSlide.Tests.EditMode.dll' @(Get-ChildItem 'Assets/_ColorSlide/Tests/EditMode' -Recurse -Filter *.cs -File).FullName $testRefs 'UNITY_EDITOR;UNITY_INCLUDE_TESTS;UNITY_6000_3_OR_NEWER;ENABLE_INPUT_SYSTEM'
    Compile-Assembly 'M46Verification.exe' @((Join-Path $PSScriptRoot 'M46Verification.cs')) $testRefs 'UNITY_EDITOR' 'exe'
    $env:MONO_PATH = "$output;$UnityData/Managed;$UnityData/Managed/UnityEngine;" + (Join-Path $project 'Library/ScriptAssemblies') + ';' + (Split-Path $nunit)
    & "$UnityData/MonoBleedingEdge/bin/mono.exe" (Join-Path $output 'M46Verification.exe') $tests $output $ExactGrand.IsPresent $ExactMasters.IsPresent
    if ($LASTEXITCODE -ne 0) { throw 'Managed M4.6 validation failed' }
}
finally {
    $env:MONO_PATH = $previousMonoPath
    Set-Location -LiteralPath $previousLocation
}
