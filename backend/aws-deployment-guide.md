# AWS EC2 배포 가이드

## 1. AWS EC2 인스턴스 생성

### 1.1 EC2 인스턴스 시작
1. AWS 콘솔에서 EC2 서비스로 이동
2. "인스턴스 시작" 클릭
3. Amazon Linux 2 AMI 선택
4. t2.micro (무료 티어) 선택
5. 보안 그룹 설정:
   - SSH (22번 포트) - 내 IP에서만
   - HTTP (80번 포트) - 모든 위치에서
   - HTTPS (443번 포트) - 모든 위치에서 (선택사항)

### 1.2 키 페어 생성 및 다운로드
1. 새 키 페어 생성 또는 기존 키 페어 선택
2. .pem 파일을 안전한 곳에 저장

## 2. EC2 인스턴스에 연결

### 2.1 SSH로 연결
```bash
ssh -i "your-key.pem" ec2-user@your-ec2-public-ip
```

### 2.2 시스템 업데이트
```bash
sudo yum update -y
```

## 3. 백엔드 배포

### 3.1 프로젝트 파일 업로드
```bash
# SCP를 사용하여 파일 업로드
scp -i "your-key.pem" -r backend/ ec2-user@your-ec2-public-ip:~/
```

### 3.2 배포 스크립트 실행
```bash
cd backend
chmod +x deploy.sh
./deploy.sh
```

## 4. 도메인 설정 (선택사항)

### 4.1 Route 53에서 도메인 연결
1. Route 53에서 호스팅 영역 생성
2. A 레코드로 EC2 인스턴스 IP 연결
3. Apache 설정에서 ServerName 업데이트

### 4.2 SSL 인증서 설정 (Let's Encrypt)
```bash
sudo yum install certbot python3-certbot-apache -y
sudo certbot --apache -d your-domain.com
```

## 5. 프론트엔드 설정 업데이트

### 5.1 sync_settings.json 업데이트
```json
{
  "EnableSync": true,
  "ServerUrl": "http://your-ec2-public-ip/",
  "SelectedDepartmentId": 0,
  "SelectedDepartmentName": ""
}
```

### 5.2 또는 도메인 사용 시
```json
{
  "EnableSync": true,
  "ServerUrl": "https://your-domain.com/",
  "SelectedDepartmentId": 0,
  "SelectedDepartmentName": ""
}
```

## 6. 보안 설정

### 6.1 방화벽 설정
```bash
sudo ufw allow 22
sudo ufw allow 80
sudo ufw allow 443
sudo ufw enable
```

### 6.2 데이터베이스 백업 설정
```bash
# SQLite 데이터베이스 백업을 위한 cron 작업
echo "0 2 * * * cp /var/www/agent-assistant-backend/calendar_sync.db /var/www/agent-assistant-backend/backups/calendar_sync_\$(date +\%Y\%m\%d).db" | sudo crontab -
```

## 7. 모니터링 및 로그

### 7.1 Apache 로그 확인
```bash
sudo tail -f /var/log/apache2/agent-assistant-backend_error.log
sudo tail -f /var/log/apache2/agent-assistant-backend_access.log
```

### 7.2 서비스 상태 확인
```bash
sudo systemctl status apache2
```

## 8. 트러블슈팅

### 8.1 일반적인 문제들
- **502 Bad Gateway**: mod_wsgi 설정 확인
- **Permission Denied**: 파일 권한 확인
- **Module Not Found**: 가상환경 활성화 확인

### 8.2 로그 확인
```bash
sudo journalctl -u apache2 -f
```

## 9. 자동 배포 설정 (선택사항)

### 9.1 GitHub Actions 설정
```yaml
name: Deploy to AWS EC2
on:
  push:
    branches: [ main ]
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Deploy to EC2
      uses: appleboy/ssh-action@v0.1.5
      with:
        host: ${{ secrets.EC2_HOST }}
        username: ec2-user
        key: ${{ secrets.EC2_SSH_KEY }}
        script: |
          cd /home/ec2-user/backend
          git pull origin main
          sudo systemctl restart apache2
```

## 10. 비용 최적화

### 10.1 인스턴스 크기 조정
- 개발/테스트: t2.micro (무료 티어)
- 프로덕션: t3.small 또는 t3.medium

### 10.2 스토리지 최적화
- GP2 EBS 볼륨 사용
- 불필요한 로그 파일 정리

이 가이드를 따라하면 AWS EC2에 백엔드를 성공적으로 배포할 수 있습니다!


