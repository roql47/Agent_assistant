using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using SocketIOClient;

namespace AgentAssistant
{
    /// <summary>
    /// 서버와의 실시간 동기화를 관리하는 서비스
    /// </summary>
    public class SyncService
    {
        private SocketIOClient.SocketIO? socket;
        private HttpClient httpClient;
        private string serverUrl = "";
        private int? currentDepartmentId;
        private bool isConnected = false;

        // 이벤트 핸들러
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler<SyncEventArgs>? EventCreated;
        public event EventHandler<SyncEventArgs>? EventUpdated;
        public event EventHandler<int>? EventDeleted;
        public event EventHandler<List<ServerEvent>>? SyncReceived;
        public event EventHandler<string>? ConnectionStatusChanged;

        public bool IsConnected => isConnected;
        public string ServerUrl => serverUrl;

        public SyncService()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// 서버에 연결
        /// </summary>
        public async Task<bool> ConnectAsync(string url, int departmentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    OnConnectionStatusChanged("서버 URL이 비어있습니다.");
                    return false;
                }

                serverUrl = url.TrimEnd('/');
                currentDepartmentId = departmentId;

                // HTTP 연결 테스트
                var response = await httpClient.GetAsync($"{serverUrl}/");
                if (!response.IsSuccessStatusCode)
                {
                    OnConnectionStatusChanged("서버에 접속할 수 없습니다.");
                    return false;
                }

                // Socket.IO 연결
                socket = new SocketIOClient.SocketIO(serverUrl);

                // 연결 이벤트 핸들러
                socket.OnConnected += async (sender, e) =>
                {
                    isConnected = true;
                    OnConnected();
                    OnConnectionStatusChanged("서버에 연결됨");

                    // 부서 그룹 참여
                    if (currentDepartmentId.HasValue)
                    {
                        await socket.EmitAsync("join_department", new { department_id = currentDepartmentId.Value });
                    }
                };

                socket.OnDisconnected += (sender, e) =>
                {
                    isConnected = false;
                    OnDisconnected();
                    OnConnectionStatusChanged("서버 연결 끊김");
                };

                // WebSocket 이벤트 수신
                socket.On("event_created", response =>
                {
                    var eventData = response.GetValue<ServerEvent>();
                    OnEventCreated(eventData);
                });

                socket.On("event_updated", response =>
                {
                    var eventData = response.GetValue<ServerEvent>();
                    OnEventUpdated(eventData);
                });

                socket.On("event_deleted", response =>
                {
                    var data = response.GetValue<JsonElement>();
                    if (data.TryGetProperty("id", out var idElement))
                    {
                        OnEventDeleted(idElement.GetInt32());
                    }
                });

                socket.On("sync_response", response =>
                {
                    var data = response.GetValue<SyncResponse>();
                    OnSyncReceived(data.Events);
                });

                await socket.ConnectAsync();
                return true;
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged($"연결 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 서버 연결 해제
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (socket != null && isConnected)
                {
                    if (currentDepartmentId.HasValue)
                    {
                        await socket.EmitAsync("leave_department", new { department_id = currentDepartmentId.Value });
                    }
                    await socket.DisconnectAsync();
                }
                isConnected = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"연결 해제 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 부서 목록 가져오기
        /// </summary>
        public async Task<List<Department>> GetDepartmentsAsync()
        {
            try
            {
                var response = await httpClient.GetFromJsonAsync<DepartmentResponse>($"{serverUrl}/api/departments");
                return response?.Departments ?? new List<Department>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 목록 가져오기 오류: {ex.Message}");
                return new List<Department>();
            }
        }

        /// <summary>
        /// 새 부서 생성
        /// </summary>
        public async Task<Department?> CreateDepartmentAsync(string name, string description = "")
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync($"{serverUrl}/api/departments", new
                {
                    name = name,
                    description = description
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CreateDepartmentResponse>();
                    return result?.Department;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 생성 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 부서별 이벤트 가져오기
        /// </summary>
        public async Task<List<ServerEvent>> GetEventsAsync(int departmentId)
        {
            try
            {
                var response = await httpClient.GetFromJsonAsync<EventsResponse>($"{serverUrl}/api/events/{departmentId}");
                return response?.Events ?? new List<ServerEvent>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이벤트 가져오기 오류: {ex.Message}");
                return new List<ServerEvent>();
            }
        }

        /// <summary>
        /// 이벤트 생성
        /// </summary>
        public async Task<ServerEvent?> CreateEventAsync(int departmentId, string eventDate, string title, 
            string description = "", string time = "", string url = "")
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync($"{serverUrl}/api/events/{departmentId}", new
                {
                    event_date = eventDate,
                    title = title,
                    description = description,
                    time = time,
                    url = url
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CreateEventResponse>();
                    return result?.Event;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이벤트 생성 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 이벤트 수정
        /// </summary>
        public async Task<bool> UpdateEventAsync(int eventId, string? title = null, string? description = null,
            string? time = null, string? url = null, string? eventDate = null)
        {
            try
            {
                var data = new Dictionary<string, string?>();
                if (title != null) data["title"] = title;
                if (description != null) data["description"] = description;
                if (time != null) data["time"] = time;
                if (url != null) data["url"] = url;
                if (eventDate != null) data["event_date"] = eventDate;

                var response = await httpClient.PutAsJsonAsync($"{serverUrl}/api/events/{eventId}", data);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이벤트 수정 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 이벤트 삭제
        /// </summary>
        public async Task<bool> DeleteEventAsync(int eventId)
        {
            try
            {
                var response = await httpClient.DeleteAsync($"{serverUrl}/api/events/{eventId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이벤트 삭제 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 동기화 요청
        /// </summary>
        public async Task RequestSyncAsync()
        {
            if (socket != null && isConnected && currentDepartmentId.HasValue)
            {
                await socket.EmitAsync("sync_request", new { department_id = currentDepartmentId.Value });
            }
        }

        // 이벤트 발생 메서드들
        protected virtual void OnConnected() => Connected?.Invoke(this, EventArgs.Empty);
        protected virtual void OnDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);
        protected virtual void OnEventCreated(ServerEvent serverEvent) => EventCreated?.Invoke(this, new SyncEventArgs(serverEvent));
        protected virtual void OnEventUpdated(ServerEvent serverEvent) => EventUpdated?.Invoke(this, new SyncEventArgs(serverEvent));
        protected virtual void OnEventDeleted(int eventId) => EventDeleted?.Invoke(this, eventId);
        protected virtual void OnSyncReceived(List<ServerEvent> events) => SyncReceived?.Invoke(this, events);
        protected virtual void OnConnectionStatusChanged(string status) => ConnectionStatusChanged?.Invoke(this, status);
    }

    // 데이터 모델
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Created_at { get; set; } = "";
    }

    public class ServerEvent
    {
        public int Id { get; set; }
        public int Department_id { get; set; }
        public string Event_date { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Time { get; set; } = "";
        public string Url { get; set; } = "";
        public string Last_modified { get; set; } = "";
    }

    // 응답 모델
    public class DepartmentResponse
    {
        public bool Success { get; set; }
        public List<Department> Departments { get; set; } = new List<Department>();
    }

    public class CreateDepartmentResponse
    {
        public bool Success { get; set; }
        public Department? Department { get; set; }
    }

    public class EventsResponse
    {
        public bool Success { get; set; }
        public List<ServerEvent> Events { get; set; } = new List<ServerEvent>();
    }

    public class CreateEventResponse
    {
        public bool Success { get; set; }
        public ServerEvent? Event { get; set; }
    }

    public class SyncResponse
    {
        public int Department_id { get; set; }
        public List<ServerEvent> Events { get; set; } = new List<ServerEvent>();
    }

    // 이벤트 인자
    public class SyncEventArgs : EventArgs
    {
        public ServerEvent ServerEvent { get; }

        public SyncEventArgs(ServerEvent serverEvent)
        {
            ServerEvent = serverEvent;
        }
    }
}

