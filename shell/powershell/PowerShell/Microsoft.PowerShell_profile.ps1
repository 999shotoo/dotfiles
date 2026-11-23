Invoke-Expression (&starship init powershell)

$llvmMinGWBin = "__HOME__\AppData\Local\Microsoft\WinGet\Packages\MartinStorsjo.LLVM-MinGW.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\llvm-mingw-20260908-ucrt-x86_64\bin"
if (Test-Path $llvmMinGWBin) {
    $env:Path = "$llvmMinGWBin;$env:Path"
}