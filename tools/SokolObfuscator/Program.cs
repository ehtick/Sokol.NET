using CommandLine;

namespace SokolObfuscator
{
    static class Program
    {
        static int Main(string[] args)
        {
            int exitCode = 1;
            Parser.Default.ParseArguments<ObfuscationOptions>(args)
                .WithParsed(opts => exitCode = new Obfuscator(opts).Run())
                .WithNotParsed(_ => exitCode = 1);
            return exitCode;
        }
    }
}
