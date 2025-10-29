using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace AgentAssistant
{
    /// <summary>
    /// JSON 파일 기반 부서 관리 클래스
    /// </summary>
    public class DepartmentManager
    {
        private string filePath = "departments.json";
        private List<Department> departments = new List<Department>();

        public DepartmentManager()
        {
            LoadDepartments();
        }

        /// <summary>
        /// 부서 목록 로드
        /// </summary>
        public void LoadDepartments()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var data = JsonSerializer.Deserialize<DepartmentData>(json);
                    departments = data?.Departments ?? new List<Department>();
                }
                else
                {
                    // 기본 부서 목록 생성
                    departments = GetDefaultDepartments();
                    SaveDepartments();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 목록 로드 오류: {ex.Message}");
                departments = GetDefaultDepartments();
            }
        }

        /// <summary>
        /// 부서 목록 저장
        /// </summary>
        public void SaveDepartments()
        {
            try
            {
                var data = new DepartmentData { Departments = departments };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 목록 저장 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 부서 목록 반환
        /// </summary>
        public List<Department> GetAllDepartments()
        {
            return departments.ToList();
        }

        /// <summary>
        /// ID로 부서 찾기
        /// </summary>
        public Department? GetDepartmentById(int id)
        {
            return departments.FirstOrDefault(d => d.Id == id);
        }

        /// <summary>
        /// 새 부서 추가
        /// </summary>
        public bool AddDepartment(string name, string description = "")
        {
            try
            {
                var newId = departments.Count > 0 ? departments.Max(d => d.Id) + 1 : 1;
                var department = new Department
                {
                    Id = newId,
                    Name = name,
                    Description = description,
                    Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                departments.Add(department);
                SaveDepartments();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 추가 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 부서 삭제
        /// </summary>
        public bool DeleteDepartment(int id)
        {
            try
            {
                var department = departments.FirstOrDefault(d => d.Id == id);
                if (department != null)
                {
                    departments.Remove(department);
                    SaveDepartments();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"부서 삭제 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 기본 부서 목록 생성
        /// </summary>
        private List<Department> GetDefaultDepartments()
        {
            return new List<Department>
            {
                new Department { Id = 1, Name = "개발팀", Description = "소프트웨어 개발", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new Department { Id = 2, Name = "마케팅팀", Description = "마케팅 및 홍보", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new Department { Id = 3, Name = "영업팀", Description = "고객 영업", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new Department { Id = 4, Name = "인사팀", Description = "인사 관리", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new Department { Id = 5, Name = "재무팀", Description = "재무 관리", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new Department { Id = 6, Name = "기획팀", Description = "사업 기획", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new Department { Id = 7, Name = "디자인팀", Description = "UI/UX 디자인", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new Department { Id = 8, Name = "QA팀", Description = "품질 보증", Created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };
        }
    }

    /// <summary>
    /// JSON 직렬화를 위한 데이터 클래스
    /// </summary>
    public class DepartmentData
    {
        public List<Department> Departments { get; set; } = new List<Department>();
    }
}


