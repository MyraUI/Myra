using AssetManagementBase;
using System;

namespace Myra.Samples;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		AMBConfiguration.Logger = Console.WriteLine;
		using var game = new CustomBrushesGame();
		game.Run();
	}
}