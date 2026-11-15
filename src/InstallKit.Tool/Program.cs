using System;

namespace InstallKit.Tool
{
    static class Program
    {
        static int Main(string[] args) => Commands.Run(args, Console.Out, Console.Error);
    }
}
