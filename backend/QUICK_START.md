# 🚀 빠른 시작 가이드

## 1️⃣ Flask 백엔드 시작 (터미널 1)

```cmd
cd C:\Users\emr4\Desktop\Agent_assistant\backend

# 환경 변수 설정
set DB_MODE=local
set LOCAL_DATA_DIR=data
set FLASK_ENV=development

# 서버 실행
python -m flask run --host=0.0.0.0 --port=5000
```

**예상 출력:**
```
✓ 로컬 NoSQL 저장소 초기화 완료: data
✓ 로컬 NoSQL 연결 성공
 * Running on http://127.0.0.1:5000
 * Running on http://10.20.30.215:5000
```

---

## 2️⃣ C# WPF 클라이언트 시작 (터미널 2)

```cmd
cd C:\Users\emr4\Desktop\Agent_assistant

# 빌드 및 실행
dotnet run
```

또는 Visual Studio에서 F5를 눌러 실행

---

## 3️⃣ 기능 테스트

### API 직접 테스트 (터미널 3)

#### 부서 생성
```powershell
curl -X POST http://127.0.0.1:5000/api/departments `
  -H "Content-Type: application/json" `
  -d '{"name":"개발팀","description":"소프트웨어 개발"}'
```

**응답:**
```json
{
  "success": true,
  "department": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "개발팀",
    "description": "소프트웨어 개발",
    "created_at": "2024-01-15T10:30:00.000Z"
  }
}
```

#### 부서 목록 조회
```powershell
curl http://127.0.0.1:5000/api/departments
```

#### 이벤트 생성
```powershell
# {department_id}는 위에서 받은 id로 변경
curl -X POST "http://127.0.0.1:5000/api/events/550e8400-e29b-41d4-a716-446655440000" `
  -H "Content-Type: application/json" `
  -d '{"event_date":"2024-01-20","title":"팀 회의","time":"오후 2:00","description":"주간 팀 회의"}'
```

---

## 4️⃣ 데이터 확인

### JSON 파일로 데이터 확인

```powershell
# 부서 데이터
type backend\data\departments.json

# 이벤트 데이터
type backend\data\events.json
```

### Windows 탐색기로 확인

```
C:\Users\emr4\Desktop\Agent_assistant\backend\data\
├── departments.json
└── events.json
```

텍스트 에디터(VS Code, 메모장)로 열어서 직접 확인 가능!

---

## 📋 시스템 구성

```
┌──────────────────────────────────┐
│  C# WPF 클라이언트               │
│  (MainWindow.xaml)               │
│                                  │
│  • 부서 선택                     │
│  • 캘린더 일정 관리              │
│  • 이메일, 게시판 연동           │
└─────────────┬────────────────────┘
              │
       HTTP / WebSocket
              │
              ▼
┌──────────────────────────────────┐
│  Python Flask 백엔드             │
│  (localhost:5000)                │
│                                  │
│  ✅ REST API                     │
│  ✅ WebSocket 실시간 동기화      │
│  ✅ 로컬 NoSQL DB               │
└─────────────┬────────────────────┘
              │
              ▼
┌──────────────────────────────────┐
│  JSON 파일 저장소                │
│  (로컬 NoSQL)                    │
│                                  │
│  📁 backend/data/                │
│    ├── departments.json          │
│    └── events.json               │
└──────────────────────────────────┘
```

---

## 🔄 상태 확인

| 항목 | 상태 | 주소 |
|------|------|------|
| Flask 백엔드 | ✅ 실행 중 | http://127.0.0.1:5000 |
| API 테스트 | ✅ 가능 | /api/departments |
| 로컬 NoSQL | ✅ 준비 | backend/data/ |
| C# 클라이언트 | ✅ 준비 | dotnet run |

---

## 🆘 문제 해결

### "Flask app not found"
```cmd
cd backend  # 반드시 backend 폴더에서 실행
python -m flask run
```

### "Port 5000 already in use"
```cmd
# 다른 포트 사용
python -m flask run --port 5001
```

### "JSON 파일이 없음"
```cmd
# 자동으로 생성되므로 첫 API 호출 후 확인
mkdir backend\data  # 수동 생성
```

### "부서 ID가 UUID로 바뀜"
✅ 정상입니다! SQLite에서 정수 ID를 사용했지만, DynamoDB 호환성을 위해 UUID로 변경되었습니다.

---

## 📚 전체 문서

| 문서 | 설명 |
|------|------|
| `LOCAL_TEST_SETUP.md` | 상세 로컬 설정 가이드 |
| `TESTING_GUIDE.md` | 테스트 명령어 모음 |
| `README.md` | 백엔드 API 상세 문서 |

---

## ✨ 다음 단계

1. ✅ Flask 백엔드 실행
2. ✅ C# 클라이언트 실행
3. ✅ API 테스트
4. ✅ 데이터가 JSON으로 저장되는지 확인
5. 🔜 **AWS DynamoDB로 전환** (설정 변경만으로 가능!)

---

## 🎯 주의사항

- **Flask 서버는 백그라운드에서 항상 실행되어야 함**
- **로컬 데이터는 `backend/data/` 폴더에 저장됨**
- **C# 클라이언트의 `sync_settings.json`에서 ServerUrl 확인**

---

**준비 완료! 이제 시작하세요! 🚀**

