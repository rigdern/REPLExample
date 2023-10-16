using MyApp.REPLServer.WebSocketServerNamespace;
using System.Reflection;
using System.Text.Json;

namespace MyApp.REPLServer
{
    /// <summary>
    /// The REPL Server. Listens on a WebSocket for the "evalDll" command and handles it by loading
    /// the specified C# DLL/assembly and executing its `EvalMain` method.
    /// </summary>
    internal class EvalServer : IDisposable
    {
        private const string evalClassName = "Eval.EvalProgram";
        private const string evalMethodName = "EvalMain";

        // Gives the caller the opportunity to preserve state across evals.
        private readonly IDictionary<string, object> _state = new Dictionary<string, object>();

        /// <summary>
        /// Loads the C# DLL/assembly located at `dllPath` and executes its `EvalMain` method.
        /// </summary>
        public void EvalDll(string dllPath)
        {
            try
            {
                var assemblyBytes = File.ReadAllBytes(dllPath);
                var pdbBytes = LoadPdbIfExists(dllPath); // Enables us to see line numbers in exceptions
                var assembly = Assembly.Load(assemblyBytes, pdbBytes);

                var type = assembly.GetType(evalClassName);
                if (type == null)
                {
                    LogInfo("EvalDll error: Could not find class " + evalClassName + " in DLL.");
                    return;
                }

                var method = type.GetMethod(evalMethodName);
                if (method == null)
                {
                    LogInfo("EvalDll error: Could not find method " + evalMethodName + " in class " + evalClassName + " in DLL.");
                    return;
                }

                method.Invoke(null, new object[] { _state });
            }
            catch (Exception ex)
            {
                LogInfo("EvalDll exception: " + ex.ToString());
                if (ex.InnerException != null)
                {
                    LogInfo("EvalDll inner exception: " + ex.InnerException.ToString());
                }
            }
        }

        private static byte[]? LoadPdbIfExists(string dllPath)
        {
            var pdbPath = Path.ChangeExtension(dllPath, "pdb");
            return !File.Exists(pdbPath) ? null :
                File.ReadAllBytes(pdbPath);
        }

        private static void LogInfo(string value) => Console.WriteLine(value);

        #region Initialization & cleanup

        private readonly WebSocketServer _webSocketServer;
        private Action _unregisterWebSocket;

        public EvalServer(WebSocketServer webSocketServer)
        {
            _webSocketServer = webSocketServer;
        }

        public void Initialize()
        {
            _unregisterWebSocket = _webSocketServer.Register(OnWebSocketMessage);
        }

        // Lifecycle hook for destruction called by the dependency injection framework.
        public void Dispose()
        {
            _unregisterWebSocket();
        }

        #endregion

        #region WebSocket message handler

        private bool OnWebSocketMessage(string cmdName, string cmdArgs)
        {
            switch (cmdName)
            {
                case "evalDll":
                    {
                        var args = Deserialize<EvalArgs>(cmdArgs);
                        EvalDll(args.DllPath);
                        return true;
                    }
                default:
                    return false;
            }
        }

        private T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    });
        }

        private class EvalArgs
        {
            public string? DllPath { get; set; }
        }

        #endregion
    }
}
