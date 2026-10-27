namespace ResolutionAssistant;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\ResolutionAssistant.v1", out bool first);
        using var activate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ResolutionAssistant.Activate.v1");
        if (!first)
        {
            activate.Set();
            return;
        }
        try
        {
            var config = ConfigStore.Load();
            using var form = new MainForm(config, args.Contains("--background", StringComparer.OrdinalIgnoreCase));
            _ = form.Handle;
            var registration = ThreadPool.RegisterWaitForSingleObject(activate, (_, _) =>
            {
                if (form.IsDisposed || !form.IsHandleCreated) return;
                try { form.BeginInvoke(form.ShowWindow); }
                catch (InvalidOperationException) { /* The window was closed during activation. */ }
            }, null, Timeout.Infinite, false);
            try { Application.Run(form); }
            finally { registration.Unregister(null); }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法启动分辨率助手。\n\n{ex.Message}\n\n配置位置：{ConfigStore.FilePath}",
                "分辨率助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { mutex.ReleaseMutex(); }
    }
}
