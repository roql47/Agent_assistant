# AWS EC2 배포 가이드

이 가이드는 AWS EC2 인스턴스에 Flask 캘린더 동기화 서버를 배포하는 방법을 설명합니다.

## 사전 준비

- AWS 계정 (무료 티어 사용 가능)
- SSH 클라이언트 (Windows: PuTTY 또는 Windows Terminal)

## 1. EC2 인스턴스 생성

### 1.1 AWS Console 접속

1. [AWS Console](https://console.aws.amazon.com/)에 로그인
2. 리전 선택 (예: 서울 - ap-northeast-2)
3. EC2 서비스로 이동

### 1.2 인스턴스 시작

1. **"인스턴스 시작" 버튼 클릭**

2. **이름 및 태그 설정**
   - 이름: `calendar-sync-server`

3. **AMI (Amazon Machine Image) 선택**
   - **Ubuntu Server 22.04 LTS** 선택
   - 64비트 (x86) 아키텍처

4. **인스턴스 유형 선택**
   - **t2.micro** (무료 티어 적용)
   - vCPU: 1개, 메모리: 1GB

5. **키 페어 생성**
   - "새 키 페어 생성" 클릭
   - 키 페어 이름: `calendar-sync-key`
   - 키 페어 유형: RSA
   - 프라이빗 키 파일 형식: `.pem` (Linux/Mac) 또는 `.ppk` (Windows/PuTTY)
   - **다운로드한 키 파일을 안전한 곳에 보관**

6. **네트워크 설정**
   - VPC: 기본값 사용
   - 서브넷: 기본값 사용
   - 퍼블릭 IP 자동 할당: **활성화**
   
   **보안 그룹 설정:**
   - 보안 그룹 이름: `calendar-sync-sg`
   - 규칙 추가:
     - SSH (포트 22): 내 IP에서만 허용 (보안)
     - 사용자 지정 TCP (포트 5000): 0.0.0.0/0 (모든 위치)
     
   ⚠️ 실제 운영 환경에서는 포트 5000을 특정 IP 범위로 제한하는 것이 좋습니다.

7. **스토리지 구성**
   - 8 GB gp3 (무료 티어: 최대 30GB)

8. **인스턴스 시작**

### 1.3 Elastic IP 할당 (고정 IP)

인스턴스를 재시작해도 IP가 바뀌지 않도록 Elastic IP를 할당합니다.

1. EC2 대시보드 → **"탄력적 IP"** 메뉴
2. **"탄력적 IP 주소 할당"** 클릭
3. **"할당"** 클릭
4. 할당된 IP 선택 → **"작업" → "탄력적 IP 주소 연결"**
5. 인스턴스: `calendar-sync-server` 선택
6. **"연결"** 클릭

✅ 이제 고정 IP 주소가 생성되었습니다. 이 IP를 클라이언트에서 사용합니다.

## 2. EC2 인스턴스 접속

### 2.1 Windows (PowerShell 또는 CMD)

```bash
ssh -i "경로\calendar-sync-key.pem" ubuntu@YOUR_ELASTIC_IP
```

처음 접속 시 키 파일 권한 오류가 발생할 수 있습니다:
1. 키 파일 우클릭 → 속성 → 보안 탭
2. "고급" 클릭
3. "상속 사용 안 함" → "이 개체에서 상속된 사용 권한을 모두 제거"
4. "추가" → "보안 주체 선택" → 본인 계정만 추가
5. 모든 권한 허용

### 2.2 Linux/Mac

```bash
chmod 400 calendar-sync-key.pem
ssh -i "calendar-sync-key.pem" ubuntu@YOUR_ELASTIC_IP
```

## 3. 서버 환경 설정

SSH로 접속한 후 다음 명령어들을 실행합니다.

### 3.1 시스템 업데이트

```bash
sudo apt update
sudo apt upgrade -y
```

### 3.2 Python 설치

```bash
sudo apt install -y python3 python3-pip python3-venv
python3 --version  # 버전 확인
```

### 3.3 Git 설치 (선택사항)

```bash
sudo apt install -y git
```

## 4. 애플리케이션 배포

### 4.1 프로젝트 디렉토리 생성

```bash
mkdir ~/calendar-sync-server
cd ~/calendar-sync-server
```

### 4.2 파일 업로드

**방법 1: SCP로 파일 전송 (로컬 PC에서 실행)**

```bash
scp -i "calendar-sync-key.pem" app.py ubuntu@YOUR_ELASTIC_IP:~/calendar-sync-server/
scp -i "calendar-sync-key.pem" models.py ubuntu@YOUR_ELASTIC_IP:~/calendar-sync-server/
scp -i "calendar-sync-key.pem" requirements.txt ubuntu@YOUR_ELASTIC_IP:~/calendar-sync-server/
```

**방법 2: Git으로 클론 (저장소가 있는 경우)**

```bash
git clone YOUR_REPOSITORY_URL ~/calendar-sync-server
cd ~/calendar-sync-server/backend
```

**방법 3: 직접 파일 생성**

```bash
# app.py, models.py, requirements.txt를 vim 또는 nano로 생성
nano app.py
# 내용 붙여넣기 후 Ctrl+X, Y, Enter로 저장
```

### 4.3 가상 환경 설정

```bash
cd ~/calendar-sync-server
python3 -m venv venv
source venv/bin/activate
```

### 4.4 의존성 설치

```bash
pip install -r requirements.txt
```

### 4.5 테스트 실행

```bash
python app.py
```

브라우저에서 `http://YOUR_ELASTIC_IP:5000`에 접속하여 확인합니다.
Ctrl+C로 종료합니다.

## 5. Systemd 서비스로 자동 시작 설정

애플리케이션이 서버 재시작 후에도 자동으로 실행되도록 설정합니다.

### 5.1 서비스 파일 생성

```bash
sudo nano /etc/systemd/system/calendar-sync.service
```

다음 내용을 입력합니다:

```ini
[Unit]
Description=Calendar Sync Server
After=network.target

[Service]
Type=simple
User=ubuntu
WorkingDirectory=/home/ubuntu/calendar-sync-server
Environment="PATH=/home/ubuntu/calendar-sync-server/venv/bin"
ExecStart=/home/ubuntu/calendar-sync-server/venv/bin/python app.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Ctrl+X, Y, Enter로 저장합니다.

### 5.2 서비스 활성화 및 시작

```bash
sudo systemctl daemon-reload
sudo systemctl enable calendar-sync
sudo systemctl start calendar-sync
```

### 5.3 서비스 상태 확인

```bash
sudo systemctl status calendar-sync
```

정상적으로 실행 중이면 **"active (running)"** 상태가 표시됩니다.

### 5.4 로그 확인

```bash
sudo journalctl -u calendar-sync -f
```

Ctrl+C로 로그 모니터링을 종료합니다.

## 6. 방화벽 설정 (선택사항)

Ubuntu의 방화벽을 사용하는 경우:

```bash
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 5000/tcp  # Flask 서버
sudo ufw enable
sudo ufw status
```

⚠️ SSH 포트(22)를 먼저 허용하지 않으면 접속이 끊길 수 있습니다!

## 7. 클라이언트 설정

C# 클라이언트의 설정 창에서:
- 서버 URL: `http://YOUR_ELASTIC_IP:5000`

## 8. 모니터링 및 관리

### 서비스 재시작

```bash
sudo systemctl restart calendar-sync
```

### 서비스 중지

```bash
sudo systemctl stop calendar-sync
```

### 로그 확인

```bash
sudo journalctl -u calendar-sync -n 100
```

### 데이터베이스 백업

```bash
cd ~/calendar-sync-server
cp calendar_sync.db calendar_sync.db.backup
```

### 데이터베이스 복구

```bash
cd ~/calendar-sync-server
cp calendar_sync.db.backup calendar_sync.db
sudo systemctl restart calendar-sync
```

## 9. HTTPS 설정 (선택사항)

보안을 강화하려면 Nginx + Let's Encrypt로 HTTPS를 설정할 수 있습니다.

### 9.1 Nginx 설치

```bash
sudo apt install -y nginx
```

### 9.2 Nginx 설정

```bash
sudo nano /etc/nginx/sites-available/calendar-sync
```

다음 내용 입력:

```nginx
server {
    listen 80;
    server_name YOUR_DOMAIN_OR_IP;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 9.3 Nginx 활성화

```bash
sudo ln -s /etc/nginx/sites-available/calendar-sync /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

### 9.4 포트 변경

이제 80번 포트로 접속할 수 있습니다:
- 클라이언트 URL: `http://YOUR_ELASTIC_IP`

보안 그룹에서 포트 80을 추가로 개방해야 합니다.

## 10. 비용 최적화

### 무료 티어 한도

- EC2 t2.micro: 월 750시간 (24시간 운영 가능)
- 데이터 전송: 월 15GB 무료

### 비용 모니터링

AWS Billing 대시보드에서 현재 사용량을 확인하세요:
1. AWS Console → "결제 및 비용 관리"
2. 예산 알림 설정 권장

## 11. 문제 해결

### 서버에 접속할 수 없는 경우

1. **보안 그룹 확인**: 포트 5000이 열려있는지 확인
2. **서비스 상태 확인**: `sudo systemctl status calendar-sync`
3. **방화벽 확인**: `sudo ufw status`
4. **로그 확인**: `sudo journalctl -u calendar-sync -n 50`

### 메모리 부족 오류

t2.micro는 1GB 메모리만 제공하므로, 많은 사용자가 동시 접속하면 부족할 수 있습니다.
- 스왑 메모리 추가 또는
- 더 큰 인스턴스 유형으로 업그레이드 (t2.small 등)

### 연결이 끊기는 경우

- Elastic IP가 인스턴스에 제대로 연결되어 있는지 확인
- 서비스가 재시작되었는지 확인: `sudo systemctl restart calendar-sync`

## 12. 보안 권장사항

✅ SSH 키를 안전하게 보관하세요
✅ 정기적으로 시스템 업데이트: `sudo apt update && sudo apt upgrade`
✅ 불필요한 포트는 닫으세요
✅ 보안 그룹에서 IP 제한을 고려하세요
✅ 데이터베이스를 정기적으로 백업하세요

## 연락처

문제가 발생하면 로그를 확인하고, 필요시 이슈를 등록해주세요.

