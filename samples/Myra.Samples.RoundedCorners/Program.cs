using AssetManagementBase;
using System;

namespace Myra.Samples;

class Program
{
	static void Main(string[] args)
	{
		AMBConfiguration.Logger = Console.WriteLine;
		using (var game = new RoundedCornersGame())
			game.Run();
	}
}