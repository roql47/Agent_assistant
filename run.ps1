# PowerShell 스크립트 - 한글 지원
Write-Host "================================" -ForegroundColor Cyan
Write-Host "에이전트 비서 실행" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "프로그램을 시작합니다..." -ForegroundColor Green
dotnet run

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "오류: 프로그램 실행 실패" -ForegroundColor Red
    Write-Host ".NET 7.0 SDK가 설치되어 있는지 확인하세요." -ForegroundColor Yellow
    pause
    exit $LASTEXITCODE
}

