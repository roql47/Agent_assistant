"""
JSON 기반 데이터 → AWS DynamoDB 마이그레이션 스크립트
기존의 departments.json과 calendar_events.json을 DynamoDB로 옮깁니다.
"""

import json
import os
import boto3
from datetime import datetime
import uuid
from pathlib import Path


class MigrationManager:
    """마이그레이션 관리 클래스"""
    
    def __init__(self):
        # AWS 설정
        self.region = os.environ.get('AWS_REGION', 'ap-northeast-2')
        self.dynamodb = boto3.resource('dynamodb', region_name=self.region)
        
        # 테이블 이름
        self.departments_table_name = os.environ.get('DEPARTMENTS_TABLE', 'departments')
        self.events_table_name = os.environ.get('EVENTS_TABLE', 'events')
        
        # 테이블 참조
        self.departments_table = self.dynamodb.Table(self.departments_table_name)
        self.events_table = self.dynamodb.Table(self.events_table_name)
        
        # 파일 경로
        self.base_path = Path(__file__).parent.parent
        self.departments_file = self.base_path / 'departments.json'
        self.calendar_file = self.base_path / 'calendar_events.json'
    
    def load_json(self, file_path):
        """JSON 파일 로드"""
        try:
            if not file_path.exists():
                print(f"⚠️  파일을 찾을 수 없습니다: {file_path}")
                return None
            
            with open(file_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception as e:
            print(f"❌ JSON 로드 오류 ({file_path}): {e}")
            return None
    
    def migrate_departments(self):
        """부서 데이터 마이그레이션"""
        print("\n📋 부서 데이터 마이그레이션 시작...")
        
        data = self.load_json(self.departments_file)
        if not data:
            return 0
        
        departments = data.get('departments', [])
        migrated = 0
        
        for dept in departments:
            try:
                # UUID 생성
                item = {
                    'id': str(uuid.uuid4()),
                    'name': dept.get('name', ''),
                    'description': dept.get('description', ''),
                    'created_at': datetime.utcnow().isoformat(),
                    'updated_at': datetime.utcnow().isoformat()
                }
                
                # DynamoDB에 저장
                self.departments_table.put_item(Item=item)
                print(f"  ✓ 부서 '{item['name']}' 마이그레이션 완료")
                migrated += 1
            except Exception as e:
                print(f"  ❌ 부서 '{dept.get('name')}' 마이그레이션 실패: {e}")
        
        print(f"\n✅ 부서 데이터 마이그레이션 완료: {migrated}개")
        return migrated
    
    def migrate_events(self):
        """캘린더 이벤트 데이터 마이그레이션"""
        print("\n📅 캘린더 이벤트 데이터 마이그레이션 시작...")
        
        data = self.load_json(self.calendar_file)
        if not data:
            return 0
        
        # 부서 목록 조회 (매핑용)
        departments = self._get_all_departments()
        
        migrated = 0
        
        for date_str, events_list in data.items():
            for event in events_list:
                try:
                    # 첫 번째 부서로 매핑 (기존 데이터는 부서 ID가 0)
                    default_dept_id = departments[0]['id'] if departments else str(uuid.uuid4())
                    
                    item = {
                        'id': str(uuid.uuid4()),
                        'department_id': default_dept_id,
                        'event_date': date_str,
                        'title': event.get('Title', ''),
                        'description': event.get('Description', ''),
                        'time': event.get('Time', ''),
                        'url': event.get('Url', ''),
                        'created_at': datetime.utcnow().isoformat(),
                        'last_modified': event.get('LastModified', datetime.utcnow().isoformat())
                    }
                    
                    # DynamoDB에 저장
                    self.events_table.put_item(Item=item)
                    print(f"  ✓ 이벤트 '{item['title']}' ({date_str}) 마이그레이션 완료")
                    migrated += 1
                except Exception as e:
                    print(f"  ❌ 이벤트 마이그레이션 실패: {e}")
        
        print(f"\n✅ 이벤트 데이터 마이그레이션 완료: {migrated}개")
        return migrated
    
    def _get_all_departments(self):
        """모든 부서 조회"""
        try:
            response = self.departments_table.scan()
            return response.get('Items', [])
        except Exception as e:
            print(f"❌ 부서 조회 오류: {e}")
            return []
    
    def verify_migration(self):
        """마이그레이션 검증"""
        print("\n🔍 마이그레이션 검증 중...")
        
        try:
            # 부서 개수 확인
            dept_response = self.departments_table.scan()
            dept_count = len(dept_response.get('Items', []))
            print(f"  • 부서: {dept_count}개")
            
            # 이벤트 개수 확인
            event_response = self.events_table.scan()
            event_count = len(event_response.get('Items', []))
            print(f"  • 이벤트: {event_count}개")
            
            print("\n✅ 마이그레이션 검증 완료")
            return dept_count, event_count
        except Exception as e:
            print(f"❌ 마이그레이션 검증 실패: {e}")
            return 0, 0
    
    def run(self):
        """전체 마이그레이션 실행"""
        print("=" * 50)
        print("🚀 JSON → DynamoDB 마이그레이션 시작")
        print("=" * 50)
        
        # 테이블 생성 여부 확인
        if not self._tables_exist():
            print("⚠️  DynamoDB 테이블이 없습니다.")
            print("다음 명령어로 테이블을 생성하세요:")
            print("\n  from models import DynamoDBDatabase")
            print("  db = DynamoDBDatabase()")
            print("  db.create_tables()")
            return False
        
        # 마이그레이션 실행
        dept_migrated = self.migrate_departments()
        event_migrated = self.migrate_events()
        
        # 검증
        dept_count, event_count = self.verify_migration()
        
        print("\n" + "=" * 50)
        print("📊 마이그레이션 결과 요약")
        print("=" * 50)
        print(f"부서: {dept_count}개")
        print(f"이벤트: {event_count}개")
        print("=" * 50)
        
        return True
    
    def _tables_exist(self):
        """테이블이 존재하는지 확인"""
        try:
            client = boto3.client('dynamodb', region_name=self.region)
            tables = client.list_tables()['TableNames']
            return self.departments_table_name in tables and self.events_table_name in tables
        except Exception as e:
            print(f"❌ 테이블 확인 실패: {e}")
            return False


def main():
    """메인 함수"""
    try:
        migration = MigrationManager()
        success = migration.run()
        
        if success:
            print("\n✨ 마이그레이션 완료! DynamoDB를 사용할 준비가 되었습니다.")
        else:
            print("\n❌ 마이그레이션 실패했습니다.")
            exit(1)
    except Exception as e:
        print(f"\n❌ 예상치 못한 오류: {e}")
        exit(1)


if __name__ == '__main__':
    main()

