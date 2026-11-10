using Fluid.Tool;

var stdin = Console.IsInputRedirected ? Console.In : TextReader.Null;
return await LiquidTool.RunAsync(args, stdin, Console.Out, Console.Error);
