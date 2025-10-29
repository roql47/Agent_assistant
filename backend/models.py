"""
AWS DynamoDB를 사용한 데이터베이스 모델 정의
부서 및 캘린더 이벤트 데이터를 DynamoDB로 관리합니다.
"""

import boto3
import uuid
from datetime import datetime
from typing import List, Dict, Optional
import os
from decimal import Decimal


class DynamoDBDatabase:
    """AWS DynamoDB 연결 및 설정 관리"""
    
    def __init__(self):
        # AWS 환경 설정
        self.region = os.environ.get('AWS_REGION', 'ap-northeast-2')
        self.dynamodb = boto3.resource('dynamodb', region_name=self.region)
        
        # 테이블 이름
        self.departments_table_name = os.environ.get('DEPARTMENTS_TABLE', 'departments')
        self.events_table_name = os.environ.get('EVENTS_TABLE', 'events')
        
        # 테이블 참조
        self.departments_table = self.dynamodb.Table(self.departments_table_name)
        self.events_table = self.dynamodb.Table(self.events_table_name)
    
    def create_tables(self):
        """DynamoDB 테이블 생성 (최초 1회만)"""
        try:
            # 부서 테이블
            self.dynamodb.create_table(
                TableName=self.departments_table_name,
                KeySchema=[
                    {'AttributeName': 'id', 'KeyType': 'HASH'}  # Partition Key
                ],
                AttributeDefinitions=[
                    {'AttributeName': 'id', 'AttributeType': 'S'},
                    {'AttributeName': 'name', 'AttributeType': 'S'}
                ],
                GlobalSecondaryIndexes=[
                    {
                        'IndexName': 'name-index',
                        'KeySchema': [
                            {'AttributeName': 'name', 'KeyType': 'HASH'}
                        ],
                        'Projection': {'ProjectionType': 'ALL'},
                        'ProvisionedThroughput': {
                            'ReadCapacityUnits': 5,
                            'WriteCapacityUnits': 5
                        }
                    }
                ],
                ProvisionedThroughput={
                    'ReadCapacityUnits': 10,
                    'WriteCapacityUnits': 10
                }
            )
            print(f"✓ {self.departments_table_name} 테이블이 생성되었습니다.")
            
            # 이벤트 테이블
            self.dynamodb.create_table(
                TableName=self.events_table_name,
                KeySchema=[
                    {'AttributeName': 'id', 'KeyType': 'HASH'},  # Partition Key
                    {'AttributeName': 'department_id', 'KeyType': 'RANGE'}  # Sort Key
                ],
                AttributeDefinitions=[
                    {'AttributeName': 'id', 'AttributeType': 'S'},
                    {'AttributeName': 'department_id', 'AttributeType': 'S'},
                    {'AttributeName': 'event_date', 'AttributeType': 'S'}
                ],
                GlobalSecondaryIndexes=[
                    {
                        'IndexName': 'department-date-index',
                        'KeySchema': [
                            {'AttributeName': 'department_id', 'KeyType': 'HASH'},
                            {'AttributeName': 'event_date', 'KeyType': 'RANGE'}
                        ],
                        'Projection': {'ProjectionType': 'ALL'},
                        'ProvisionedThroughput': {
                            'ReadCapacityUnits': 10,
                            'WriteCapacityUnits': 10
                        }
                    }
                ],
                ProvisionedThroughput={
                    'ReadCapacityUnits': 10,
                    'WriteCapacityUnits': 10
                }
            )
            print(f"✓ {self.events_table_name} 테이블이 생성되었습니다.")
        except self.dynamodb.meta.client.exceptions.ResourceInUseException:
            print("✓ 테이블이 이미 존재합니다.")
    
    def get_departments_table(self):
        return self.departments_table
    
    def get_events_table(self):
        return self.events_table


class Department:
    """AWS DynamoDB 기반 부서 모델"""
    
    def __init__(self, db: DynamoDBDatabase):
        self.db = db
        self.table = db.get_departments_table()
    
    def create(self, name: str, description: str = "") -> Optional[str]:
        """새 부서 생성"""
        try:
            department_id = str(uuid.uuid4())
            
            item = {
                'id': department_id,
                'name': name,
                'description': description,
                'created_at': datetime.utcnow().isoformat(),
                'updated_at': datetime.utcnow().isoformat()
            }
            
            # 중복 확인
            response = self.table.scan(
                FilterExpression='#name = :name',
                ExpressionAttributeNames={'#name': 'name'},
                ExpressionAttributeValues={':name': name}
            )
            
            if response.get('Items'):
                return None  # 중복된 부서명
            
            self.table.put_item(Item=item)
            return department_id
        except Exception as e:
            print(f"부서 생성 오류: {e}")
            return None
    
    def get_all(self) -> List[Dict]:
        """모든 부서 조회"""
        try:
            response = self.table.scan()
            items = response.get('Items', [])
            
            # Decimal을 int로 변환
            for item in items:
                self._convert_decimal(item)
            
            return sorted(items, key=lambda x: x.get('name', ''))
        except Exception as e:
            print(f"부서 조회 오류: {e}")
            return []
    
    def get_by_id(self, department_id: str) -> Optional[Dict]:
        """ID로 부서 조회"""
        try:
            response = self.table.get_item(Key={'id': department_id})
            item = response.get('Item')
            if item:
                self._convert_decimal(item)
            return item
        except Exception as e:
            print(f"부서 조회 오류: {e}")
            return None
    
    def delete(self, department_id: str) -> bool:
        """부서 삭제"""
        try:
            self.table.delete_item(Key={'id': department_id})
            return True
        except Exception as e:
            print(f"부서 삭제 오류: {e}")
            return False
    
    @staticmethod
    def _convert_decimal(obj):
        """Decimal 값을 int 또는 float로 변환"""
        if isinstance(obj, dict):
            for k, v in obj.items():
                if isinstance(v, Decimal):
                    obj[k] = int(v) if v % 1 == 0 else float(v)
                elif isinstance(v, dict):
                    Department._convert_decimal(v)
                elif isinstance(v, list):
                    for item in v:
                        if isinstance(item, dict):
                            Department._convert_decimal(item)


class Event:
    """AWS DynamoDB 기반 캘린더 이벤트 모델"""
    
    def __init__(self, db: DynamoDBDatabase):
        self.db = db
        self.table = db.get_events_table()
    
    def create(self, department_id: str, event_date: str, title: str,
               description: str = "", time: str = "", url: str = "") -> Optional[str]:
        """새 이벤트 생성"""
        try:
            event_id = str(uuid.uuid4())
            
            item = {
                'id': event_id,
                'department_id': department_id,
                'event_date': event_date,
                'title': title,
                'description': description,
                'time': time,
                'url': url,
                'created_at': datetime.utcnow().isoformat(),
                'last_modified': datetime.utcnow().isoformat()
            }
            
            self.table.put_item(Item=item)
            return event_id
        except Exception as e:
            print(f"이벤트 생성 오류: {e}")
            return None
    
    def get_by_department(self, department_id: str) -> List[Dict]:
        """부서별 모든 이벤트 조회"""
        try:
            response = self.table.query(
                IndexName='department-date-index',
                KeyConditionExpression='department_id = :dept_id',
                ExpressionAttributeValues={':dept_id': department_id}
            )
            
            items = response.get('Items', [])
            
            # Decimal 변환
            for item in items:
                Event._convert_decimal(item)
            
            return sorted(items, key=lambda x: (x.get('event_date', ''), x.get('time', '')))
        except Exception as e:
            print(f"이벤트 조회 오류: {e}")
            return []
    
    def get_by_id(self, event_id: str) -> Optional[Dict]:
        """ID로 이벤트 조회"""
        try:
            # DynamoDB에서 GSI를 사용하여 id로만 검색
            response = self.table.scan(
                FilterExpression='id = :event_id',
                ExpressionAttributeValues={':event_id': event_id}
            )
            
            items = response.get('Items', [])
            if items:
                Event._convert_decimal(items[0])
                return items[0]
            return None
        except Exception as e:
            print(f"이벤트 조회 오류: {e}")
            return None
    
    def update(self, event_id: str, title: str = None, description: str = None,
               time: str = None, url: str = None, event_date: str = None) -> bool:
        """이벤트 수정"""
        try:
            # 먼저 이벤트 조회
            event = self.get_by_id(event_id)
            if not event:
                return False
            
            # 업데이트할 속성 준비
            update_expression_parts = []
            expression_attribute_values = {}
            
            if title is not None:
                update_expression_parts.append('title = :title')
                expression_attribute_values[':title'] = title
            if description is not None:
                update_expression_parts.append('description = :description')
                expression_attribute_values[':description'] = description
            if time is not None:
                update_expression_parts.append('time_val = :time')
                expression_attribute_values[':time'] = time
            if url is not None:
                update_expression_parts.append('url = :url')
                expression_attribute_values[':url'] = url
            if event_date is not None:
                update_expression_parts.append('event_date = :event_date')
                expression_attribute_values[':event_date'] = event_date
            
            if not update_expression_parts:
                return False
            
            update_expression_parts.append('last_modified = :last_modified')
            expression_attribute_values[':last_modified'] = datetime.utcnow().isoformat()
            
            # 업데이트 실행
            self.table.update_item(
                Key={'id': event_id, 'department_id': event['department_id']},
                UpdateExpression='SET ' + ', '.join(update_expression_parts),
                ExpressionAttributeValues=expression_attribute_values
            )
            return True
        except Exception as e:
            print(f"이벤트 수정 오류: {e}")
            return False
    
    def delete(self, event_id: str) -> bool:
        """이벤트 삭제"""
        try:
            # 먼저 이벤트 조회
            event = self.get_by_id(event_id)
            if not event:
                return False
            
            self.table.delete_item(
                Key={'id': event_id, 'department_id': event['department_id']}
            )
            return True
        except Exception as e:
            print(f"이벤트 삭제 오류: {e}")
            return False
    
    def get_by_date_range(self, department_id: str, start_date: str, end_date: str) -> List[Dict]:
        """날짜 범위로 이벤트 조회"""
        try:
            response = self.table.query(
                IndexName='department-date-index',
                KeyConditionExpression='department_id = :dept_id AND event_date BETWEEN :start AND :end',
                ExpressionAttributeValues={
                    ':dept_id': department_id,
                    ':start': start_date,
                    ':end': end_date
                }
            )
            
            items = response.get('Items', [])
            
            # Decimal 변환
            for item in items:
                Event._convert_decimal(item)
            
            return sorted(items, key=lambda x: (x.get('event_date', ''), x.get('time', '')))
        except Exception as e:
            print(f"이벤트 날짜 범위 조회 오류: {e}")
            return []
    
    @staticmethod
    def _convert_decimal(obj):
        """Decimal 값을 int 또는 float로 변환"""
        if isinstance(obj, dict):
            for k, v in obj.items():
                if isinstance(v, Decimal):
                    obj[k] = int(v) if v % 1 == 0 else float(v)
                elif isinstance(v, dict):
                    Event._convert_decimal(v)
                elif isinstance(v, list):
                    for item in v:
                        if isinstance(item, dict):
                            Event._convert_decimal(item)


# 하위 호환성을 위한 별칭
Database = DynamoDBDatabase


# 로컬 테스트 모드 지원
def get_database(mode: str = None):
    """
    데이터베이스 인스턴스 생성
    
    Args:
        mode: 'local' (로컬 JSON 파일) 또는 'aws' (AWS DynamoDB)
              None이면 환경 변수 확인
    
    Returns:
        DynamoDBDatabase 또는 LocalDynamoDBDatabase 인스턴스
    """
    if mode is None:
        mode = os.environ.get('DB_MODE', 'local').lower()
    
    if mode == 'local':
        from local_nosql import LocalDynamoDBDatabase
        data_dir = os.environ.get('LOCAL_DATA_DIR', 'data')
        return LocalDynamoDBDatabase(data_dir)
    elif mode == 'aws':
        return DynamoDBDatabase()
    else:
        raise ValueError(f"Unknown database mode: {mode}. Use 'local' or 'aws'")

