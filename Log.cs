using System;

namespace AurLand;

public static class Log
{
	public static void WriteLine(string message)
	{
		Console.WriteLine($"{message}");
	}
	public static void InfoLine(string message)
	{
		Console.ForegroundColor = ConsoleColor.Blue;
		Console.Write("Info: ");
		Console.ResetColor();
		
		Console.WriteLine($"\t{message}");
	}
	public static void WarningLine(string message)
	{
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine("Warning: ");
		Console.ResetColor();
		
		Console.WriteLine($"\t{message}");
	}
	public static void ErrorLine(string message)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("ERROR: ");
		Console.ResetColor();
		
		Console.Error.WriteLine($"\t{message}");
	}
}
