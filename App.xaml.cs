using System;
using System.Windows;
using System.Windows.Threading;

namespace AgentAssistant
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 예외 처리 핸들러 등록
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            // 애플리케이션 시작 시 초기화 작업
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            // 평문 쿠키 파일을 암호화 파일로 자동 마이그레이션
            CookieMigrationHelper.AutoMigrate();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"오류가 발생했습니다:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "에러",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"심각한 오류가 발생했습니다:\n\n{exception?.Message}\n\n{exception?.StackTrace}",
                "심각한 에러",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}


