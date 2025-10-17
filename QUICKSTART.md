# 빠른 시작 가이드

## 1단계: 개발 환경 설치

### .NET SDK 설치
1. [.NET 7.0 SDK 다운로드](https://dotnet.microsoft.com/download/dotnet/7.0)
2. 설치 프로그램 실행
3. 설치 완료 후 CMD에서 확인:
   ```cmd
   dotnet --version
   ```

## 2단계: 프로젝트 실행

### 방법 1: PowerShell 스크립트 사용 (추천)
PowerShell에서:
```powershell
.\run.ps1
```
또는 파일 탐색기에서 `run.ps1` 우클릭 → "PowerShell에서 실행"

### 방법 2: 직접 실행
```powershell
dotnet restore
dotnet run
```

## 3단계: 프로그램 사용

### 기본 조작
- **이동**: 캐릭터를 마우스 좌클릭으로 드래그
- **메뉴**: 우클릭으로 컨텍스트 메뉴
- **설정**: 우측 상단 ⚙️ 버튼
- **종료**: 우측 상단 ❌ 버튼

### 주요 기능
1. **인사하기**: 캐릭터가 인사 메시지 표시
2. **도움말**: 사용법 안내
3. **말풍선**: 자동으로 메시지 표시
4. **설정**:
   - 색상 변경
   - 투명도 조절
   - 메시지 주기 설정
   - 자동 시작 설정

## 배포 버전 만들기

### 배포 빌드
PowerShell에서:
```powershell
.\publish.ps1
```

이렇게 하면 `publish` 폴더에 실행 파일이 생성됩니다.

### 다른 PC에서 실행
1. `publish` 폴더 전체 복사
2. 대상 PC에 [.NET 7.0 Runtime](https://dotnet.microsoft.com/download/dotnet/7.0/runtime) 설치
3. `AgentAssistant.exe` 실행

## 문제 해결

### "dotnet을 인식할 수 없습니다"
- .NET SDK가 설치되지 않았습니다
- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) 설치 후 재시도

### 프로그램이 보이지 않음
- 화면 밖으로 벗어났을 수 있습니다
- 프로그램을 재시작하면 기본 위치로 돌아갑니다

### 캐릭터가 깨져 보임
- GPU 드라이버를 최신 버전으로 업데이트
- 윈도우 업데이트 확인

## 추가 정보

자세한 내용은 `README.md`를 참고하세요.

---
**즐거운 코딩 되세요! 😊**


