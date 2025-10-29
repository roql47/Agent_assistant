# 테스트 환경 빠른 시작 가이드

## 🚀 2분 안에 시작하기

### Windows CMD

```cmd
cd backend

# 1. 환경 설정
set DB_MODE=local
set LOCAL_DATA_DIR=data

# 2. 서버 실행
set FLASK_ENV=development
python -m flask run --host=0.0.0.0 --port=5000
```

### macOS / Linux

```bash
cd backend

# 1. 환경 설정
export DB_MODE=local
export LOCAL_DATA_DIR=data

# 2. 서버 실행
export FLASK_ENV=development
flask run --host=0.0.0.0 --port=5000
```

---

## ✅ 검증

### 서버 실행 확인

```
✓ 로컬 NoSQL 저장소 초기화 완료: data
✓ 로컬 NoSQL 연결 성공
 * Running on http://0.0.0.0:5000
```

### API 테스트

```bash
# 부서 생성
curl -X POST http://localhost:5000/api/departments \
  -H "Content-Type: application/json" \
  -d '{"name":"개발팀","description":"소프트웨어 개발"}'

# 응답 확인
# {"success": true, "department": {...}}
```

---

## 📁 데이터 저장 위치

```
backend/
├── data/
│   ├── departments.json    # 부서 데이터 (자동 생성)
│   └── events.json         # 이벤트 데이터 (자동 생성)
```

JSON 파일을 텍스트 에디터로 열어서 직접 데이터 확인 가능!

---

## 📚 전체 가이드

자세한 가이드는 `LOCAL_TEST_SETUP.md` 참고

---

## 🔄 AWS로 전환

```bash
# 환경 변수 변경
export DB_MODE=aws
export AWS_REGION=ap-northeast-2
export AWS_ACCESS_KEY_ID=your_key
export AWS_SECRET_ACCESS_KEY=your_secret

# 서버 재시작
flask run --host=0.0.0.0 --port=5000
```

자세한 가이드는 `AWS_DEPLOYMENT_CONFIG.md` 또는 `DYNAMODB_SETUP.md` 참고

