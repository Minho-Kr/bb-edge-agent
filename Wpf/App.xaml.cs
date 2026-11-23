using System;
using System.Windows;
using System.Windows.Threading;
using BBEdgeAgent.Services;
using BBEdgeAgent.Wpf.Views;

namespace BBEdgeAgent.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // LoginWindow 닫힐 때 자동 종료 방지
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 전역 예외 핸들러 — 실제 오류 메시지를 화면에 표시
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            try
            {
                var cfg = AppConfig.Load();
                L.Current = cfg.Language;

                var db         = new LocalDbService(AppConfig.DbPath);
                var supabase   = new SupabaseService(cfg);
                var rfid       = new RfidService(cfg);
                var attendance = new AttendanceService(db, supabase, cfg);

                var login = new LoginWindow(cfg);
                if (login.ShowDialog() != true)
                {
                    db.Dispose();
                    rfid.Dispose();
                    Shutdown();
                    return;
                }

                var main = new MainWindow(cfg, rfid, supabase, db, attendance);
                main.Closed += (_, __) =>
                {
                    rfid.Dispose();
                    db.Dispose();
                    Shutdown();
                };
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"시작 오류:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "BB Edge Agent — 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"UI 오류:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "BB Edge Agent — UI 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"치명적 오류:\n\n{ex?.GetType().Name}: {ex?.Message}\n\n{ex?.StackTrace}",
                "BB Edge Agent — 치명적 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
