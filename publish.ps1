# PowerShell 배포 스크립트 - 한글 지원
Write-Host "================================" -ForegroundColor Cyan
Write-Host "에이전트 비서 배포 빌드" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$OUTPUT_DIR = "publish"

Write-Host "[1/3] 이전 배포 파일 정리 중..." -ForegroundColor Green
if (Test-Path $OUTPUT_DIR) {
    Remove-Item -Path $OUTPUT_DIR -Recurse -Force
}

Write-Host ""
Write-Host "[2/3] 배포 빌드 중..." -ForegroundColor Green
Write-Host "대상: Windows x64" -ForegroundColor Yellow
Write-Host "프레임워크: .NET 7.0" -ForegroundColor Yellow
Write-Host ""

dotnet publish -c Release -r win-x64 --self-contained false -o $OUTPUT_DIR

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "오류: 배포 빌드 실패" -ForegroundColor Red
    pause
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "[3/3] 완료!" -ForegroundColor Green
Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "배포 파일이 생성되었습니다!" -ForegroundColor Green
Write-Host "위치: $OUTPUT_DIR\" -ForegroundColor Yellow
Write-Host ""
Write-Host "실행 방법:" -ForegroundColor Cyan
Write-Host "1. $OUTPUT_DIR 폴더를 원하는 위치에 복사" -ForegroundColor White
Write-Host "2. AgentAssistant.exe 실행" -ForegroundColor White
Write-Host ""
Write-Host "참고: .NET 7.0 Runtime이 설치되어 있어야 합니다." -ForegroundColor Yellow
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
pause

