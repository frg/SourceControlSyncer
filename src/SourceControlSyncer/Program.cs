using System;
using PowerArgs;

namespace SourceControlSyncer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Args.InvokeAction<ConsoleApi>(args);
        }
    }
}
