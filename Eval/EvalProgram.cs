namespace Eval
{
    public class EvalProgram
    {
        // `EvalMain` is intended to be used like a REPL. While `MyApp` is running, write code in
        // `EvalMain` and then build the "Eval" project to have it injected into and executed in the
        // running `MyApp` program.
        public static void EvalMain(IDictionary<string, object> state)
        {
            Console.WriteLine($"value: {MyApp.Program.GetSomeValue()}");
        }
    }
}
