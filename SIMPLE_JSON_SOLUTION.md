# 간단한 JSON 기반 솔루션

## 현재 문제점
- 과도하게 복잡한 Flask + Socket.IO + SQLite 구조
- 단일 사용자 앱에 실시간 동기화는 불필요
- AWS EC2 배포까지 필요 없음

## 제안하는 간단한 구조

### 1. 부서 목록 관리
```json
// departments.json
{
  "departments": [
    {"id": 1, "name": "개발팀", "description": "소프트웨어 개발"},
    {"id": 2, "name": "마케팅팀", "description": "마케팅 및 홍보"},
    {"id": 3, "name": "영업팀", "description": "고객 영업"}
  ]
}
```

### 2. 이벤트 저장
```json
// calendar_events.json
{
  "2024-01-15": [
    {
      "id": 1,
      "departmentId": 1,
      "title": "팀 미팅",
      "description": "주간 팀 미팅",
      "time": "14:00",
      "url": ""
    }
  ]
}
```

### 3. 설정 관리
```json
// sync_settings.json
{
  "EnableSync": false,
  "ServerUrl": "",
  "SelectedDepartmentId": 1,
  "SelectedDepartmentName": "개발팀"
}
```

## 구현 방법

### 1. 부서 관리 클래스
```csharp
public class DepartmentManager
{
    private string filePath = "departments.json";
    
    public List<Department> LoadDepartments()
    {
        // JSON 파일에서 부서 목록 로드
    }
    
    public void SaveDepartments(List<Department> departments)
    {
        // JSON 파일에 부서 목록 저장
    }
}
```

### 2. 이벤트 관리 클래스
```csharp
public class EventManager
{
    private string filePath = "calendar_events.json";
    
    public Dictionary<DateTime, List<CalendarEvent>> LoadEvents()
    {
        // JSON 파일에서 이벤트 로드
    }
    
    public void SaveEvents(Dictionary<DateTime, List<CalendarEvent>> events)
    {
        // JSON 파일에 이벤트 저장
    }
}
```

## 장점

### ✅ **단순함**
- 서버 불필요
- 데이터베이스 불필요
- 네트워크 통신 불필요

### ✅ **빠른 성능**
- 로컬 파일 읽기/쓰기
- 네트워크 지연 없음
- 즉시 응답

### ✅ **배포 용이성**
- 단일 실행 파일
- 외부 의존성 없음
- 어디서든 실행 가능

### ✅ **데이터 안전성**
- 로컬 파일 백업
- 버전 관리 가능
- 복구 용이

## 마이그레이션 계획

### 1단계: JSON 기반 클래스 생성
- DepartmentManager
- EventManager
- SettingsManager

### 2단계: 기존 서버 코드 제거
- SyncService 제거
- 서버 연결 코드 제거
- Socket.IO 제거

### 3단계: 로컬 파일 기반으로 변경
- 부서 목록을 JSON에서 로드
- 이벤트를 JSON에서 로드/저장
- 설정을 JSON에서 로드/저장

## 결론

**현재 요구사항에는 JSON 파일만 있으면 충분합니다!**

- 서버 불필요
- AWS 불필요
- 복잡한 동기화 불필요
- 단순하고 빠른 로컬 솔루션

이렇게 하면 훨씬 간단하고 효율적인 시스템이 됩니다.


