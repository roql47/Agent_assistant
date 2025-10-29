# 로컬 테스트 환경 NoSQL 설정 가이드

## 개요

AWS DynamoDB 없이 **로컬 파일 기반 NoSQL 데이터베이스**를 사용하여 테스트할 수 있습니다.

JSON 파일로 부서와 이벤트 데이터를 저장하며, DynamoDB와 동일한 인터페이스를 제공합니다.

---

## 빠른 시작 (2분)

### 1단계: 환경 설정

```bash
cd backend

# 환경 변수 설정 (로컬 모드)
export DB_MODE=local
export LOCAL_DATA_DIR=data

# Windows (CMD)
set DB_MODE=local
set LOCAL_DATA_DIR=data
```

### 2단계: 패키지 설치

```bash
pip install -r requirements.txt
```

### 3단계: 서버 실행

```bash
export FLASK_ENV=development
flask run --host=0.0.0.0 --port=5000
```

**출력:**
```
✓ 로컬 NoSQL 저장소 초기화 완료: data
✓ 로컬 NoSQL 연결 성공
 * Running on http://0.0.0.0:5000
```

### 4단계: 테스트

```bash
# 부서 생성
curl -X POST http://localhost:5000/api/departments \
  -H "Content-Type: application/json" \
  -d '{"name":"개발팀","description":"소프트웨어 개발"}'

# 부서 목록 조회
curl http://localhost:5000/api/departments
```

---

## 디렉토리 구조

```
backend/
├── data/                      # 로컬 NoSQL 데이터 저장소 (자동 생성)
│   ├── departments.json       # 부서 데이터
│   └── events.json            # 이벤트 데이터
│
├── local_nosql.py            # 로컬 NoSQL 구현
├── models.py                  # DynamoDB & 로컬 모드 통합
├── app.py                     # Flask 앱 (모드 자동 선택)
└── ...
```

---

## 데이터 파일 예시

### departments.json
```json
{
  "550e8400-e29b-41d4-a716-446655440000": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "개발팀",
    "description": "소프트웨어 개발",
    "created_at": "2024-01-15T10:30:00.000Z",
    "updated_at": "2024-01-15T10:30:00.000Z"
  },
  "660e8400-e29b-41d4-a716-446655440000": {
    "id": "660e8400-e29b-41d4-a716-446655440000",
    "name": "마케팅팀",
    "description": "마케팅 및 홍보",
    "created_at": "2024-01-15T10:35:00.000Z",
    "updated_at": "2024-01-15T10:35:00.000Z"
  }
}
```

### events.json
```json
{
  "770e8400-e29b-41d4-a716-446655440000": {
    "id": "770e8400-e29b-41d4-a716-446655440000",
    "department_id": "550e8400-e29b-41d4-a716-446655440000",
    "event_date": "2024-01-20",
    "title": "팀 회의",
    "description": "주간 팀 회의",
    "time": "오후 2:00",
    "url": "https://example.com",
    "created_at": "2024-01-15T10:30:00.000Z",
    "last_modified": "2024-01-15T10:30:00.000Z"
  }
}
```

---

## 환경 변수

### 기본 설정

```bash
# 데이터베이스 모드
DB_MODE=local              # 'local' (기본값) 또는 'aws'

# 로컬 데이터 디렉토리
LOCAL_DATA_DIR=data        # 기본값: 'data'

# Flask 설정
FLASK_ENV=development      # 개발 모드
SECRET_KEY=test-key        # 테스트용
```

### AWS 모드 전환

로컬에서 AWS로 전환하려면:

```bash
export DB_MODE=aws
export AWS_REGION=ap-northeast-2
export AWS_ACCESS_KEY_ID=your_key
export AWS_SECRET_ACCESS_KEY=your_secret
```

---

## API 사용 예시

### 부서 관리

#### 부서 생성
```bash
curl -X POST http://localhost:5000/api/departments \
  -H "Content-Type: application/json" \
  -d '{
    "name": "새로운팀",
    "description": "팀 설명"
  }'
```

**응답:**
```json
{
  "success": true,
  "department": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "새로운팀",
    "description": "팀 설명",
    "created_at": "2024-01-15T10:30:00.000Z"
  }
}
```

#### 부서 목록 조회
```bash
curl http://localhost:5000/api/departments
```

#### 부서 삭제
```bash
curl -X DELETE http://localhost:5000/api/departments/550e8400-e29b-41d4-a716-446655440000
```

### 이벤트 관리

#### 이벤트 생성
```bash
curl -X POST http://localhost:5000/api/events/550e8400-e29b-41d4-a716-446655440000 \
  -H "Content-Type: application/json" \
  -d '{
    "event_date": "2024-01-20",
    "title": "팀 회의",
    "time": "오후 2:00",
    "description": "주간 팀 회의"
  }'
```

#### 이벤트 조회
```bash
curl http://localhost:5000/api/events/550e8400-e29b-41d4-a716-446655440000
```

#### 이벤트 수정
```bash
curl -X PUT http://localhost:5000/api/events/770e8400-e29b-41d4-a716-446655440000 \
  -H "Content-Type: application/json" \
  -d '{
    "title": "수정된 회의",
    "time": "오후 3:00"
  }'
```

#### 이벤트 삭제
```bash
curl -X DELETE http://localhost:5000/api/events/770e8400-e29b-41d4-a716-446655440000
```

---

## WebSocket 테스트

### Node.js 클라이언트 예시

```javascript
const io = require('socket.io-client');

const socket = io('http://localhost:5000');

// 연결
socket.on('connected', (data) => {
  console.log('연결됨:', data.message);
});

// 부서 그룹 참여
socket.emit('join_department', {
  'department_id': '550e8400-e29b-41d4-a716-446655440000'
});

// 이벤트 생성 알림 수신
socket.on('event_created', (event) => {
  console.log('새 이벤트:', event);
});

// 이벤트 생성 (API를 통해 하면 WebSocket으로도 알림)
```

### Python 클라이언트 예시

```python
from socketio import Client

sio = Client()

@sio.on('connected')
def on_connected(data):
    print('연결됨:', data['message'])
    
    # 부서 그룹 참여
    sio.emit('join_department', {
        'department_id': '550e8400-e29b-41d4-a716-446655440000'
    })

@sio.on('event_created')
def on_event_created(event):
    print('새 이벤트:', event)

sio.connect('http://localhost:5000')
sio.wait()
```

---

## 데이터 초기화

### 모든 데이터 삭제

```bash
# Unix/Linux/Mac
rm -rf backend/data

# Windows
rmdir /s /q backend\data
```

서버 재시작 시 자동으로 빈 데이터 폴더가 생성됩니다.

### 특정 파일 삭제

```bash
# 부서 데이터만 삭제
rm backend/data/departments.json

# 이벤트 데이터만 삭제
rm backend/data/events.json
```

---

## 데이터 백업

### 수동 백업

```bash
# data 폴더 전체 복사
cp -r backend/data backend/data.backup

# 또는 zip으로 압축
zip -r backend/data.backup.zip backend/data
```

### 자동 백업 스크립트

```python
# backup_local_data.py
import shutil
from datetime import datetime

timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
backup_dir = f"data.backup_{timestamp}"
shutil.copytree("data", backup_dir)
print(f"✓ 백업 완료: {backup_dir}")
```

```bash
python backup_local_data.py
```

---

## 문제 해결

### 1. "로컬 NoSQL 저장소 초기화 실패"

**원인:** data 디렉토리 생성 권한 없음

**해결:**
```bash
# 수동으로 디렉토리 생성
mkdir backend/data

# 권한 설정 (Unix/Linux)
chmod 755 backend/data
```

### 2. "Item not found" 오류

**원인:** 잘못된 부서/이벤트 ID 사용

**해결:**
- 먼저 부서/이벤트 목록 조회
- 응답에서 ID 복사해서 사용

```bash
# 부서 ID 확인
curl http://localhost:5000/api/departments

# 응답에서 "id" 값 사용
curl http://localhost:5000/api/events/{id}
```

### 3. JSON 파일 손상

**증상:** "JSONDecodeError" 오류

**해결:**
```bash
# 파일 백업
cp backend/data/departments.json backend/data/departments.json.bak

# 파일 초기화
echo "{}" > backend/data/departments.json
echo "{}" > backend/data/events.json
```

---

## 로컬 모드 vs AWS 모드

| 항목 | 로컬 모드 | AWS 모드 |
|------|---------|---------|
| 설정 | 간단 (파일만 생성) | 복잡 (AWS 계정 필요) |
| 속도 | 빠름 (로컬 파일) | 중간 (네트워크 왕복) |
| 확장성 | 제한적 | 무제한 |
| 비용 | 무료 | 사용량 기반 ($) |
| 백업 | 수동 | 자동 |
| **사용 시기** | **개발/테스트** | **프로덕션** |

---

## 로컬 → AWS 마이그레이션

### 1단계: AWS DynamoDB 준비

```bash
# AWS_DEPLOYMENT_CONFIG.md 또는 DYNAMODB_SETUP.md 참고
```

### 2단계: 환경 변수 변경

```bash
export DB_MODE=aws
export AWS_REGION=ap-northeast-2
export AWS_ACCESS_KEY_ID=your_key
export AWS_SECRET_ACCESS_KEY=your_secret
```

### 3단계: 데이터 마이그레이션

```bash
# 기존 로컬 데이터를 AWS로 이전
python migrate_to_dynamodb.py
```

### 4단계: 서버 재시작

```bash
flask run --host=0.0.0.0 --port=5000
```

---

## 성능 최적화

### 대량 데이터 추가

```python
# bulk_insert.py
import requests
import json

base_url = 'http://localhost:5000'

# 100개 이벤트 추가
for i in range(100):
    event_data = {
        "event_date": f"2024-01-{(i % 28) + 1:02d}",
        "title": f"이벤트 {i+1}",
        "time": f"오후 {(i % 12) + 1}:00"
    }
    
    # 첫 번째 부서로 테스트
    response = requests.post(
        f'{base_url}/api/events/550e8400-e29b-41d4-a716-446655440000',
        json=event_data
    )
    
    if response.status_code == 201:
        print(f"✓ 이벤트 {i+1} 추가 완료")
    else:
        print(f"✗ 실패: {response.text}")
```

```bash
python bulk_insert.py
```

---

## 다음 단계

1. ✅ 로컬 모드로 개발 및 테스트
2. ✅ 기능 검증 완료
3. ✅ AWS 모드로 전환 (프로덕션)
4. ✅ CloudWatch 모니터링 설정

---

## 참고 자료

- `backend/models.py` - 데이터베이스 모델
- `backend/local_nosql.py` - 로컬 NoSQL 구현
- `backend/app.py` - Flask 애플리케이션
- `AWS_DEPLOYMENT_CONFIG.md` - AWS 배포 가이드

---

## 지원

문제 발생 시:
1. 이 문서의 "문제 해결" 섹션 확인
2. `data/` 디렉토리의 JSON 파일 직접 확인
3. 서버 로그 확인

---

**Happy Testing! 🚀**

