# AWS DynamoDB 배포 설정 가이드

## 개요
이 프로젝트는 부서 관리 및 캘린더 이벤트를 **AWS DynamoDB**의 NoSQL 문서 데이터베이스에서 관리합니다.

---

## 1. AWS 환경 설정

### 1.1 필수 요구사항
- AWS 계정
- AWS CLI 설치 및 구성
- IAM 사용자 (AccessKey & SecretKey)

### 1.2 IAM 정책 설정

DynamoDB 접근 권한을 가진 IAM 사용자를 생성하고 다음 정책을 첨부합니다:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:CreateTable",
        "dynamodb:DeleteTable",
        "dynamodb:DescribeTable",
        "dynamodb:ListTables",
        "dynamodb:GetItem",
        "dynamodb:PutItem",
        "dynamodb:UpdateItem",
        "dynamodb:DeleteItem",
        "dynamodb:Query",
        "dynamodb:Scan",
        "dynamodb:BatchGetItem",
        "dynamodb:BatchWriteItem"
      ],
      "Resource": "arn:aws:dynamodb:ap-northeast-2:*:table/departments*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:CreateTable",
        "dynamodb:DeleteTable",
        "dynamodb:DescribeTable",
        "dynamodb:ListTables",
        "dynamodb:GetItem",
        "dynamodb:PutItem",
        "dynamodb:UpdateItem",
        "dynamodb:DeleteItem",
        "dynamodb:Query",
        "dynamodb:Scan",
        "dynamodb:BatchGetItem",
        "dynamodb:BatchWriteItem"
      ],
      "Resource": "arn:aws:dynamodb:ap-northeast-2:*:table/events*"
    }
  ]
}
```

---

## 2. DynamoDB 테이블 설정

### 2.1 테이블 스키마

#### **부서 테이블 (departments)**
```
Partition Key: id (String)
Global Secondary Index (name-index):
  - Partition Key: name (String)
  - Read: 5 RCU, Write: 5 WCU
Throughput: 10 RCU, 10 WCU
```

**문서 구조 예:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "개발팀",
  "description": "소프트웨어 개발",
  "created_at": "2024-01-15T10:30:00.000Z",
  "updated_at": "2024-01-15T10:30:00.000Z"
}
```

#### **이벤트 테이블 (events)**
```
Partition Key: id (String)
Sort Key: department_id (String)
Global Secondary Index (department-date-index):
  - Partition Key: department_id (String)
  - Sort Key: event_date (String)
  - Read: 10 RCU, Write: 10 WCU
Throughput: 10 RCU, 10 WCU
```

**문서 구조 예:**
```json
{
  "id": "660e8400-e29b-41d4-a716-446655440000",
  "department_id": "550e8400-e29b-41d4-a716-446655440000",
  "event_date": "2024-01-20",
  "title": "팀 회의",
  "description": "주간 팀 회의",
  "time": "오후 2:00",
  "url": "https://example.com/meeting",
  "created_at": "2024-01-15T10:30:00.000Z",
  "last_modified": "2024-01-15T10:30:00.000Z"
}
```

### 2.2 AWS Console에서 테이블 생성 (수동)

1. AWS Console → DynamoDB 서비스
2. "테이블 생성" 클릭
3. 위의 스키마 참고하여 테이블 생성

### 2.3 AWS CLI로 테이블 생성 (자동)

```bash
# 부서 테이블 생성
aws dynamodb create-table \
  --table-name departments \
  --attribute-definitions \
    AttributeName=id,AttributeType=S \
    AttributeName=name,AttributeType=S \
  --key-schema \
    AttributeName=id,KeyType=HASH \
  --global-secondary-indexes \
    "IndexName=name-index,KeySchema=[{AttributeName=name,KeyType=HASH}],Projection={ProjectionType=ALL},ProvisionedThroughput={ReadCapacityUnits=5,WriteCapacityUnits=5}" \
  --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10 \
  --region ap-northeast-2

# 이벤트 테이블 생성
aws dynamodb create-table \
  --table-name events \
  --attribute-definitions \
    AttributeName=id,AttributeType=S \
    AttributeName=department_id,AttributeType=S \
    AttributeName=event_date,AttributeType=S \
  --key-schema \
    AttributeName=id,KeyType=HASH \
    AttributeName=department_id,KeyType=RANGE \
  --global-secondary-indexes \
    "IndexName=department-date-index,KeySchema=[{AttributeName=department_id,KeyType=HASH},{AttributeName=event_date,KeyType=RANGE}],Projection={ProjectionType=ALL},ProvisionedThroughput={ReadCapacityUnits=10,WriteCapacityUnits=10}" \
  --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10 \
  --region ap-northeast-2
```

---

## 3. 환경 변수 설정

### 3.1 로컬 개발 환경 (.env 파일)

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
SECRET_KEY=your-secret-key-change-in-production
```

### 3.2 EC2/Elastic Beanstalk 환경 변수

AWS Console에서 환경 변수 설정:
1. Elastic Beanstalk → 환경 설정
2. 소프트웨어 → 환경 속성
3. 위의 변수들 입력

---

## 4. 애플리케이션 배포

### 4.1 Python 패키지 설치

```bash
cd backend
pip install -r requirements.txt
```

### 4.2 테이블 초기화 (선택사항)

```python
from models import DynamoDBDatabase

db = DynamoDBDatabase()
db.create_tables()
```

### 4.3 Flask 서버 실행

```bash
export FLASK_ENV=development
flask run --host=0.0.0.0 --port=5000
```

---

## 5. 데이터 마이그레이션

### 5.1 기존 JSON 데이터 → DynamoDB 마이그레이션

`migrate_to_dynamodb.py` 스크립트를 사용하여 기존 데이터를 마이그레이션합니다.

```bash
python backend/migrate_to_dynamodb.py
```

---

## 6. 비용 최적화

### 6.1 온디맨드 모드 (권장 - 소규모)

DynamoDB 테이블을 "온디맨드" 모드로 설정하여 사용한 만큼만 지불:
- 기본값: 프로비저닝 모드 (고정 비용)
- 변경: AWS Console → 테이블 설정 → 계산된 처리량

### 6.2 글로벌 보조 인덱스 (GSI) 최적화

현재 GSI 용량:
- `name-index`: 5 RCU/5 WCU
- `department-date-index`: 10 RCU/10 WCU

필요시 AWS Console에서 조정 가능.

---

## 7. 모니터링 및 로깅

### 7.1 CloudWatch 로그

Flask 애플리케이션 로그는 자동으로 CloudWatch에 기록됩니다.

### 7.2 DynamoDB 메트릭

AWS Console → CloudWatch → 메트릭에서 모니터링:
- ConsumedReadCapacityUnits
- ConsumedWriteCapacityUnits
- UserErrors
- ProvisionedThroughputExceeded

---

## 8. 보안 설정

### 8.1 암호화

- **전송 중 암호화**: TLS/SSL (자동)
- **저장 시 암호화**: AWS 관리형 키 (기본)

### 8.2 액세스 제어

- IAM 정책으로 접근 제한
- 리소스 기반 정책으로 세분화된 권한 설정

### 8.3 백업 및 복구

DynamoDB 자동 백업:
- AWS Console → 테이블 설정 → 백업
- 포인트 인타임 복구 (PITR) 활성화

---

## 9. 문제 해결

### 9.1 연결 오류

```
error: Failed to connect to DynamoDB
해결: AWS 자격증명 확인, 리전 설정 확인
```

### 9.2 용량 초과

```
error: ProvisionedThroughputExceededException
해결: 프로비저닝된 용량 증가 또는 온디맨드 모드 전환
```

### 9.3 테이블 생성 오류

```
error: ResourceInUseException
해결: 테이블이 이미 존재합니다. 기존 테이블 사용.
```

---

## 10. 참고 자료

- [AWS DynamoDB 문서](https://docs.aws.amazon.com/dynamodb/)
- [boto3 DynamoDB 가이드](https://boto3.amazonaws.com/v1/documentation/api/latest/reference/services/dynamodb.html)
- [DynamoDB 가격 계산기](https://aws.amazon.com/ko/dynamodb/pricing/)

