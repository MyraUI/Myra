using System;

namespace Myra.Samples;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		using var game = new CustomBrushesGame();
		game.Run();
	}
}