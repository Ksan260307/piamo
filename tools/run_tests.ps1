# Piamo: すべてのテストを実行する
#   powershell -ExecutionPolicy Bypass -File tools/run_tests.ps1 [-Only EditMode|PlayMode|Tools|Blender]
#  - Tools   : Python ツールの単体テスト（音・スタンプ・アイコン・フォント・ビルド検査）
#  - Blender : 鍵盤キット（build_piano_kit.py）の単体テスト
#  - EditMode: Unity の単体テスト（鍵盤のならび・カメラ・まねっこ・スタンプのきまり・保存・設定・プロジェクト設定…）
#  - PlayMode: Unity の総合テスト（メニュー・もどる・ピアノ・グリッサンド・せってい・まねっこ・スタンプ・画面の大きさ）
param(
    [string]$Only = "",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe",
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$out = Join-Path $root "build-tmp"
New-Item -ItemType Directory -Force $out | Out-Null
$failed = @()

function Want($name) { return ($Only -eq "") -or ($Only -eq $name) }

if (Want "Tools") {
    Write-Host "== Python tools"
    python -m unittest discover -s (Join-Path $root "tools/tests")
    if ($LASTEXITCODE -ne 0) { $failed += "Tools" }
}

if (Want "Blender") {
    Write-Host "== Blender kit"
    & $Blender -b --factory-startup --python-exit-code 1 --python (Join-Path $root "blender/tests/test_piano_kit.py")
    if ($LASTEXITCODE -ne 0) { $failed += "Blender" }
}

foreach ($mode in @("EditMode", "PlayMode")) {
    if (-not (Want $mode)) { continue }
    Write-Host "== Unity $mode"
    $xml = Join-Path $out "results-$mode.xml"
    $log = Join-Path $out "test-$mode.log"
    if (Test-Path $xml) { Remove-Item $xml }
    $p = Start-Process -FilePath $Unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        "-batchmode", "-projectPath", (Join-Path $root "unity"), "-runTests", "-testPlatform", $mode,
        "-testResults", $xml, "-logFile", $log)
    if (-not (Test-Path $xml)) { $failed += $mode; Write-Host "結果ファイルがありません。ログ: $log"; continue }
    # 結果は UTF-8（日本語のメッセージをふくむ）。PowerShell 5.1 の既定の文字コードで読むとこわれるので明示する
    [xml]$r = Get-Content -Raw -Encoding UTF8 $xml
    $run = $r."test-run"
    Write-Host ("  {0}: total={1} passed={2} failed={3}" -f $mode, $run.total, $run.passed, $run.failed)
    foreach ($tc in $r.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host ("  FAIL {0}`n       {1}" -f $tc.fullname, $tc.failure.message.InnerText.Trim())
    }
    if ($p.ExitCode -ne 0) { $failed += $mode }
}

if ($failed.Count -gt 0) {
    Write-Host ("失敗: " + ($failed -join ", ")) -ForegroundColor Red
    exit 1
}
Write-Host "すべてのテストに合格しました" -ForegroundColor Green
