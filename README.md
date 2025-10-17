# 에이전트 비서 (Agent Assistant)

윈도우 10/11에서 작동하는 프레임리스 데스크톱 에이전트 비서 프로그램입니다. 
마이크로소프트 오피스의 클리피(Clippy)를 현대적으로 재해석한 귀여운 캐릭터가 화면에 떠다니며 도움을 줍니다.

## 주요 기능

- 🎨 **프레임리스 투명 윈도우**: 깔끔한 UI로 화면에 자연스럽게 표시
- 🎭 **애니메이션 캐릭터**: 부드러운 Idle 애니메이션과 인터랙션
- 💬 **말풍선 메시지**: 귀여운 말풍선으로 메시지 표시
- 🖱️ **드래그 이동**: 마우스로 자유롭게 위치 이동 가능
- 📌 **항상 위에 표시**: 다른 창 위에 항상 표시
- 🎯 **컨텍스트 메뉴**: 우클릭으로 다양한 기능 접근

## 시스템 요구사항

- **운영체제**: Windows 10 / Windows 11
- **.NET**: .NET 7.0 Runtime 이상
- **메모리**: 최소 50MB RAM

## 설치 및 실행

### 개발 환경 설정

1. **.NET SDK 설치**
   - [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) 다운로드 및 설치

2. **프로젝트 복원**
   ```powershell
   dotnet restore
   ```

3. **빌드**
   ```powershell
   dotnet build
   ```
   또는 PowerShell 스크립트 사용:
   ```powershell
   .\build.ps1
   ```

4. **실행**
   ```powershell
   dotnet run
   ```
   또는 PowerShell 스크립트 사용:
   ```powershell
   .\run.ps1
   ```

### 릴리즈 빌드 (배포용)

PowerShell 스크립트 사용:
```powershell
.\publish.ps1
```

또는 직접 명령어:
```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

릴리즈 빌드는 `bin\Release\net7.0-windows\win-x64\publish\` 폴더 또는 `publish\` 폴더에 생성됩니다.

## 사용 방법

### 기본 조작

- **이동**: 캐릭터를 좌클릭하여 드래그
- **메뉴**: 우클릭으로 컨텍스트 메뉴 열기
- **닫기**: 우측 상단 ❌ 버튼 또는 컨텍스트 메뉴에서 종료

### 메뉴 기능

- **인사하기**: 캐릭터가 인사 메시지 표시
- **도움말 표시**: 사용법 안내
- **말풍선 숨기기**: 현재 표시된 말풍선 즉시 숨김
- **설정**: 설정 옵션 (개발 예정)
- **종료**: 프로그램 종료

## 프로젝트 구조

```
Agent_assistant/
│
├── Agent_assistant.csproj    # 프로젝트 파일
├── App.xaml                   # 애플리케이션 리소스
├── App.xaml.cs                # 애플리케이션 코드
├── MainWindow.xaml            # 메인 윈도우 UI
├── MainWindow.xaml.cs         # 메인 윈도우 로직
└── README.md                  # 이 파일
```

## 기술 스택

- **WPF (Windows Presentation Foundation)**: UI 프레임워크
- **.NET 7.0**: 런타임 환경
- **XAML**: UI 마크업
- **C#**: 프로그래밍 언어

## 향후 개발 예정 기능

- [ ] 설정 창 구현 (캐릭터 테마, 위치 저장, 자동 시작 등)
- [ ] 더 많은 캐릭터 테마
- [ ] 다양한 애니메이션 추가
- [ ] AI 연동 (ChatGPT, Claude 등)
- [ ] 음성 인식 및 TTS
- [ ] 화면 캡처 및 OCR 기능
- [ ] 클립보드 모니터링
- [ ] 시스템 정보 표시 (CPU, 메모리 사용량 등)
- [ ] 타이머 및 알림 기능
- [ ] 날씨 정보 표시

## 커스터마이징

### 캐릭터 색상 변경

`MainWindow.xaml` 파일에서 캐릭터 색상을 변경할 수 있습니다:

```xml
<Ellipse Fill="#6C5CE7" ... />
```

`#6C5CE7`를 원하는 색상 코드로 변경하세요.

### 메시지 변경

`MainWindow.xaml.cs` 파일의 `greetings` 및 `helpMessages` 배열을 수정하여 메시지를 추가/변경할 수 있습니다.

## 문제 해결

### 프로그램이 실행되지 않는 경우
- .NET 7.0 Runtime이 설치되어 있는지 확인하세요
- `dotnet --version` 명령어로 .NET 버전 확인

### 캐릭터가 보이지 않는 경우
- 화면 밖으로 벗어났을 수 있습니다. 프로그램을 재시작하세요
- GPU 드라이버를 최신 버전으로 업데이트하세요

## 라이선스

이 프로젝트는 개인 및 상업적 용도로 자유롭게 사용할 수 있습니다.

## 기여

버그 리포트, 기능 제안, 코드 기여를 환영합니다!

## 개발자

Agent Assistant Team

---

**즐거운 코딩 되세요! 😊**


