using System;
using System.Threading.Tasks;

namespace SafePipeline.Tests
{
    public static class TestHelpers
    {
        public static T ThrowNotImplemented<T>(T context)
        {
            throw new NotImplementedException();
        }

        public static string AddStringValue(string context) => $"{context}_Updated";

        public static Operable<string> YesNo(Operable<string> context) => 
            string.Compare(context, "Yes", StringComparison.CurrentCultureIgnoreCase) == 0 
                ? (Operable<string>)new Ok<string>(context.Value) 
                : new Skip<string>();

        public static async Task<string> WaitForIt(string context)
        {
            await Task.Delay(250);
            return await Task.Run(() => $"{context}_Waited");
        }
    }

    public class Monitor
    {
        public bool Success { get; set; }
        public bool Failure { get; set; }
        public bool Skip { get; set; }
    }
}