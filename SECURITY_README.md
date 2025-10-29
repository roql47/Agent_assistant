# 🔒 쿠키 암호화 보안 개선

## 적용 내역

### ✅ 완료된 작업

1. **DPAPI 암호화 시스템 구축** (`CookieEncryption.cs`)
   - Windows DPAPI (Data Protection API) 사용
   - 현재 로그인한 사용자만 복호화 가능
   - 다른 사용자나 다른 컴퓨터에서는 복호화 불가능
   - 키 관리를 Windows OS가 자동으로 처리

2. **기존 코드에 암호화 적용**
   - `MainWindow.xaml.cs`: 쿠키 저장 시 자동 암호화
   - `CookieInputDialog.xaml.cs`: 수동 입력 쿠키 암호화
   - `HttpIntranetCrawler.cs`: 암호화된 쿠키 읽기

3. **자동 마이그레이션** (`MigrateCookies.cs`)
   - 프로그램 시작 시 평문 쿠키 자동 암호화
   - `manual_cookies.json` → `manual_cookies.dat`
   - 원본 파일은 `.backup`으로 보존

4. **.gitignore 보안 강화**
   - 민감한 파일들을 Git 추적에서 제외
   - 쿠키, 로그인 정보, 개인 데이터 보호

## 파일 변경 사항

### 암호화 파일 형식
- **이전**: `manual_cookies.json` (평문 JSON)
- **이후**: `manual_cookies.dat` (DPAPI 암호화 바이너리)

### 보안 수준 비교

| 구분 | 이전 (평문) | 이후 (DPAPI) |
|------|------------|--------------|
| 파일 형식 | JSON (텍스트) | 바이너리 |
| 암호화 | ❌ 없음 | ✅ DPAPI |
| 타 사용자 접근 | ⚠️ 가능 | ✅ 불가능 |
| 타 컴퓨터 접근 | ⚠️ 가능 | ✅ 불가능 |
| Git 노출 | ⚠️ 위험 | ✅ .gitignore 적용 |
| 키 관리 | ❌ 불필요 | ✅ OS 자동 관리 |

## 사용 방법

### 자동 마이그레이션
프로그램을 실행하면 자동으로 평문 쿠키를 암호화합니다.

```
manual_cookies.json → manual_cookies.dat (암호화)
manual_cookies.json → manual_cookies.json.backup (백업)
```

### 수동 마이그레이션
필요시 코드에서 호출 가능:
```csharp
CookieMigrationHelper.ManualMigrate();
```

### 암호화 상태 확인
```csharp
string status = CookieMigrationHelper.GetEncryptionStatus();
```

## 보안 특징

### 1. Windows DPAPI
- Microsoft의 공식 암호화 API
- AES-256 수준의 강력한 암호화
- 사용자 프로필과 연동되어 보안성 극대화

### 2. CurrentUser 스코프
- 현재 Windows 사용자만 복호화 가능
- 관리자라도 다른 사용자의 데이터는 복호화 불가

### 3. 추가 엔트로피
- Salt 역할을 하는 고유 식별자 추가
- 무작위 대입 공격 방어

## Git 보안

`.gitignore`에 추가된 민감 파일:
```
manual_cookies.json
manual_cookies.dat
manual_cookies*.json
login_info.json
login.txt
calendar_events.json
mail_page_*.html
```

## 주의사항

### ⚠️ 백업 필요
- DPAPI 암호화는 사용자 프로필에 종속
- Windows 재설치 시 복호화 불가능
- 중요한 쿠키는 별도 백업 권장

### ⚠️ 계정 변경 시
- 다른 Windows 계정에서는 복호화 불가
- 계정 변경 전 쿠키 재입력 필요

### ⚠️ 컴퓨터 변경 시
- 다른 컴퓨터에서는 복호화 불가
- 새 컴퓨터에서 쿠키 재입력 필요

## 기술 스택

- **암호화**: Windows DPAPI (ProtectedData)
- **프레임워크**: .NET 7.0
- **언어**: C# 11
- **보안 수준**: 매우 높음 ⭐⭐⭐⭐⭐

## 문의

암호화 관련 문제가 발생하면:
1. 프로그램 재실행 (자동 마이그레이션)
2. 쿠키 수동 재입력 (CookieInputDialog)
3. 디버그 로그 확인 (System.Diagnostics.Debug)

---

**생성일**: 2025-10-20  
**버전**: 1.0  
**상태**: 프로덕션 준비 완료 ✅

