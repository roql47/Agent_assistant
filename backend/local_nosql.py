"""
로컬 테스트 환경용 NoSQL 저장소
JSON 파일 기반의 NoSQL 데이터베이스 에뮬레이션
"""

import json
import uuid
import os
from datetime import datetime
from typing import List, Dict, Optional
from pathlib import Path


class LocalNoSQLDatabase:
    """로컬 파일 기반 NoSQL 데이터베이스"""
    
    def __init__(self, data_dir: str = "data"):
        """
        Args:
            data_dir: 데이터를 저장할 디렉토리 경로
        """
        self.data_dir = Path(data_dir)
        self.data_dir.mkdir(exist_ok=True)
        
        # 테이블 파일 경로
        self.departments_file = self.data_dir / "departments.json"
        self.events_file = self.data_dir / "events.json"
        
        # 테이블 초기화
        self._init_storage()
        print(f"✓ 로컬 NoSQL 저장소 초기화 완료: {self.data_dir}")
    
    def _init_storage(self):
        """저장소 초기화"""
        if not self.departments_file.exists():
            self._write_json(self.departments_file, {})
        
        if not self.events_file.exists():
            self._write_json(self.events_file, {})
    
    def _read_json(self, file_path: Path) -> dict:
        """JSON 파일 읽기"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except (FileNotFoundError, json.JSONDecodeError):
            return {}
    
    def _write_json(self, file_path: Path, data: dict):
        """JSON 파일 쓰기"""
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
    
    def get_departments_table(self):
        """부서 테이블 반환"""
        return LocalNoSQLTable(self.departments_file, "departments")
    
    def get_events_table(self):
        """이벤트 테이블 반환"""
        return LocalNoSQLTable(self.events_file, "events")


class LocalNoSQLTable:
    """로컬 NoSQL 테이블 에뮬레이션"""
    
    def __init__(self, file_path: Path, table_name: str):
        self.file_path = file_path
        self.table_name = table_name
    
    def _read_data(self) -> dict:
        """현재 데이터 읽기"""
        try:
            with open(self.file_path, 'r', encoding='utf-8') as f:
                content = f.read()
                if not content:
                    return {}
                return json.loads(content)
        except (FileNotFoundError, json.JSONDecodeError):
            return {}
    
    def _write_data(self, data: dict):
        """데이터 쓰기"""
        with open(self.file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
    
    def put_item(self, Item: dict):
        """항목 추가 또는 수정"""
        data = self._read_data()
        item_id = Item.get('id')
        
        if not item_id:
            raise ValueError("Item must have 'id' field")
        
        data[item_id] = Item
        self._write_data(data)
    
    def get_item(self, Key: dict) -> dict:
        """항목 조회"""
        data = self._read_data()
        item_id = Key.get('id')
        
        if item_id in data:
            return {'Item': data[item_id]}
        return {'Item': None}
    
    def delete_item(self, Key: dict):
        """항목 삭제"""
        data = self._read_data()
        item_id = Key.get('id')
        
        if item_id in data:
            del data[item_id]
            self._write_data(data)
    
    def scan(self, FilterExpression: str = None, 
             ExpressionAttributeNames: dict = None,
             ExpressionAttributeValues: dict = None) -> dict:
        """전체 스캔 (필터링 지원)"""
        data = self._read_data()
        items = list(data.values())
        
        # 필터링 적용
        if FilterExpression and ExpressionAttributeValues:
            filtered_items = []
            for item in items:
                if self._evaluate_filter(item, FilterExpression, ExpressionAttributeNames, ExpressionAttributeValues):
                    filtered_items.append(item)
            items = filtered_items
        
        return {'Items': items}
    
    def query(self, IndexName: str = None, KeyConditionExpression: str = None,
              ExpressionAttributeValues: dict = None) -> dict:
        """쿼리 실행"""
        data = self._read_data()
        items = list(data.values())
        
        # 키 조건 평가
        if KeyConditionExpression and ExpressionAttributeValues:
            filtered_items = []
            for item in items:
                if self._evaluate_query(item, KeyConditionExpression, ExpressionAttributeValues):
                    filtered_items.append(item)
            items = filtered_items
        
        return {'Items': items}
    
    def update_item(self, Key: dict, UpdateExpression: str,
                   ExpressionAttributeValues: dict):
        """항목 업데이트"""
        data = self._read_data()
        item_id = Key.get('id')
        
        if item_id not in data:
            raise KeyError(f"Item with id {item_id} not found")
        
        item = data[item_id]
        
        # UpdateExpression 처리 (간단한 SET 구문만 지원)
        if UpdateExpression.startswith('SET'):
            parts = UpdateExpression[3:].split(',')
            
            for part in parts:
                part = part.strip()
                if '=' in part:
                    attr_name, attr_value = part.split('=')
                    attr_name = attr_name.strip()
                    attr_value = attr_value.strip()
                    
                    # ExpressionAttributeValues에서 값 찾기
                    if attr_value.startswith(':'):
                        if attr_value in ExpressionAttributeValues:
                            item[attr_name] = ExpressionAttributeValues[attr_value]
        
        data[item_id] = item
        self._write_data(data)
    
    @staticmethod
    def _evaluate_filter(item: dict, expression: str, attr_names: dict, attr_values: dict) -> bool:
        """필터 표현식 평가"""
        # name = :value 형식
        if '=' in expression:
            left, right = expression.split('=')
            left = left.strip()
            right = right.strip()
            
            # #name 치환
            if left.startswith('#') and attr_names:
                left = attr_names.get(left, left)
            
            # :value 치환
            if right.startswith(':') and attr_values:
                right_value = attr_values.get(right)
                return item.get(left) == right_value
        
        return True
    
    @staticmethod
    def _evaluate_query(item: dict, expression: str, attr_values: dict) -> bool:
        """쿼리 조건 평가"""
        # department_id = :dept_id 형식
        # 또는 department_id = :dept_id AND event_date BETWEEN :start AND :end
        
        if 'BETWEEN' in expression:
            # BETWEEN 처리
            parts = expression.split(' AND ')
            
            for part in parts:
                part = part.strip()
                
                if '=' in part:
                    attr_name, value_key = part.split('=')
                    attr_name = attr_name.strip()
                    value_key = value_key.strip()
                    
                    if value_key.startswith(':') and value_key in attr_values:
                        if item.get(attr_name) != attr_values[value_key]:
                            return False
                
                elif 'BETWEEN' in part:
                    attr_name, rest = part.split(' BETWEEN ')
                    attr_name = attr_name.strip()
                    start_key, end_key = rest.split(' AND ')
                    start_key = start_key.strip().lstrip(':')
                    end_key = end_key.strip().lstrip(':')
                    
                    value = item.get(attr_name)
                    if value:
                        start = attr_values.get(':' + start_key)
                        end = attr_values.get(':' + end_key)
                        
                        if not (start <= value <= end):
                            return False
            
            return True
        
        elif '=' in expression:
            attr_name, value_key = expression.split('=')
            attr_name = attr_name.strip()
            value_key = value_key.strip()
            
            if value_key.startswith(':') and value_key in attr_values:
                return item.get(attr_name) == attr_values[value_key]
        
        return True


# boto3 호환 래퍼
class LocalNoSQLClient:
    """boto3 호환 클라이언트"""
    
    def __init__(self, data_dir: str = "data"):
        self.db = LocalNoSQLDatabase(data_dir)
        self.meta = type('obj', (object,), {'client': type('obj', (object,), {
            'exceptions': type('obj', (object,), {
                'ResourceInUseException': Exception
            })()
        })()})()
    
    def create_table(self, **kwargs):
        """테이블 생성 (로컬에서는 무시)"""
        table_name = kwargs.get('TableName')
        print(f"✓ 로컬 테이블 '{table_name}' 준비 완료")
    
    def Table(self, table_name: str):
        """테이블 객체 반환"""
        if table_name == 'departments':
            return self.db.get_departments_table()
        elif table_name == 'events':
            return self.db.get_events_table()
        else:
            raise ValueError(f"Unknown table: {table_name}")
    
    def resource(self, *args, **kwargs):
        """AWS 리소스 호환성"""
        return self


class LocalDynamoDBDatabase:
    """로컬 테스트용 DynamoDB 에뮬레이션"""
    
    def __init__(self, data_dir: str = "data"):
        self.data_dir = data_dir
        self.client = LocalNoSQLClient(data_dir)
        self.dynamodb = self.client
    
    def create_tables(self):
        """테이블 생성 (로컬에서는 무시)"""
        self.dynamodb.create_table(TableName='departments')
        self.dynamodb.create_table(TableName='events')
        print("✓ 모든 로컬 테이블이 준비되었습니다")
    
    def get_departments_table(self):
        """부서 테이블 반환"""
        return self.dynamodb.Table('departments')
    
    def get_events_table(self):
        """이벤트 테이블 반환"""
        return self.dynamodb.Table('events')

