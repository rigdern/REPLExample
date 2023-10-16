using MyApp.REPLServer;
using MyApp.REPLServer.WebSocketServerNamespace;

namespace MyApp
{
    internal class Program
    {
        private static WebSocketServer? _webSocketServer;
        private static EvalServer? _evalServer;

        public static void Main(string[] args)
        {
            InitializeREPL();

            Console.WriteLine("Hello, World!");
            Console.ReadLine();

            DisposeREPL();
        }

        public static int GetSomeValue()
        {
            return 84;
        }

        private static void InitializeREPL()
        {
            _webSocketServer = new WebSocketServer();
            _webSocketServer.Initialize();

            _evalServer = new EvalServer(_webSocketServer);
            _evalServer.Initialize();
        }

        private static void DisposeREPL()
        {
            _evalServer?.Dispose();
            _webSocketServer?.Dispose();
        }
    }
}
