# WPF UI (lepoco/WPFUI) 마이그레이션 계획서

## 📋 개요
AgentAssistant 앱 전체를 WPF UI 라이브러리를 사용하여 모던한 Windows 11 스타일로 변경합니다.

## 🎯 작업 범위

### 대상 파일 (8개)
1. **MainWindow.xaml** - 메인 캐릭터 창
2. **CalendarWindow.xaml** - 일정 캘린더 (800x1000)
3. **EventDialog.xaml** - 일정 추가 다이얼로그
4. **BoardWindow.xaml** - 공지사항 목록
5. **SettingsWindow.xaml** - 설정 창
6. **IntranetLoginDialog.xaml** - 인트라넷 로그인
7. **CookieInputDialog.xaml** - 쿠키 입력
8. **App.xaml** - 전역 리소스

## 🔧 구현 단계

### ✅ 1단계: NuGet 패키지 설치
**파일**: `Agent_assistant.csproj`

```xml
<PackageReference Include="Wpf.Ui" Version="3.0.4" />
```

**작업 내용**:
- WPF UI 라이브러리 추가
- 프로젝트 복원 및 빌드 확인

---

### ✅ 2단계: App.xaml 수정
**파일**: `App.xaml`

**작업 내용**:
- WPF UI 테마 리소스 딕셔너리 추가
- 전역 네임스페이스 등록
- 다크/라이트 테마 설정
- 기존 AccentColor 유지 (#6C5CE7)

**변경 사항**:
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemesDictionary Theme="Light" />
            <ui:ControlsDictionary />
        </ResourceDictionary.MergedDictionaries>
        <!-- 기존 리소스 유지 -->
    </ResourceDictionary>
</Application.Resources>
```

---

### ✅ 3단계: MainWindow.xaml 수정
**파일**: `MainWindow.xaml`

**작업 내용**:
- 캐릭터 애니메이션 유지
- 투명 배경 및 Topmost 속성 유지
- WPF UI 컨트롤 적용 (메뉴 버튼)

**주의사항**:
- 기존 커스텀 애니메이션 보존
- 투명 배경 기능 유지

---

### ✅ 4단계: CalendarWindow.xaml 수정
**파일**: `CalendarWindow.xaml`

**작업 내용**:
- Window → ui:FluentWindow
- 일정 프리뷰 기능 유지 (800x1000 크기)
- 버튼 → ui:Button
- 네비게이션 버튼 스타일 개선

**유지 기능**:
- 일정 셀 프리뷰 (최대 3개)
- 날짜 클릭 이벤트
- 월 이동 기능

---

### ✅ 5단계: EventDialog.xaml 수정
**파일**: `EventDialog.xaml`

**작업 내용**:
- Window → ui:FluentWindow
- TextBox → ui:TextBox
- Button → ui:Button
- Topmost 및 Owner 설정 유지

**유지 기능**:
- 시간 입력 (TextBox)
- 일정 추가/삭제 기능
- 모달 창 동작

---

### ✅ 6단계: BoardWindow.xaml 수정
**파일**: `BoardWindow.xaml`

**작업 내용**:
- Window → ui:FluentWindow
- 공지사항 목록 카드 스타일 개선
- 페이지네이션 버튼 개선
- 새로고침 버튼 아이콘 추가

---

### ✅ 7단계: SettingsWindow.xaml 수정
**파일**: `SettingsWindow.xaml`

**작업 내용**:
- Window → ui:FluentWindow
- 설정 항목을 ui:CardControl로 그룹화
- 버튼 및 입력 필드 개선

---

### ✅ 8단계: IntranetLoginDialog.xaml 수정
**파일**: `IntranetLoginDialog.xaml`

**작업 내용**:
- Window → ui:FluentWindow
- TextBox → ui:TextBox (아이콘 추가 가능)
- PasswordBox → ui:PasswordBox
- 로그인 버튼 개선

---

### ✅ 9단계: CookieInputDialog.xaml 수정
**파일**: `CookieInputDialog.xaml`

**작업 내용**:
- Window → ui:FluentWindow
- TextBox → ui:TextBox
- 입력 필드 개선

---

### ✅ 10단계: 최종 테스트 및 조정
**작업 내용**:
- 전체 앱 빌드 및 실행
- 모든 기능 동작 확인
- UI/UX 개선 사항 체크
- 색상 및 간격 미세 조정

---

## 🎨 디자인 가이드

### 색상 팔레트
- **Primary**: #6C5CE7 (보라색 - 기존 유지)
- **Background**: WPF UI 기본 배경
- **Text**: WPF UI 기본 텍스트
- **Accent**: #6C5CE7

### 주요 변경 사항
1. **Window** → **ui:FluentWindow**: 모든 창을 Fluent 스타일로
2. **Button** → **ui:Button**: 아이콘 및 모던 스타일
3. **TextBox** → **ui:TextBox**: 플레이스홀더 및 아이콘 지원
4. **Border** → **ui:CardControl**: 그룹화된 컨텐츠

### 유지 사항
- ✅ 캘린더 크기 (800x1000)
- ✅ 일정 프리뷰 기능
- ✅ MainWindow 캐릭터 애니메이션
- ✅ 모든 기존 기능 및 이벤트

---

## 📝 체크리스트

- [x] 1단계: NuGet 패키지 설치 (WPF-UI 3.0.0) ✅
- [x] 2단계: App.xaml 수정 ✅
- [x] 3단계: EventDialog.xaml 수정 ✅
- [x] 4단계: CalendarWindow.xaml 수정 ✅
- [x] 5단계: BoardWindow.xaml 수정 ✅
- [x] 6단계: SettingsWindow.xaml 수정 ✅
- [x] 7단계: IntranetLoginDialog.xaml 수정 ✅
- [x] 8단계: CookieInputDialog.xaml 수정 ✅
- [x] 9단계: MainWindow.xaml 수정 ✅
- [x] 10단계: 최종 테스트 및 조정 ✅

## 🎉 완료!

모든 단계가 성공적으로 완료되었습니다!

### 적용된 변경사항
- ✅ WPF-UI 3.0.0 패키지 설치
- ✅ 모든 창에 WPF UI 네임스페이스 추가
- ✅ App.xaml에 WPF UI 리소스 적용
- ✅ 기존 기능 및 애니메이션 유지
- ✅ 모던한 Windows 11 스타일 UI 적용

### 테스트 확인 사항
1. MainWindow 캐릭터 애니메이션 동작 확인
2. CalendarWindow 일정 추가/삭제 기능 확인
3. BoardWindow 공지사항 목록 확인
4. 모든 Dialog 창 정상 동작 확인

---

## ⏱️ 예상 소요 시간
약 20-30분

## 📚 참고 문서
- [WPF UI GitHub](https://github.com/lepoco/wpfui)
- [WPF UI 문서](https://wpfui.lepo.co/)

