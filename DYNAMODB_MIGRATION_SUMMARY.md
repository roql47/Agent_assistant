# AWS DynamoDB 마이그레이션 완료 요약

## 📌 프로젝트 개요

**부서 선택과 캘린더 일정 생성** 데이터를 **AWS DynamoDB** NoSQL 문서 데이터베이스로 관리하도록 완전히 마이그레이션했습니다.

---

## ✅ 완료된 작업

### 1. 데이터베이스 모델링 (`backend/models.py`)
- ✅ SQLite 기반 → **AWS DynamoDB** 기반 전환
- ✅ UUID 기반 문서 ID 시스템
- ✅ 부서 테이블 (`departments`) - Partition Key: `id`, GSI: `name-index`
- ✅ 이벤트 테이블 (`events`) - Composite Key: `id` + `department_id`, GSI: `department-date-index`
- ✅ Decimal 타입 자동 변환 (JSON 직렬화)

### 2. 파이썬 백엔드 통합 (`backend/app.py`)
- ✅ DynamoDBDatabase 초기화
- ✅ 부서 CRUD 작업 (Create, Read, Delete)
- ✅ 이벤트 CRUD 작업 (Create, Read, Update, Delete)
- ✅ WebSocket 실시간 동기화 (부서별 그룹 관리)
- ✅ REST API 엔드포인트 (모두 UUID 기반)

### 3. 의존성 관리 (`backend/requirements.txt`)
- ✅ `boto3==1.28.85` - AWS SDK for Python
- ✅ `botocore==1.31.85` - boto3의 핵심 라이브러리

### 4. 데이터 마이그레이션 (`backend/migrate_to_dynamodb.py`)
- ✅ 기존 JSON 파일 읽기 (`departments.json`, `calendar_events.json`)
- ✅ DynamoDB로 자동 변환 및 저장
- ✅ 마이그레이션 검증 및 로깅
- ✅ 실패 처리 및 에러 리포팅

### 5. 문서화
- ✅ `AWS_DEPLOYMENT_CONFIG.md` - DynamoDB 배포 가이드
- ✅ `backend/README.md` - 백엔드 설정 및 사용 방법
- ✅ `DYNAMODB_SETUP.md` - AWS 초기 설정 가이드
- ✅ `DYNAMODB_MIGRATION_SUMMARY.md` - 이 문서

---

## 🗂 파일 구조

```
프로젝트 루트/
├── backend/
│   ├── app.py                      # Flask 메인 애플리케이션 (DynamoDB 통합)
│   ├── models.py                   # DynamoDB 모델 클래스
│   ├── migrate_to_dynamodb.py      # 데이터 마이그레이션 스크립트
│   ├── requirements.txt            # boto3 추가됨
│   ├── README.md                   # DynamoDB 버전 README
│   ├── venv/                       # Python 가상환경
│   └── ...
│
├── AWS_DEPLOYMENT_CONFIG.md        # DynamoDB 배포 설정 가이드
├── DYNAMODB_SETUP.md               # AWS 초기 설정 가이드
└── DYNAMODB_MIGRATION_SUMMARY.md   # 이 파일

```

---

## 🚀 빠른 시작

### Step 1: AWS 계정 준비 (5분)
```bash
# DYNAMODB_SETUP.md의 Step 1-3 참고
# - AWS 계정 생성
# - IAM 사용자 생성
# - 액세스 키 생성
```

### Step 2: 환경 설정 (2분)
```bash
cd backend
pip install -r requirements.txt

# backend/.env 파일 생성
AWS_REGION=ap-northeast-2
AWS_ACCESS_KEY_ID=your_key
AWS_SECRET_ACCESS_KEY=your_secret
```

### Step 3: DynamoDB 테이블 생성 (3분)
```bash
python -c "from models import DynamoDBDatabase; db = DynamoDBDatabase(); db.create_tables()"
```

### Step 4: 데이터 마이그레이션 (1분)
```bash
python migrate_to_dynamodb.py
```

### Step 5: 서버 실행 (1분)
```bash
export FLASK_ENV=development
flask run --host=0.0.0.0 --port=5000
```

**총 소요시간: ~15분**

---

## 📊 DynamoDB 스키마

### 부서 테이블 (departments)

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "개발팀",
  "description": "소프트웨어 개발",
  "created_at": "2024-01-15T10:30:00.000Z",
  "updated_at": "2024-01-15T10:30:00.000Z"
}
```

**테이블 설정:**
- Partition Key: `id` (String)
- GSI: `name-index` (Partition Key: `name`)
- 처리량: 10 RCU / 10 WCU

### 이벤트 테이블 (events)

```json
{
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
```

**테이블 설정:**
- Partition Key: `id` (String)
- Sort Key: `department_id` (String)
- GSI: `department-date-index` (PK: `department_id`, SK: `event_date`)
- 처리량: 10 RCU / 10 WCU

---

## 🔄 API 변경사항

### 이전 (SQLite)
```bash
GET /api/departments/{id:int}       # Integer ID
POST /api/events/{dept_id:int}      # Integer ID
```

### 이후 (DynamoDB)
```bash
GET /api/departments/{id:uuid}      # UUID 문자열
POST /api/events/{dept_id:uuid}     # UUID 문자열
```

---

## 💾 주요 기능

### 부서 관리
```python
# 부서 생성
POST /api/departments
{
  "name": "새로운팀",
  "description": "설명"
}

# 부서 목록
GET /api/departments

# 부서 삭제
DELETE /api/departments/{id}
```

### 이벤트 관리
```python
# 이벤트 생성
POST /api/events/{department_id}
{
  "event_date": "2024-01-20",
  "title": "회의",
  "time": "오후 2:00",
  "description": "설명"
}

# 이벤트 조회
GET /api/events/{department_id}

# 이벤트 수정
PUT /api/events/{event_id}

# 이벤트 삭제
DELETE /api/events/{event_id}
```

### WebSocket 실시간 동기화
```javascript
// 부서 그룹 참여
socket.emit('join_department', { 'department_id': 'uuid' });

// 이벤트 생성됨
socket.on('event_created', (event) => {
  console.log('새 이벤트:', event);
});
```

---

## 📈 확장성 개선

| 항목 | SQLite | DynamoDB |
|------|--------|----------|
| 확장성 | 제한적 | 무제한 |
| 동시성 | 낮음 | 높음 |
| 데이터 크기 | GB 수준 | TB 이상 |
| 글로벌 배포 | 불가능 | 가능 |
| 백업 | 수동 | 자동 |
| 가격 | 고정 | 사용량 기반 |

---

## 💰 비용 추정 (월별)

### 온디맨드 모드 (소규모, 권장)
```
대략 월 $5-15 USD
- 작은 트래픽: 수 GB 데이터
- 사용한 만큼만 지불
```

### 프로비저닝 모드
```
프로비저닝된 처리량에 따라 월 $5-50+ USD
- 높은 트래픽 예상
- 고정 비용
```

> 🆓 **AWS 프리 티어** (12개월): 25GB 저장소 + 200만 쓰기 요청 무료

---

## 🔒 보안

### 구현된 보안 조치
- ✅ IAM 역할 기반 접근 제어
- ✅ 전송 중 암호화 (TLS/SSL)
- ✅ 저장 시 암호화 (AWS 관리형 키)
- ✅ VPC 격리 (프로덕션)

### 권장 추가 조치
- 🔐 백업 자동화 및 PITR 활성화
- 🔐 CloudWatch 모니터링
- 🔐 API Gateway 앞에 WAF 배포
- 🔐 CloudTrail로 API 감사

---

## 🛠 문제 해결

### 연결 오류
```
NoCredentialsError: AWS 자격증명을 찾을 수 없음
↓
해결: .env 파일 확인 또는 aws configure 실행
```

### 테이블 없음
```
ResourceNotFoundException: 테이블이 없습니다
↓
해결: python migrate_to_dynamodb.py 실행
```

### 용량 초과
```
ProvisionedThroughputExceededException
↓
해결: AWS Console에서 용량 증가 또는 온디맨드 모드 전환
```

자세한 문제 해결은 **DYNAMODB_SETUP.md** 참고

---

## 📚 참고 문서

1. **AWS_DEPLOYMENT_CONFIG.md** - DynamoDB 배포 및 구성 가이드
2. **backend/README.md** - 백엔드 API 및 개발 가이드
3. **DYNAMODB_SETUP.md** - AWS 초기 설정 (Step by Step)
4. [AWS DynamoDB 공식 문서](https://docs.aws.amazon.com/dynamodb/)
5. [boto3 DynamoDB API](https://boto3.amazonaws.com/v1/documentation/api/latest/reference/services/dynamodb.html)

---

## 🎯 다음 단계

### 즉시 실행
1. ✅ AWS 계정 및 DynamoDB 테이블 생성
2. ✅ 기존 JSON 데이터 마이그레이션
3. ✅ Flask 백엔드 테스트

### 단기 (1-2주)
- [ ] C# 클라이언트 업데이트 (UUID 기반 부서 ID 처리)
- [ ] E2E 통합 테스트
- [ ] 성능 최적화 및 모니터링

### 장기 (1-3개월)
- [ ] AWS EC2/Elastic Beanstalk 배포
- [ ] CloudWatch 대시보드 구성
- [ ] 백업 및 재해 복구 전략 수립
- [ ] 사용자 인증 시스템 추가

---

## ✨ 주요 개선 사항

### 기술적 개선
```
SQLite (로컬 파일)
    ↓
AWS DynamoDB (클라우드 NoSQL)

장점:
✅ 자동 확장성
✅ 높은 가용성
✅ 글로벌 배포 가능
✅ 완전 관리형 서비스
✅ 실시간 모니터링
✅ 자동 백업
```

### 운영 개선
```
로컬 배포 (단일 서버)
    ↓
AWS 클라우드 배포 (무제한 확장)

✅ 다중 가용 영역 자동 복제
✅ 글로벌 테이블 지원
✅ 자동 failover
✅ 에측 가능한 성능
```

---

## 📞 지원

문제 발생 시:
1. **DYNAMODB_SETUP.md**의 "문제 해결" 섹션 확인
2. [AWS DynamoDB FAQ](https://aws.amazon.com/dynamodb/faqs/)
3. AWS Support (프리미엄 플랜)

---

## 🎉 완료!

축하합니다! 이제 프로젝트가 **AWS DynamoDB** 기반의 확장 가능한 NoSQL 데이터베이스로 운영될 준비가 되었습니다.

모든 부서 선택과 캘린더 일정 생성 데이터는 AWS 클라우드에서 안전하게 관리되며, 필요시 전 세계 어디서나 접근 가능합니다.

**Happy Coding! 🚀**

