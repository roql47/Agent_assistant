# 캘린더 동기화 시스템 사용 가이드

## 개요

Agent Assistant의 캘린더를 Flask 서버를 통해 부서별로 그룹화하고, 여러 PC 간 실시간 동기화가 가능합니다.

## 시스템 구성

```
┌─────────────────┐     WebSocket      ┌─────────────────┐
│   클라이언트 A   │ ◄──────────────► │                 │
│   (Windows PC)  │                    │  Flask 서버     │
└─────────────────┘                    │  (AWS EC2 또는  │
                                       │   로컬 서버)    │
┌─────────────────┐     WebSocket      │                 │
│   클라이언트 B   │ ◄──────────────► │  - 부서 관리    │
│   (Windows PC)  │                    │  - 이벤트 저장  │
└─────────────────┘                    │  - 실시간 동기화│
                                       └─────────────────┘
┌─────────────────┐     WebSocket      
│   클라이언트 C   │ ◄──────────────►
│   (Windows PC)  │
└─────────────────┘
```

## 1단계: 서버 설정

### 방법 A: AWS EC2 사용 (권장)

AWS EC2에 서버를 배포하는 상세 가이드는 `backend/AWS_DEPLOYMENT.md`를 참조하세요.

**요약:**
1. AWS EC2 t2.micro 인스턴스 생성 (무료 티어 1년)
2. Ubuntu 22.04 LTS 선택
3. Security Group에서 포트 5000 개방
4. Elastic IP 할당 (고정 IP)
5. SSH로 접속하여 Flask 서버 설치 및 실행

**예상 비용:**
- 무료 티어: 1년간 무료
- 무료 티어 이후: 월 약 $10

### 방법 B: 로컬/내부망 서버 사용

사내 네트워크 또는 로컬 PC를 서버로 사용하는 가이드는 `backend/LOCAL_DEPLOYMENT.md`를 참조하세요.

**요약:**
1. 한 대의 PC를 서버로 지정 (항상 켜져있는 PC 권장)
2. Python 3.10+ 설치
3. Flask 서버 실행
4. 방화벽에서 포트 5000 개방
5. 고정 IP 설정

## 2단계: 서버 실행

### 서버 파일 배치

1. `backend/` 폴더의 모든 파일을 서버에 복사:
   - `app.py`
   - `models.py`
   - `requirements.txt`

### 의존성 설치

```bash
# Windows
cd backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt

# Linux/Mac
cd backend
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
```

### 서버 실행

```bash
python app.py
```

서버가 `http://0.0.0.0:5000`에서 실행됩니다.

### 서버 동작 확인

브라우저에서 다음 주소로 접속:
- `http://SERVER_IP:5000`

다음과 같은 응답이 나오면 정상:
```json
{
  "status": "running",
  "message": "캘린더 동기화 서버가 실행 중입니다.",
  "version": "1.0.0"
}
```

## 3단계: 부서 생성 (최초 1회)

서버 실행 후, 부서를 생성해야 합니다.

### 방법 1: cURL로 생성 (권장)

```bash
curl -X POST http://SERVER_IP:5000/api/departments \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"개발팀\",\"description\":\"소프트웨어 개발 부서\"}"
```

### 방법 2: Python 스크립트로 생성

```python
import requests

url = "http://SERVER_IP:5000/api/departments"
data = {
    "name": "개발팀",
    "description": "소프트웨어 개발 부서"
}

response = requests.post(url, json=data)
print(response.json())
```

### 방법 3: Postman 사용

1. Postman 실행
2. POST `http://SERVER_IP:5000/api/departments`
3. Body → raw → JSON:
```json
{
  "name": "개발팀",
  "description": "소프트웨어 개발 부서"
}
```

## 4단계: 클라이언트 설정

### 1. NuGet 패키지 복원

프로젝트를 빌드하기 전에 새로 추가된 패키지를 복원해야 합니다.

```cmd
dotnet restore
```

### 2. 프로젝트 빌드

```cmd
dotnet build
```

### 3. 애플리케이션 실행

```cmd
dotnet run
```

또는 Visual Studio에서 F5를 눌러 실행합니다.

### 4. 캘린더 열기

1. 에이전트 비서 우클릭
2. **"캘린더 열기"** 클릭

### 5. 동기화 설정

1. 에이전트 비서 우클릭
2. **"설정"** 클릭
3. 하단의 **"캘린더 동기화"** 섹션으로 이동
4. **"동기화 활성화"** 체크
5. **서버 URL** 입력:
   - AWS EC2: `http://YOUR_ELASTIC_IP:5000`
   - 로컬: `http://192.168.x.x:5000` (서버 PC의 IP)
6. **"🔄" 버튼 클릭**하여 부서 목록 새로고침
7. 드롭다운에서 **부서 선택**
8. **"확인"** 클릭

### 6. 동기화 상태 확인

캘린더 창 상단에 동기화 상태가 표시됩니다:
- 🟢 **연결됨**: 정상 동기화 중
- 🟡 **연결 중...**: 서버에 연결 시도 중
- 🔴 **끊김**: 서버 연결 끊김
- 🔴 **연결 실패**: 서버에 연결할 수 없음

## 5단계: 사용하기

### 일정 추가

1. 캘린더에서 날짜 클릭
2. **"새 일정 추가"** 클릭
3. 제목, 설명, 시간 입력
4. **"추가"** 클릭

**동기화 활성화 시:**
- 일정이 자동으로 서버에 저장됨
- 같은 부서의 다른 PC에 실시간으로 반영됨

### 일정 수정

1. 일정이 있는 날짜 클릭
2. 수정할 일정 선택
3. 내용 수정
4. **"저장"** 클릭

**동기화 활성화 시:**
- 수정 사항이 즉시 서버에 반영됨
- 다른 PC에 실시간으로 업데이트됨

### 일정 삭제

1. 일정이 있는 날짜 클릭
2. 삭제할 일정 선택
3. **"삭제"** 클릭

**동기화 활성화 시:**
- 삭제가 즉시 서버에 반영됨
- 다른 PC에서도 자동으로 삭제됨

## 동기화 작동 원리

### 실시간 양방향 동기화

```
PC A에서 일정 추가
    ↓
서버로 전송 (REST API)
    ↓
서버가 데이터베이스에 저장
    ↓
WebSocket으로 모든 클라이언트에게 브로드캐스트
    ↓
PC B, PC C가 자동으로 업데이트 수신
    ↓
각 PC의 캘린더 UI가 자동 새로고침
```

### 충돌 해결

- **서버 우선 원칙**: 서버의 데이터가 항상 우선됩니다.
- 네트워크가 끊겼다가 다시 연결되면 서버로부터 전체 동기화를 받습니다.
- 로컬 파일 저장도 유지되어 오프라인 모드에서도 사용 가능합니다.

## 문제 해결

### 문제 1: 부서 목록이 로드되지 않음

**원인:**
- 서버 URL이 잘못되었거나
- 서버가 실행되지 않았거나
- 방화벽이 포트를 차단함

**해결:**
1. 서버 URL 확인: 브라우저에서 `http://SERVER_IP:5000` 접속 테스트
2. 서버가 실행 중인지 확인
3. 방화벽 설정 확인 (포트 5000 개방)

### 문제 2: 동기화 상태가 "🔴 끊김"으로 표시됨

**원인:**
- 네트워크 연결 불안정
- 서버가 중지됨

**해결:**
1. 서버 상태 확인
2. 네트워크 연결 확인
3. 설정 창에서 동기화를 비활성화했다가 다시 활성화

### 문제 3: 다른 PC의 변경사항이 반영되지 않음

**원인:**
- 각 PC가 다른 부서를 선택함
- WebSocket 연결이 끊어짐

**해결:**
1. 모든 PC가 같은 부서를 선택했는지 확인
2. 동기화 상태 확인 (🟢 연결됨이어야 함)
3. 설정에서 동기화를 재활성화

### 문제 4: 서버가 자동으로 종료됨

**원인:**
- 서버가 백그라운드로 실행되지 않음

**해결:**
- Windows: `backend/LOCAL_DEPLOYMENT.md`의 "Windows 서비스로 등록" 참조
- Linux: `backend/AWS_DEPLOYMENT.md`의 "systemd 서비스" 참조

## 보안 고려사항

⚠️ **중요: 현재 버전은 인증 시스템이 없습니다.**

### 내부 네트워크 사용 (권장)

- 사내 네트워크 또는 VPN을 통해서만 접속
- 외부 인터넷에 노출하지 않기

### AWS EC2 사용 시

- Security Group에서 특정 IP만 허용하도록 설정
- 필요한 경우 VPN 사용
- 정기적인 백업

## 성능 최적화

### 소규모 팀 (10명 이하)

- t2.micro (1GB RAM)로 충분
- 동시 접속 및 실시간 동기화 가능

### 중규모 팀 (10-50명)

- t2.small (2GB RAM) 권장
- 또는 Gunicorn으로 멀티 워커 설정

### 대규모 팀 (50명 이상)

- t2.medium (4GB RAM) 이상
- 로드 밸런서 고려
- 데이터베이스를 PostgreSQL/MySQL로 마이그레이션

## FAQ

### Q: 인터넷 없이 사용할 수 있나요?

A: 네, 로컬 파일 저장이 유지되므로 오프라인에서도 사용 가능합니다. 다만 동기화는 서버 연결 시에만 작동합니다.

### Q: 데이터는 어디에 저장되나요?

A: 서버의 SQLite 데이터베이스(`calendar_sync.db`)와 각 클라이언트의 로컬 JSON 파일(`calendar_events.json`)에 저장됩니다.

### Q: 여러 부서를 동시에 구독할 수 있나요?

A: 현재 버전은 한 번에 하나의 부서만 선택 가능합니다. 여러 부서의 일정을 보려면 설정에서 부서를 변경하세요.

### Q: 이벤트를 부서간 이동할 수 있나요?

A: 직접적인 이동은 불가능합니다. 한 부서에서 삭제하고 다른 부서에서 다시 생성해야 합니다.

### Q: 서버를 종료하면 데이터가 사라지나요?

A: 아니요, 서버의 SQLite 데이터베이스 파일(`calendar_sync.db`)에 영구 저장됩니다. 서버를 재시작해도 데이터가 유지됩니다.

## 백업 및 복구

### 데이터베이스 백업

**서버에서:**
```bash
# 수동 백업
cp calendar_sync.db calendar_sync.db.backup

# 자동 백업 (cron)
# crontab -e
0 3 * * * cp /path/to/calendar_sync.db /backup/calendar_sync_$(date +\%Y\%m\%d).db
```

### 데이터베이스 복구

```bash
# 서버 중지
sudo systemctl stop calendar-sync

# 백업 파일로 복원
cp calendar_sync.db.backup calendar_sync.db

# 서버 시작
sudo systemctl start calendar-sync
```

## 추가 기능 아이디어

향후 추가 가능한 기능:
- 🔐 사용자 인증 시스템
- 📧 이메일 알림
- 🔔 일정 알림 (D-Day)
- 📱 모바일 앱 지원
- 🎨 부서별 색상 구분
- 📊 통계 및 리포트
- 🔍 검색 기능
- 📤 일정 내보내기 (iCal, CSV)

## 지원 및 문의

문제가 발생하거나 질문이 있으면:
1. 서버 로그 확인: `backend/README.md` 참조
2. 클라이언트 로그 확인: Visual Studio의 Output 창
3. GitHub Issues에 문의

---

**즐거운 일정 관리 되세요! 📅✨**

