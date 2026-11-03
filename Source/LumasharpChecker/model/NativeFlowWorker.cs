using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LumasharpChecker.model;

// One serialized, parent-owned worker; native DLLs remain outside the WPF process.
internal static class NativeFlowWorker
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object Sync = new();
    private static Process? _process;
    private static string? _runtime;
    private static string? _executableStamp;
    private static Task? _stderr;

    internal static async Task<NativeRecipeBridge.StageReport> InspectAsync(NativeRecipeBridge.PreparedRecipe native,
        int station, string image, string result, string stopAfter, string? selection)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Process process;
            lock (Sync)
            {
                string exe = Path.Combine(native.NativeRoot, "LumaNativeFlowHost.exe");
                if (!File.Exists(exe)) throw new FileNotFoundException("缺少隔離原生流程執行器。", exe);
                var file = new FileInfo(exe); string stamp = file.Length + ":" + file.LastWriteTimeUtc.Ticks;
                if (_process == null || _process.HasExited || _runtime != native.NativeRoot || _executableStamp != stamp)
                {
                    StopUnderLock();
                    var info = new ProcessStartInfo(exe, "--worker") { WorkingDirectory = native.NativeRoot,
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                    _process = Process.Start(info) ?? throw new IOException("無法啟動原生常駐程序。");
                    _stderr = DrainErrorsAsync(_process.StandardError);
                    _runtime = native.NativeRoot; _executableStamp = stamp;
                }
                process = _process;
            }
            string id = Guid.NewGuid().ToString("N");
            var args = selection == null ? new[] { native.Name, Path.GetFullPath(image), result, station.ToString(), stopAfter }
                : new[] { native.Name, Path.GetFullPath(image), result, station.ToString(), stopAfter, selection };
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { Id = id, Revision = native.Revision, Args = args })).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            var response = process.StandardOutput.ReadLineAsync();
            if (await Task.WhenAny(response, Task.Delay(25000)).ConfigureAwait(false) != response)
                throw new TimeoutException("原生流程逾時，已停止此隔離程序；下次檢測會重新啟動。");
            string? line = await response.ConfigureAwait(false);
            if (string.IsNullOrEmpty(line))
                throw new IOException("原生常駐程序已中斷，未收到本次結果。");
            var report = JsonSerializer.Deserialize<NativeRecipeBridge.StageReport>(line,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("原生流程回傳空結果。");
            if (report.RequestId != id || report.Recipe != native.Name || report.Station != station ||
                report.Image != Path.GetFileName(image) || report.WorkerProcessId != process.Id || report.AiExecuted)
            {
                Trace.WriteLine("Native worker response rejected: " + line);
                throw new InvalidDataException("原生回傳的工作／配方／站別／圖片不一致，結果作廢。");
            }
            if (report.Exit != 0 && report.Exit != 2)
                throw new IOException("原生流程無法完成：" + report.Error);
            return report;
        }
        catch { Shutdown(); throw; }
        finally { Gate.Release(); }
    }

    internal static void Shutdown() { lock (Sync) StopUnderLock(); }
    private static async Task DrainErrorsAsync(StreamReader reader)
    {
        // A persistent process must not accumulate an unbounded stderr string.
        var buffer = new char[1024];
        try { while (await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0) { } }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }
    private static void StopUnderLock()
    {
        var process = _process; _process = null; _runtime = _executableStamp = null; _stderr = null;
        if (process == null) return;
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.Close();
                if (!process.WaitForExit(500)) { process.Kill(); process.WaitForExit(2000); }
            }
        }
        catch (InvalidOperationException) { }
        finally { process.Dispose(); }
    }
}
