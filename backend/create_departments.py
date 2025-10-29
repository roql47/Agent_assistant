#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
부서 생성 스크립트
한글 부서명을 올바르게 생성합니다.
"""

import requests
import sys

# 서버 URL
SERVER_URL = "http://localhost:5000"

# 생성할 부서 목록
DEPARTMENTS = [
    {"name": "심장뇌혈관시술센터", "description": ""},
    {"name": "내과", "description": ""},
    {"name": "외과", "description": ""},
    {"name": "응급의료센터", "description": ""},
]

def create_department(name, description=""):
    """부서 생성"""
    try:
        response = requests.post(
            f"{SERVER_URL}/api/departments",
            json={"name": name, "description": description},
            timeout=5
        )
        
        if response.status_code == 201:
            result = response.json()
            dept = result['department']
            print(f"✓ 부서 생성 성공: {dept['name']} (ID: {dept['id']})")
            return True
        else:
            result = response.json()
            print(f"✗ 부서 생성 실패: {name} - {result.get('message', '알 수 없는 오류')}")
            return False
    except Exception as e:
        print(f"✗ 오류 발생: {name} - {e}")
        return False

def list_departments():
    """생성된 부서 목록 조회"""
    try:
        response = requests.get(f"{SERVER_URL}/api/departments", timeout=5)
        result = response.json()
        
        print("\n" + "="*50)
        print("생성된 부서 목록:")
        print("="*50)
        
        for dept in result['departments']:
            print(f"ID: {dept['id']:2d} | 이름: {dept['name']}")
        
        print("="*50)
        print(f"총 {len(result['departments'])}개 부서\n")
    except Exception as e:
        print(f"✗ 부서 목록 조회 오류: {e}")

def main():
    """메인 함수"""
    print("캘린더 동기화 서버 - 부서 생성 스크립트")
    print(f"서버: {SERVER_URL}\n")
    
    # 서버 연결 확인
    try:
        response = requests.get(SERVER_URL, timeout=5)
        if response.status_code == 200:
            print("✓ 서버 연결 성공\n")
        else:
            print("✗ 서버 응답 오류")
            return
    except Exception as e:
        print(f"✗ 서버에 연결할 수 없습니다: {e}")
        print("\n서버를 먼저 실행해주세요:")
        print("  python app.py")
        return
    
    # 부서 생성
    print("부서 생성 중...\n")
    success_count = 0
    
    for dept in DEPARTMENTS:
        if create_department(dept['name'], dept['description']):
            success_count += 1
    
    print(f"\n생성 완료: {success_count}/{len(DEPARTMENTS)}개 부서")
    
    # 생성된 부서 목록 출력
    list_departments()

if __name__ == "__main__":
    main()

