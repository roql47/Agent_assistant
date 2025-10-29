"""
Flask + Socket.IO 기반 캘린더 동기화 서버
부서별 그룹 관리 및 실시간 캘린더 이벤트 동기화를 제공합니다.
"""

from flask import Flask, request, jsonify
from flask_socketio import SocketIO, emit, join_room, leave_room
from flask_cors import CORS
from models import get_database
import logging
import os

# Flask 애플리케이션 설정
app = Flask(__name__)
app.config['SECRET_KEY'] = os.environ.get('SECRET_KEY', 'your-secret-key-change-this-in-production')
app.config['JSON_AS_ASCII'] = False  # 한글 등 유니코드 문자 제대로 표시
CORS(app)

# Socket.IO 설정
socketio = SocketIO(app, cors_allowed_origins="*", async_mode='eventlet')

# 데이터베이스 초기화 (로컬 또는 AWS)
try:
    db_mode = os.environ.get('DB_MODE', 'local').lower()
    db = get_database(db_mode)
    
    from models import Department, Event
    department_model = Department(db)
    event_model = Event(db)
    
    mode_name = "로컬 NoSQL" if db_mode == 'local' else "AWS DynamoDB"
    print(f"✓ {mode_name} 연결 성공")
except Exception as e:
    print(f"✗ 데이터베이스 연결 실패: {e}")
    print("환경 변수를 확인하세요. (DB_MODE=local 또는 aws)")
    exit(1)

# 로깅 설정
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


# ==================== REST API 엔드포인트 ====================

@app.route('/')
def index():
    """서버 상태 확인"""
    db_mode = os.environ.get('DB_MODE', 'local').upper()
    return jsonify({
        'status': 'running',
        'message': f'캘린더 동기화 서버가 실행 중입니다. (모드: {db_mode})',
        'version': '2.0.0'
    })


@app.route('/api/departments', methods=['GET'])
def get_departments():
    """모든 부서 목록 조회"""
    departments = department_model.get_all()
    return jsonify({
        'success': True,
        'departments': departments
    })


@app.route('/api/departments', methods=['POST'])
def create_department():
    """새 부서 생성"""
    data = request.get_json()
    name = data.get('name')
    description = data.get('description', '')
    
    if not name:
        return jsonify({
            'success': False,
            'message': '부서 이름은 필수입니다.'
        }), 400
    
    department_id = department_model.create(name, description)
    
    if department_id:
        department = department_model.get_by_id(department_id)
        # 모든 클라이언트에게 새 부서 생성 알림
        socketio.emit('department_created', department)
        return jsonify({
            'success': True,
            'department': department
        }), 201
    else:
        return jsonify({
            'success': False,
            'message': '이미 존재하는 부서 이름입니다.'
        }), 409


@app.route('/api/departments/<department_id>', methods=['DELETE'])
def delete_department(department_id):
    """부서 삭제"""
    if department_model.delete(department_id):
        # 모든 클라이언트에게 부서 삭제 알림
        socketio.emit('department_deleted', {'id': department_id})
        return jsonify({
            'success': True,
            'message': '부서가 삭제되었습니다.'
        })
    else:
        return jsonify({
            'success': False,
            'message': '부서를 찾을 수 없습니다.'
        }), 404


@app.route('/api/events/<department_id>', methods=['GET'])
def get_events(department_id):
    """특정 부서의 모든 이벤트 조회"""
    events = event_model.get_by_department(department_id)
    return jsonify({
        'success': True,
        'events': events
    })


@app.route('/api/events/<department_id>', methods=['POST'])
def create_event(department_id):
    """새 이벤트 생성"""
    data = request.get_json()
    
    event_date = data.get('event_date')
    title = data.get('title')
    description = data.get('description', '')
    time = data.get('time', '')
    url = data.get('url', '')
    
    if not event_date or not title:
        return jsonify({
            'success': False,
            'message': '날짜와 제목은 필수입니다.'
        }), 400
    
    event_id = event_model.create(department_id, event_date, title, description, time, url)
    
    if event_id:
        event = event_model.get_by_id(event_id)
        # 같은 부서 그룹에게 이벤트 생성 알림
        socketio.emit('event_created', event, room=f'dept_{department_id}')
        return jsonify({
            'success': True,
            'event': event
        }), 201
    else:
        return jsonify({
            'success': False,
            'message': '이벤트 생성에 실패했습니다.'
        }), 500


@app.route('/api/events/<event_id>', methods=['PUT'])
def update_event(event_id):
    """이벤트 수정"""
    data = request.get_json()
    
    # 먼저 이벤트가 존재하는지 확인
    event = event_model.get_by_id(event_id)
    if not event:
        return jsonify({
            'success': False,
            'message': '이벤트를 찾을 수 없습니다.'
        }), 404
    
    department_id = event['department_id']
    
    # 이벤트 업데이트
    updated = event_model.update(
        event_id,
        title=data.get('title'),
        description=data.get('description'),
        time=data.get('time'),
        url=data.get('url'),
        event_date=data.get('event_date')
    )
    
    if updated:
        updated_event = event_model.get_by_id(event_id)
        # 같은 부서 그룹에게 이벤트 수정 알림
        socketio.emit('event_updated', updated_event, room=f'dept_{department_id}')
        return jsonify({
            'success': True,
            'event': updated_event
        })
    else:
        return jsonify({
            'success': False,
            'message': '이벤트 수정에 실패했습니다.'
        }), 500


@app.route('/api/events/<event_id>', methods=['DELETE'])
def delete_event(event_id):
    """이벤트 삭제"""
    # 먼저 이벤트가 존재하는지 확인하고 부서 ID 가져오기
    event = event_model.get_by_id(event_id)
    if not event:
        return jsonify({
            'success': False,
            'message': '이벤트를 찾을 수 없습니다.'
        }), 404
    
    department_id = event['department_id']
    
    if event_model.delete(event_id):
        # 같은 부서 그룹에게 이벤트 삭제 알림
        socketio.emit('event_deleted', {
            'id': event_id,
            'department_id': department_id
        }, room=f'dept_{department_id}')
        return jsonify({
            'success': True,
            'message': '이벤트가 삭제되었습니다.'
        })
    else:
        return jsonify({
            'success': False,
            'message': '이벤트 삭제에 실패했습니다.'
        }), 500


# ==================== WebSocket 이벤트 핸들러 ====================

@socketio.on('connect')
def handle_connect():
    """클라이언트 연결"""
    logger.info(f'클라이언트 연결됨: {request.sid}')
    emit('connected', {'message': '서버에 연결되었습니다.'})


@socketio.on('disconnect')
def handle_disconnect():
    """클라이언트 연결 해제"""
    logger.info(f'클라이언트 연결 해제됨: {request.sid}')


@socketio.on('join_department')
def handle_join_department(data):
    """부서 그룹 참여"""
    department_id = data.get('department_id')
    
    if department_id:
        room = f'dept_{department_id}'
        join_room(room)
        logger.info(f'클라이언트 {request.sid}가 부서 {department_id} 그룹에 참여했습니다.')
        emit('joined_department', {
            'department_id': department_id,
            'message': f'부서 그룹에 참여했습니다.'
        })


@socketio.on('leave_department')
def handle_leave_department(data):
    """부서 그룹 나가기"""
    department_id = data.get('department_id')
    
    if department_id:
        room = f'dept_{department_id}'
        leave_room(room)
        logger.info(f'클라이언트 {request.sid}가 부서 {department_id} 그룹에서 나갔습니다.')
        emit('left_department', {
            'department_id': department_id,
            'message': f'부서 그룹에서 나갔습니다.'
        })


@socketio.on('sync_request')
def handle_sync_request(data):
    """동기화 요청 처리"""
    department_id = data.get('department_id')
    
    if department_id:
        events = event_model.get_by_department(department_id)
        emit('sync_response', {
            'department_id': department_id,
            'events': events
        })
        logger.info(f'클라이언트 {request.sid}에게 부서 {department_id}의 이벤트 동기화 완료')


# ==================== 서버 실행 ====================

if __name__ == '__main__':
    # AWS 배포 시에는 Apache mod_wsgi를 사용하므로 직접 실행하지 않음
    # 로컬 개발 시에만 사용
    if os.environ.get('FLASK_ENV') == 'development':
        logger.info('캘린더 동기화 서버를 시작합니다...')
        logger.info('서버 주소: http://0.0.0.0:5000')
        socketio.run(app, host='0.0.0.0', port=5000, debug=True)
    else:
        # AWS 배포 시에는 app 객체만 export
        logger.info('AWS 배포 모드: Apache mod_wsgi를 통해 실행됩니다.')

