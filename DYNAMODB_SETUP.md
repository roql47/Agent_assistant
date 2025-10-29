# AWS DynamoDB 초기 설정 가이드

## 개요

이 문서는 AWS DynamoDB를 처음으로 설정하고 프로젝트에 통합하는 방법을 설명합니다.

---

## Step 1: AWS 계정 준비

### 1.1 AWS 계정 생성
- [AWS 콘솔](https://aws.amazon.com/)에서 계정 생성
- 신용카드 정보 등록

### 1.2 IAM 사용자 생성

1. AWS 콘솔 → **IAM** 서비스
2. 왼쪽 메뉴 → **사용자** → **사용자 생성**
3. 사용자 이름 입력 (예: `agent-assistant-dynamodb`)
4. **다음** 클릭

### 1.3 권한 설정

1. **권한 정책 검토 및 생성** 선택
2. **DynamoDB 정책 생성**:
   - 정책 생성 → JSON 편집기
   - 아래 정책 복사:

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
        "dynamodb:BatchWriteItem",
        "dynamodb:DescribeStream",
        "dynamodb:GetRecords",
        "dynamodb:GetShardIterator",
        "dynamodb:ListStreams"
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
        "dynamodb:BatchWriteItem",
        "dynamodb:DescribeStream",
        "dynamodb:GetRecords",
        "dynamodb:GetShardIterator",
        "dynamodb:ListStreams"
      ],
      "Resource": "arn:aws:dynamodb:ap-northeast-2:*:table/events*"
    }
  ]
}
```

### 1.4 액세스 키 생성

1. 사용자 생성 완료 후, 사용자 세부 정보 페이지 이동
2. **보안 자격증명** 탭
3. **액세스 키 생성**
4. **액세스 키** (Access Key ID) 및 **보안 액세스 키** (Secret Access Key) 복사
   - ⚠️ 보안 액세스 키는 이곳에서만 한 번만 확인 가능!

---

## Step 2: AWS CLI 설정

### 2.1 AWS CLI 설치

```bash
# Windows (Chocolatey 사용)
choco install awscli

# macOS
brew install awscli

# Linux
sudo apt-get install awscli
```

### 2.2 AWS 자격증명 설정

```bash
aws configure

# 프롬프트 입력:
# AWS Access Key ID: your_access_key_id
# AWS Secret Access Key: your_secret_access_key
# Default region name: ap-northeast-2
# Default output format: json
```

### 2.3 설정 확인

```bash
aws dynamodb list-tables --region ap-northeast-2
```

---

## Step 3: 환경 변수 설정

### 3.1 프로젝트 디렉토리에서 `.env` 파일 생성

**backend/.env:**

```bash
# AWS 설정
AWS_REGION=ap-northeast-2
AWS_ACCESS_KEY_ID=your_access_key_id_here
AWS_SECRET_ACCESS_KEY=your_secret_access_key_here

# DynamoDB 테이블 이름
DEPARTMENTS_TABLE=departments
EVENTS_TABLE=events

# Flask 설정
FLASK_ENV=development
SECRET_KEY=your-secret-key-change-in-production
```

### 3.2 환경 변수 로드 (선택사항 - Python 스크립트)

```bash
pip install python-dotenv
```

---

## Step 4: DynamoDB 테이블 생성

### 방법 1: Python으로 생성 (권장)

```bash
cd backend
python
```

```python
from models import DynamoDBDatabase

db = DynamoDBDatabase()
db.create_tables()
```

**출력:**
```
✓ departments 테이블이 생성되었습니다.
✓ events 테이블이 생성되었습니다.
```

### 방법 2: AWS CLI로 생성

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
    '[
      {
        "IndexName": "name-index",
        "KeySchema": [
          {"AttributeName": "name", "KeyType": "HASH"}
        ],
        "Projection": {"ProjectionType": "ALL"},
        "ProvisionedThroughput": {
          "ReadCapacityUnits": 5,
          "WriteCapacityUnits": 5
        }
      }
    ]' \
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
    '[
      {
        "IndexName": "department-date-index",
        "KeySchema": [
          {"AttributeName": "department_id", "KeyType": "HASH"},
          {"AttributeName": "event_date", "KeyType": "RANGE"}
        ],
        "Projection": {"ProjectionType": "ALL"},
        "ProvisionedThroughput": {
          "ReadCapacityUnits": 10,
          "WriteCapacityUnits": 10
        }
      }
    ]' \
  --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10 \
  --region ap-northeast-2
```

### 방법 3: AWS Console 수동 생성

1. AWS 콘솔 → **DynamoDB** 서비스
2. **테이블 생성** 클릭
3. 테이블 이름: `departments`
4. 파티션 키: `id` (문자열)
5. 글로벌 보조 인덱스 추가:
   - 인덱스 이름: `name-index`
   - 파티션 키: `name`
6. **생성** 클릭
7. 동일 방식으로 `events` 테이블 생성

---

## Step 5: 데이터 마이그레이션

기존 JSON 파일의 데이터를 DynamoDB로 이동합니다:

```bash
cd backend
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

## Step 6: 백엔드 서버 시작

### 개발 모드

```bash
cd backend
export FLASK_ENV=development
flask run --host=0.0.0.0 --port=5000
```

### 프로덕션 모드

```bash
cd backend
gunicorn --worker-class eventlet -w 1 app:app --bind 0.0.0.0:8000
```

---

## Step 7: 테스트

### API 테스트 (cURL)

```bash
# 부서 생성
curl -X POST http://localhost:5000/api/departments \
  -H "Content-Type: application/json" \
  -d '{"name":"QA팀","description":"품질 보증"}'

# 부서 목록 조회
curl http://localhost:5000/api/departments

# 이벤트 생성 (첫 부서 ID로 테스트)
curl -X POST http://localhost:5000/api/events/550e8400-e29b-41d4-a716-446655440000 \
  -H "Content-Type: application/json" \
  -d '{
    "event_date":"2024-01-25",
    "title":"테스트 이벤트",
    "time":"오후 3:00"
  }'

# 이벤트 조회
curl http://localhost:5000/api/events/550e8400-e29b-41d4-a716-446655440000
```

---

## 문제 해결

### 1. `NoCredentialsError`

**증상:**
```
botocore.exceptions.NoCredentialsError: Unable to locate credentials
```

**해결:**
```bash
# AWS CLI 설정 확인
aws configure list

# 또는 .env 파일의 자격증명 확인
cat backend/.env | grep AWS
```

### 2. `ResourceNotFoundException`

**증상:**
```
botocore.exceptions.ClientError: An error occurred (ResourceNotFoundException) when calling the ...
```

**해결:**
- 테이블이 존재하는지 확인
- 테이블 이름이 올바른지 확인
- 영역(Region) 설정이 올바른지 확인

```bash
aws dynamodb list-tables --region ap-northeast-2
```

### 3. `ProvisionedThroughputExceededException`

**증상:**
```
You exceeded your maximum allowed provisioned throughput for this table
```

**해결:**
- AWS Console → DynamoDB → 테이블 설정
- 용량 증가 또는 온디맨드 모드 변경

### 4. 테이블 생성 실패

**증상:**
```
ResourceInUseException: An error occurred (ResourceInUseException)
```

**해결:**
- 테이블이 이미 존재합니다
- 기존 테이블을 사용하거나 삭제 후 재생성

```bash
# 테이블 삭제
aws dynamodb delete-table --table-name departments --region ap-northeast-2
```

---

## 다음 단계

1. ✅ C# 클라이언트 설정 (선택사항)
2. ✅ AWS EC2 또는 Elastic Beanstalk 배포
3. ✅ CloudWatch 모니터링 설정
4. ✅ 백업 및 복구 정책 설정

---

## 참고 자료

- [AWS DynamoDB 공식 문서](https://docs.aws.amazon.com/dynamodb/)
- [boto3 DynamoDB 가이드](https://boto3.amazonaws.com/v1/documentation/api/latest/reference/services/dynamodb.html)
- [DynamoDB 가격 계산기](https://aws.amazon.com/dynamodb/pricing/)
- [AWS IAM 모범 사례](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html)


