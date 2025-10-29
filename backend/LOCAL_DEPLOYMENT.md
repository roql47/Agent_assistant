# 로컬/내부망 서버 배포 가이드

이 가이드는 로컬 PC나 내부 네트워크 서버에 Flask 캘린더 동기화 서버를 배포하는 방법을 설명합니다.

## 1. Windows 서버 배포

### 1.1 사전 준비

- Windows 10/11 또는 Windows Server
- Python 3.10 이상
- 관리자 권한

### 1.2 Python 설치

1. [Python 공식 웹사이트](https://www.python.org/downloads/)에서 다운로드
2. 설치 시 **"Add Python to PATH"** 체크
3. 설치 완료 후 CMD에서 확인:

```cmd
python --version
```

### 1.3 프로젝트 설정

```cmd
cd C:\
mkdir calendar-sync-server
cd calendar-sync-server
```

프로젝트 파일들(`app.py`, `models.py`, `requirements.txt`)을 이 폴더에 복사합니다.

### 1.4 가상 환경 설정

```cmd
python -m venv venv
venv\Scripts\activate
```

### 1.5 의존성 설치

```cmd
pip install -r requirements.txt
```

### 1.6 서버 실행

```cmd
python app.py
```

서버가 `http://0.0.0.0:5000`에서 실행됩니다.

### 1.7 방화벽 설정

Windows 방화벽에서 포트 5000을 허용해야 다른 PC에서 접속할 수 있습니다.

**방법 1: 명령어로 설정 (관리자 권한 필요)**

```cmd
netsh advfirewall firewall add rule name="Calendar Sync Server" dir=in action=allow protocol=TCP localport=5000
```

**방법 2: GUI로 설정**

1. "Windows Defender 방화벽" 열기
2. "고급 설정" 클릭
3. "인바운드 규칙" 선택
4. "새 규칙" 클릭
5. 규칙 종류: **포트**
6. 프로토콜 및 포트: **TCP, 특정 로컬 포트 5000**
7. 작업: **연결 허용**
8. 프로필: **모두 선택**
9. 이름: **Calendar Sync Server**

### 1.8 Windows 서비스로 등록 (자동 시작)

서버가 재시작되어도 자동으로 실행되도록 Windows 서비스로 등록할 수 있습니다.

**NSSM (Non-Sucking Service Manager) 사용:**

1. [NSSM 다운로드](https://nssm.cc/download)
2. 압축 해제 후 `nssm.exe`를 `C:\calendar-sync-server`에 복사
3. 관리자 권한으로 CMD 실행:

```cmd
cd C:\calendar-sync-server
nssm install CalendarSyncServer
```

4. NSSM GUI가 열리면:
   - **Path**: `C:\calendar-sync-server\venv\Scripts\python.exe`
   - **Startup directory**: `C:\calendar-sync-server`
   - **Arguments**: `app.py`

5. 서비스 시작:

```cmd
nssm start CalendarSyncServer
```

6. 서비스 상태 확인:

```cmd
nssm status CalendarSyncServer
```

**서비스 관리 명령어:**

```cmd
nssm stop CalendarSyncServer      # 중지
nssm restart CalendarSyncServer   # 재시작
nssm remove CalendarSyncServer    # 제거
```

### 1.9 IP 주소 확인

다른 PC에서 접속하려면 서버 PC의 IP 주소가 필요합니다.

```cmd
ipconfig
```

`IPv4 주소` 항목을 확인하세요 (예: 192.168.1.100)

클라이언트에서 사용할 URL: `http://192.168.1.100:5000`

---

## 2. Linux 서버 배포

### 2.1 사전 준비

- Ubuntu 20.04/22.04 또는 CentOS 7/8
- Python 3.10 이상
- sudo 권한

### 2.2 Python 설치

**Ubuntu/Debian:**

```bash
sudo apt update
sudo apt install -y python3 python3-pip python3-venv
python3 --version
```

**CentOS/RHEL:**

```bash
sudo yum update
sudo yum install -y python3 python3-pip
python3 --version
```

### 2.3 프로젝트 설정

```bash
mkdir ~/calendar-sync-server
cd ~/calendar-sync-server
```

프로젝트 파일들을 이 디렉토리에 복사합니다.

### 2.4 가상 환경 설정

```bash
python3 -m venv venv
source venv/bin/activate
```

### 2.5 의존성 설치

```bash
pip install -r requirements.txt
```

### 2.6 서버 실행

```bash
python app.py
```

### 2.7 방화벽 설정

**Ubuntu (ufw):**

```bash
sudo ufw allow 5000/tcp
sudo ufw enable
sudo ufw status
```

**CentOS (firewalld):**

```bash
sudo firewall-cmd --permanent --add-port=5000/tcp
sudo firewall-cmd --reload
```

### 2.8 Systemd 서비스로 등록

```bash
sudo nano /etc/systemd/system/calendar-sync.service
```

다음 내용 입력 (사용자명과 경로를 실제 환경에 맞게 수정):

```ini
[Unit]
Description=Calendar Sync Server
After=network.target

[Service]
Type=simple
User=YOUR_USERNAME
WorkingDirectory=/home/YOUR_USERNAME/calendar-sync-server
Environment="PATH=/home/YOUR_USERNAME/calendar-sync-server/venv/bin"
ExecStart=/home/YOUR_USERNAME/calendar-sync-server/venv/bin/python app.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

서비스 활성화 및 시작:

```bash
sudo systemctl daemon-reload
sudo systemctl enable calendar-sync
sudo systemctl start calendar-sync
sudo systemctl status calendar-sync
```

### 2.9 IP 주소 확인

```bash
ip addr show
```

또는

```bash
hostname -I
```

클라이언트에서 사용할 URL: `http://YOUR_IP:5000`

---

## 3. 네트워크 설정

### 3.1 고정 IP 설정 (권장)

서버 PC의 IP 주소가 변경되지 않도록 고정 IP를 설정하는 것이 좋습니다.

**Windows:**
1. 제어판 → 네트워크 및 공유 센터
2. 어댑터 설정 변경
3. 사용 중인 네트워크 어댑터 우클릭 → 속성
4. "인터넷 프로토콜 버전 4 (TCP/IPv4)" 선택 → 속성
5. "다음 IP 주소 사용" 선택
6. IP 주소, 서브넷 마스크, 기본 게이트웨이, DNS 서버 입력

**Linux:**

Ubuntu 18.04 이상 (netplan 사용):

```bash
sudo nano /etc/netplan/01-netcfg.yaml
```

```yaml
network:
  version: 2
  ethernets:
    eth0:
      dhcp4: no
      addresses:
        - 192.168.1.100/24
      gateway4: 192.168.1.1
      nameservers:
        addresses: [8.8.8.8, 8.8.4.4]
```

```bash
sudo netplan apply
```

### 3.2 라우터 포트 포워딩 (외부 접속 시)

외부 인터넷에서 접속하려면 라우터에서 포트 포워딩을 설정해야 합니다.

1. 라우터 관리 페이지 접속 (보통 192.168.0.1 또는 192.168.1.1)
2. 포트 포워딩 설정 메뉴 찾기
3. 새 규칙 추가:
   - 외부 포트: 5000
   - 내부 IP: 서버 PC의 IP (예: 192.168.1.100)
   - 내부 포트: 5000
   - 프로토콜: TCP

⚠️ 보안상 권장하지 않습니다. 대신 VPN을 사용하세요.

---

## 4. 테스트

### 4.1 로컬 테스트

서버가 실행 중인 PC에서:

```bash
curl http://localhost:5000
```

또는 브라우저에서 `http://localhost:5000` 접속

### 4.2 같은 네트워크에서 테스트

다른 PC에서:

```bash
curl http://SERVER_IP:5000
```

또는 브라우저에서 `http://SERVER_IP:5000` 접속

### 4.3 부서 생성 테스트

```bash
curl -X POST http://SERVER_IP:5000/api/departments \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"개발팀\",\"description\":\"소프트웨어 개발 부서\"}"
```

### 4.4 부서 목록 조회

```bash
curl http://SERVER_IP:5000/api/departments
```

---

## 5. 모니터링 및 로그

### Windows

서비스로 실행 중인 경우 NSSM이 로그를 남깁니다:
- `C:\calendar-sync-server\nssm\logs\`

수동 실행 시에는 콘솔에 로그가 출력됩니다.

### Linux

```bash
# 실시간 로그 확인
sudo journalctl -u calendar-sync -f

# 최근 100줄 로그
sudo journalctl -u calendar-sync -n 100

# 에러 로그만 확인
sudo journalctl -u calendar-sync -p err
```

---

## 6. 백업 및 복구

### 데이터베이스 백업

**Windows:**

```cmd
copy calendar_sync.db calendar_sync.db.backup
```

**Linux:**

```bash
cp calendar_sync.db calendar_sync.db.backup
```

### 자동 백업 스크립트 (Linux)

```bash
nano backup.sh
```

```bash
#!/bin/bash
BACKUP_DIR=~/calendar-sync-backups
mkdir -p $BACKUP_DIR
DATE=$(date +%Y%m%d_%H%M%S)
cp ~/calendar-sync-server/calendar_sync.db $BACKUP_DIR/calendar_sync_$DATE.db
# 7일 이상 된 백업 파일 삭제
find $BACKUP_DIR -name "calendar_sync_*.db" -mtime +7 -delete
```

```bash
chmod +x backup.sh
```

Cron으로 매일 자동 백업:

```bash
crontab -e
```

다음 줄 추가 (매일 새벽 3시에 백업):

```
0 3 * * * /home/YOUR_USERNAME/calendar-sync-server/backup.sh
```

---

## 7. 문제 해결

### 포트가 이미 사용 중

**Windows:**

```cmd
netstat -ano | findstr :5000
taskkill /PID <PID> /F
```

**Linux:**

```bash
sudo lsof -i :5000
sudo kill -9 <PID>
```

### 다른 PC에서 접속이 안 될 때

1. **방화벽 확인**: 포트 5000이 열려있는지 확인
2. **서비스 상태**: 서버가 실행 중인지 확인
3. **네트워크**: 같은 네트워크에 있는지 확인 (ping 테스트)
4. **IP 주소**: 서버 IP가 올바른지 확인

```bash
# Ping 테스트
ping SERVER_IP

# 포트 연결 테스트 (Telnet)
telnet SERVER_IP 5000
```

### 메모리 부족

Python 프로세스의 메모리 사용량이 높으면 서버를 재시작하세요:

```bash
# Linux
sudo systemctl restart calendar-sync

# Windows (NSSM)
nssm restart CalendarSyncServer
```

---

## 8. 보안 권장사항

✅ 내부 네트워크에서만 사용
✅ 외부 인터넷에 노출하지 않기
✅ 필요시 VPN 사용
✅ 정기적으로 백업
✅ 서버 PC를 최신 상태로 유지

---

## 9. 성능 최적화

### 여러 Worker 프로세스 사용

많은 사용자가 동시 접속하는 경우, Gunicorn (Linux)을 사용하여 여러 Worker를 실행할 수 있습니다:

```bash
pip install gunicorn
gunicorn --worker-class eventlet -w 4 --bind 0.0.0.0:5000 app:app
```

systemd 서비스 파일의 `ExecStart` 부분을 수정:

```ini
ExecStart=/home/YOUR_USERNAME/calendar-sync-server/venv/bin/gunicorn --worker-class eventlet -w 4 --bind 0.0.0.0:5000 app:app
```

---

## 연락처

문제가 발생하면 로그를 확인하고, 필요시 이슈를 등록해주세요.

