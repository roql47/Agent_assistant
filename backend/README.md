# 캘린더 동기화 서버 - DynamoDB 버전

## 개요

Flask + Socket.IO를 기반으로 한 실시간 캘린더 동기화 서버입니다.
부서별 그룹 관리 및 캘린더 이벤트 동기화를 **AWS DynamoDB** NoSQL 데이터베이스로 관리합니다.

---

## 주요 기능

- ✅ **부서 관리**: 부서 생성, 조회, 삭제
- ✅ **캘린더 이벤트 관리**: 이벤트 생성, 수정, 삭제, 조회
- ✅ **실시간 동기화**: WebSocket을 통한 실시간 이벤트 전파
- ✅ **부서별 그룹 관리**: 부서별로 클라이언트를 분리하여 관리
- ✅ **DynamoDB 기반**: AWS의 확장 가능한 NoSQL 데이터베이스 사용

---

## 설치 및 설정

### 1. 필수 요구사항

- Python 3.8+
- AWS 계정 및 자격증명
- AWS CLI 설치 (선택사항)

### 2. Python 패키지 설치

```bash
pip install -r requirements.txt
```

### 3. AWS 환경 변수 설정

**.env 파일 또는 환경 변수로 설정:**

```bash
# AWS 설정
AWS_REGION=ap-northeast-2
AWS_ACCESS_KEY_ID=your_access_key
AWS_SECRET_ACCESS_KEY=your_secret_key

# DynamoDB 테이블 이름
DEPARTMENTS_TABLE=departments
EVENTS_TABLE=events

# Flask 설정
FLASK_ENV=development
SECRET_KEY=your-secret-key-here
```

### 4. DynamoDB 테이블 생성

#### 방법 1: Python으로 생성 (권장)

```python
from models import DynamoDBDatabase

db = DynamoDBDatabase()
db.create_tables()
```

#### 방법 2: AWS CLI로 생성

```bash
# 부서 테이블
aws dynamodb create-table \
  --table-name departments \
  --attribute-definitions AttributeName=id,AttributeType=S AttributeName=name,AttributeType=S \
  --key-schema AttributeName=id,KeyType=HASH \
  --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10 \
  --region ap-northeast-2

# 이벤트 테이블
aws dynamodb create-table \
  --table-name events \
  --attribute-definitions AttributeName=id,AttributeType=S AttributeName=department_id,AttributeType=S AttributeName=event_date,AttributeType=S \
  --key-schema AttributeName=id,KeyType=HASH AttributeName=department_id,KeyType=RANGE \
  --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10 \
  --region ap-northeast-2
```

---

## 데이터 마이그레이션

기존 JSON 파일에서 DynamoDB로 데이터를 마이그레이션합니다:

```bash
python migrate_to_dynamodb.py
```

**출력 예:**
```
==================================================
🚀 JSON → DynamoDB 마이그레이션 시작
==================================================

📋 부서 데이터 마이그레이션 시작...
  ✓ 부서 '개발팀' 마이그레이션 완료
  ✓ 부서 '마케팅팀' 마이그레이션 완료
✅ 부서 데이터 마이그레이션 완료: 2개

📅 캘린더 이벤트 데이터 마이그레이션 시작...
  ✓ 이벤트 '팀 회의' (2024-01-20) 마이그레이션 완료
✅ 이벤트 데이터 마이그레이션 완료: 1개

🔍 마이그레이션 검증 중...
  • 부서: 2개
  • 이벤트: 1개

✅ 마이그레이션 검증 완료
==================================================
```

---

## 서버 실행

### 개발 모드

```bash
export FLASK_ENV=development
flask run --host=0.0.0.0 --port=5000
```

### 프로덕션 모드

```bash
gunicorn --worker-class eventlet -w 1 app:app --bind 0.0.0.0:8000
```

---

## API 엔드포인트

### 부서 API

#### 모든 부서 조회
```bash
GET /api/departments

Response:
{
  "success": true,
  "departments": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "개발팀",
      "description": "소프트웨어 개발",
      "created_at": "2024-01-15T10:30:00.000Z"
    }
  ]
}
```

#### 새 부서 생성
```bash
POST /api/departments

Request:
{
  "name": "새로운팀",
  "description": "팀 설명"
}

Response:
{
  "success": true,
  "department": {
    "id": "660e8400-e29b-41d4-a716-446655440000",
    "name": "새로운팀",
    "description": "팀 설명",
    "created_at": "2024-01-15T10:30:00.000Z"
  }
}
```

#### 부서 삭제
```bash
DELETE /api/departments/{department_id}

Response:
{
  "success": true,
  "message": "부서가 삭제되었습니다."
}
```

### 이벤트 API

#### 부서의 모든 이벤트 조회
```bash
GET /api/events/{department_id}

Response:
{
  "success": true,
  "events": [
    {
      "id": "770e8400-e29b-41d4-a716-446655440000",
      "department_id": "550e8400-e29b-41d4-a716-446655440000",
      "event_date": "2024-01-20",
      "title": "팀 회의",
      "description": "주간 팀 회의",
      "time": "오후 2:00",
      "url": "https://example.com",
      "last_modified": "2024-01-15T10:30:00.000Z"
    }
  ]
}
```

#### 새 이벤트 생성
```bash
POST /api/events/{department_id}

Request:
{
  "event_date": "2024-01-20",
  "title": "팀 회의",
  "description": "주간 팀 회의",
  "time": "오후 2:00",
  "url": "https://example.com"
}

Response:
{
  "success": true,
  "event": {
    "id": "770e8400-e29b-41d4-a716-446655440000",
    ...
  }
}
```

#### 이벤트 수정
```bash
PUT /api/events/{event_id}

Request:
{
  "title": "수정된 제목",
  "time": "오후 3:00"
}
```

#### 이벤트 삭제
```bash
DELETE /api/events/{event_id}

Response:
{
  "success": true,
  "message": "이벤트가 삭제되었습니다."
}
```

---

## WebSocket 이벤트

### 클라이언트 → 서버

#### 부서 그룹 참여
```javascript
socket.emit('join_department', {
  'department_id': 'department-uuid'
})
```

#### 부서 그룹 나가기
```javascript
socket.emit('leave_department', {
  'department_id': 'department-uuid'
})
```

#### 동기화 요청
```javascript
socket.emit('sync_request', {
  'department_id': 'department-uuid'
})
```

### 서버 → 클라이언트

#### 연결 성공
```javascript
socket.on('connected', (data) => {
  console.log(data.message)
})
```

#### 부서 생성됨
```javascript
socket.on('department_created', (department) => {
  console.log('새 부서:', department)
})
```

#### 이벤트 생성됨
```javascript
socket.on('event_created', (event) => {
  console.log('새 이벤트:', event)
})
```

#### 이벤트 업데이트됨
```javascript
socket.on('event_updated', (event) => {
  console.log('업데이트된 이벤트:', event)
})
```

#### 이벤트 삭제됨
```javascript
socket.on('event_deleted', (data) => {
  console.log('삭제된 이벤트 ID:', data.id)
})
```

---

## 프로젝트 구조

```
backend/
├── app.py                    # Flask 애플리케이션 메인
├── models.py                 # DynamoDB 모델
├── migrate_to_dynamodb.py    # 데이터 마이그레이션 스크립트
├── requirements.txt          # Python 의존성
├── README.md                 # 이 파일
└── venv/                     # 가상환경 (gitignore)
```

---

## 데이터베이스 스키마

### 부서 테이블 (departments)

| 속성 | 타입 | 설명 |
|------|------|------|
| id | String (PK) | 부서 고유 ID (UUID) |
| name | String (GSI) | 부서 이름 |
| description | String | 부서 설명 |
| created_at | String | 생성 시간 |
| updated_at | String | 수정 시간 |

### 이벤트 테이블 (events)

| 속성 | 타입 | 설명 |
|------|------|------|
| id | String (PK) | 이벤트 고유 ID (UUID) |
| department_id | String (SK, GSI) | 부서 ID |
| event_date | String (GSI) | 이벤트 날짜 |
| title | String | 이벤트 제목 |
| description | String | 이벤트 설명 |
| time | String | 이벤트 시간 |
| url | String | 이벤트 URL |
| created_at | String | 생성 시간 |
| last_modified | String | 마지막 수정 시간 |

---

## 문제 해결

### DynamoDB 연결 오류

```
✗ DynamoDB 연결 실패: 자격증명 오류
```

**해결:**
- AWS 자격증명 확인
- 환경 변수 설정 확인
- IAM 권한 확인

### 테이블 존재하지 않음

```
테이블 'departments'가 없습니다.
```

**해결:**
```python
from models import DynamoDBDatabase
db = DynamoDBDatabase()
db.create_tables()
```

### 용량 초과

```
ProvisionedThroughputExceededException
```

**해결:**
- AWS Console에서 테이블 용량 증가
- 또는 온디맨드 모드로 변경

---

## 배포

### AWS Elastic Beanstalk

```bash
eb init -p python-3.11 agent-assistant-backend
eb create production
eb deploy
```

### EC2

1. EC2 인스턴스 생성
2. 필수 소프트웨어 설치 (`python3`, `pip`)
3. 애플리케이션 배포
4. 환경 변수 설정
5. Gunicorn으로 서버 실행

---

## 비용 최적화

- 온디맨드 모드 사용: 사용한 만큼만 지불
- 글로벌 보조 인덱스 용량 조정
- CloudWatch 모니터링으로 사용량 추적

---

## 라이선스

MIT License

---

## 지원

문제나 제안사항은 GitHub 이슈로 등록해주세요.

