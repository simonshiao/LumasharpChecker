using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Cyntec.Logger;
using Emgu.CV.Cuda;
using NLog;
using static LumasharpChecker.model.Listener;

namespace LumasharpChecker.model
{
    public class Listener
    {
        private TcpListener server;
        private int _port;
        public Listener(int port = 5000) 
        {
            server = new TcpListener(IPAddress.Any, port);
            _port = port;
        }

        public async void Start()
        {
            if (server != null)
            {
                server.Start();
                Logmanager.loger.Trace($"Server is listening on port {_port}");
                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                int byteRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string requestJson = Encoding.UTF8.GetString(buffer, 0 , byteRead);

                if (string.IsNullOrEmpty(requestJson)) return;

                Logmanager.loger.Trace($"Received: {requestJson}");
                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 禁用非必要转义
                };
                RequestJsonModel? request = new RequestJsonModel();
                try
                {
                    request = JsonSerializer.Deserialize<RequestJsonModel>(requestJson);
                    if (request is not null)
                    {
                        var response = await ProcessRequestAsync(request);
                        string responseJson1 = JsonSerializer.Serialize(response, options);
                        byte[] responseBytes1 = Encoding.UTF8.GetBytes(responseJson1);
                        await stream.WriteAsync(responseBytes1, 0, responseBytes1.Length);
                    }
                }
                catch (Exception ex)
                {
                    var error = new ErrorJsonModel { ERROR = ex.Message };
                    string responseJson = JsonSerializer.Serialize(error, options);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    Logmanager.loger.Error($"{ex}");
                }
            }
            client.Close();
        }

        private async Task<ResponseJsonModel> ProcessRequestAsync(RequestJsonModel request)
        {
            return await Task.Run(() =>
            {
                ImageProcessor processor = new ImageProcessor();
                ImageProcessor.ProcessResult res = processor.Process(request);

                var _timestamp = DateTime.Now.ToString(@"yyyy-MM-dd HH:mm:ss.ff");

                return new ResponseJsonModel
                {
                    message_id = request.message_id,
                    timestamp = _timestamp,
                    brightness = res.Brightness,
                    brightness_max = res.BrightnessMax,
                    brightness_min = res.BrightnessMin,
                    brightness_safe = res.BrightnessSafe,
                    sharpness = res.Sharpness,
                    sharpness_min = res.SharpnessMin,
                    sharpness_safe = res.SharpnessSafe,
                    is_matched = res.IsMatched,
                    roi_coords = res.RoiCoords.ToString()
                };
            }).ConfigureAwait(false);
        }

        private class ErrorJsonModel
        {
            public string? ERROR { get; set; }
        }

        public class RequestJsonModel
        {
            public string? type { get; set; }
            public string? message_id { get; set; }
            public string? image_data { get; set; }
            public string? profile { get; set; }
        }

        public class ResponseJsonModel
        {
            public string? type { get; set; }
            public string? message_id { get; set; }
            public string? timestamp { get; set; }
            public string? station_id { get; set; }
            public double brightness { get; set; }
            public double sharpness { get; set; }
            public bool brightness_safe { get; set; }
            public bool sharpness_safe { get; set; }
            public string? roi_coords { get; set; }
            public bool is_matched { get; set; }
            public double? brightness_max { get; set; }
            public double? brightness_min { get; set; }
            public double? sharpness_min { get; set; }
            public string? profile { get; set; }
        }
    }

    public class processInfo : ObservableObject
    {
        public string? MessageID { get; set; }
        public string? MessageType { get; set; }
        public string? Status { get; set; }
        public string? ReceivedString { get; set; }
        public string? SendString { get; set; }
        public string? Profile { get; set; }
        public bool BrightnessSafe { get; set; }
        public bool SharpnessSafe { get; set; }
        public bool IsMatched { get; set; }
    }

    public class AsyncTcpServer : IDisposable
    {
        private readonly TcpListener _server;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _concurrencyLimiter = new(Environment.ProcessorCount * 2); // 併發控制
        private const char MessageDelimiter = '\n';  // 自訂訊息分隔符
        // 定義狀態變化事件
        public event Action<string> ListerStatusChanged;
        public event Action<processInfo> ProcessStatusChanged;

        private string _currentStatus;
        public string CurrentStatus
        {
            get => _currentStatus;
            private set
            {
                if (_currentStatus != value)
                {
                    _currentStatus = value;
                    ListerStatusChanged?.Invoke(value); // 觸發事件
                }
            }
        }

        private processInfo _processStatus;
        public processInfo ProcessStatus
        {
            get => _processStatus;
            private set
            {
                if (_processStatus != value)
                {
                    _processStatus = value;
                    ProcessStatusChanged?.Invoke(value); // 觸發事件
                }
            }
        }

        public AsyncTcpServer(int port = 5000)
        {
            _server = new TcpListener(IPAddress.Any, port);
        }

        public async Task StartAsync()
        {
            CurrentStatus = "啟動中";
            _server.Start();
            Logmanager.loger.Info($"Server started on port {((IPEndPoint)_server.LocalEndpoint).Port}");
            CurrentStatus = "運作中(Host未連線)";
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var client = await _server.AcceptTcpClientAsync();
                    _ = ProcessClientAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
                CurrentStatus = "已停止";
                // 正常停止
            }
            catch (SocketException) when (_cts.IsCancellationRequested)
            {
                CurrentStatus = "已停止";
            }
            catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
            {
                CurrentStatus = "已停止";
            }
        }

        private async Task ProcessClientAsync(TcpClient client)
        {
            await _concurrencyLimiter.WaitAsync();
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[4096];
                    var memoryStream = new MemoryStream();

                    while (client.Connected && !_cts.IsCancellationRequested)
                    {
                        CurrentStatus = "已連線";
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);

                        string requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                       // string debugstr = requestJson.Length > 60 ? requestJson.Substring(0, 60) : requestJson;

                       // Logmanager.loger.Trace($"-> {debugstr}");

                        if (bytesRead == 0) break; // 客戶端正常關閉

                        memoryStream.Write(buffer, 0, bytesRead);

                        // 處理可能的多個 JSON 指令 (假設以 \n 分隔)
                        while (TryReadJsonMessage(memoryStream, out string json))
                        {
                            int debLength = 100;
                            string debugstr = json.Length > debLength ? json.Substring(0, debLength) : requestJson;
                            //Logmanager.loger.Trace($"-> {debugstr}");
                            Logmanager.loger.Trace($"Process json: {debugstr}...");
                            _ = ProcessSingleMessageAsync(stream, json);
                            //var response = await ProcessJsonCommandAsync(json);

                            //// 序列化回應
                            //string responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                            //byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson + "\n"); // 添加訊息邊界

                            //await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                            //Logmanager.loger.Trace($"<- {responseJson}");
                            // 非阻塞式寫入+流量控
                            //await WriteWithBackpressure(stream, responseBytes);
                        }
                    }
                    //CurrentStatus = "運作中(Host未連線)";
                }
            }
            catch (Exception ex)
            {
                Logmanager.loger.Error($"Client handling error: {ex.Message}");
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        private async Task ProcessSingleMessageAsync(NetworkStream stream, string jsonMessage)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // 業務邏輯處理
                var response = await ProcessJsonCommandAsync(jsonMessage);

                // 非阻塞回應寫入
                string responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

                string s = responseJson + MessageDelimiter.ToString();
                byte[] responseData = Encoding.UTF8.GetBytes(s);
                await stream.WriteAsync(responseData, 0, responseData.Length);

                Logmanager.loger.Trace($"<- {responseJson}");
                Logmanager.loger.Trace($"Processed message in {stopwatch.ElapsedMilliseconds}ms, profile: {response.profile}");
                ProcessStatus = new processInfo() { MessageID = response.message_id, Profile = response.profile, MessageType = "C", Status = "Complete",
                                                    BrightnessSafe = response.brightness_safe, SharpnessSafe = response.sharpness_safe, IsMatched = response.is_matched};

            }
            catch (Exception ex)
            {
                Logmanager.loger.Error($"Message processing failed: {ex.Message}");
                //await SendErrorResponse(stream, ex);
            }
        }

        // 流量控制寫入
        private async Task WriteWithBackpressure(NetworkStream stream, byte[] data)
        {
            int totalSent = 0;
            while (totalSent < data.Length)
            {
                int chunkSize = Math.Min(data.Length - totalSent, 4096);
                await stream.WriteAsync(data, totalSent, chunkSize);
                totalSent += chunkSize;

                // NetworkStream.WriteAsync 會自行等待網路寫入，無需讀取 Socket。
            }
        }

        private bool TryReadJsonMessage(MemoryStream bufferStream, out string json)
        {
            var data = bufferStream.ToArray();
            int endIndex = Array.IndexOf(data, (byte)MessageDelimiter, 0);
            if (endIndex == -1)
            {
                json = null;
                return false;
            }

            json = Encoding.UTF8.GetString(data, 0, endIndex);
            int remainingBytes = data.Length - endIndex - 1;

            // 重置緩衝區
            bufferStream.SetLength(0);
            bufferStream.Write(data, endIndex + 1, remainingBytes);
            return true;
        }

        private async Task<ResponseJsonModel> ProcessJsonCommandAsync(string json)
        {
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 禁用非必要转义
            };
            RequestJsonModel? request = new RequestJsonModel();
            try
            {
                request = JsonSerializer.Deserialize<RequestJsonModel>(json);
                if (request is not null)
                {
                    return await ProcessRequestAsync(request);
                    //var response = await ProcessRequestAsync(request);
                    //string responseJson1 = JsonSerializer.Serialize(response, options);
                    //byte[] responseBytes1 = Encoding.UTF8.GetBytes(responseJson1);
                    //await stream.WriteAsync(responseBytes1, 0, responseBytes1.Length);
                }
            }
            catch (Exception ex)
            {
                Logmanager.loger.Error($"Invalid JSON format: {ex}");
            }
            return new ResponseJsonModel();
        }

        private async Task<ResponseJsonModel> ProcessRequestAsync(RequestJsonModel request)
        {
            ProcessStatus = new processInfo() { MessageID = request.message_id, Profile = request.profile, MessageType = "C", Status = "Received"  };

            return await Task.Run(() =>
            {
                ImageProcessor processor = new ImageProcessor();
                ProcessStatus = new processInfo() { MessageID = request.message_id, Profile = request.profile, MessageType = "C", Status = "Processing" };
                ImageProcessor.ProcessResult res = processor.Process(request);
                ProcessStatus = new processInfo() { MessageID = request.message_id, Profile = request.profile, MessageType = "C", Status = "Processed" };
                var _timestamp = DateTime.Now.ToString(@"yyyy-MM-dd HH:mm:ss.fff");
                //var sss = GetRandomInt();
                //Logmanager.loger.Trace($"MId: {request.message_id} sleep :{sss}");
                //Thread.Sleep(1000*sss);
                return new ResponseJsonModel
                {
                    message_id = request.message_id,
                    timestamp = _timestamp,
                    brightness = res.Brightness,
                    brightness_max = res.BrightnessMax,
                    brightness_min = res.BrightnessMin,
                    brightness_safe = res.BrightnessSafe,
                    sharpness = res.Sharpness,
                    sharpness_min = res.SharpnessMin,
                    sharpness_safe = res.SharpnessSafe,
                    is_matched = res.IsMatched,
                    roi_coords = res.RoiCoords.ToString(),
                    profile = request.profile
                };
            }).ConfigureAwait(false);
        }

        public void Stop()
        {
            _cts.Cancel();
            _server.Stop(); // .NET Framework 的 AcceptTcpClientAsync 不支援 CancellationToken。
        }

        public void Dispose()
        {
            _server?.Stop();
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }

        // 使用 ThreadLocal 解決 Random 的執行緒安全問題
        private static readonly ThreadLocal<Random> _random =
            new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        public int GetRandomInt()
        {
            return _random.Value.Next(1, 11); // 11 是 exclusive 上限
        }
    }
}