# PowerShell 빌드 스크립트 - 한글 지원
Write-Host "================================" -ForegroundColor Cyan
Write-Host "에이전트 비서 빌드 스크립트" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/4] 프로젝트 복원 중..." -ForegroundColor Green
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "오류: 프로젝트 복원 실패" -ForegroundColor Red
    pause
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "[2/4] 디버그 빌드 중..." -ForegroundColor Green
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "오류: 빌드 실패" -ForegroundColor Red
    pause
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "[3/4] 릴리즈 빌드 중..." -ForegroundColor Green
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "오류: 릴리즈 빌드 실패" -ForegroundColor Red
    pause
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "[4/4] 완료!" -ForegroundColor Green
Write-Host ""
Write-Host "빌드가 성공적으로 완료되었습니다!" -ForegroundColor Cyan
Write-Host "실행 파일 위치: bin\Release\net7.0-windows\" -ForegroundColor Yellow
Write-Host ""
pause

