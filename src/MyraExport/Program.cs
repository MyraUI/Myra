using System;
using System.IO;
using AssetManagementBase;
using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D.UI;
using MyraPad;

namespace MyraExport
{
	internal static class Program
	{
		private sealed class HeadlessGame : Game
		{
			public HeadlessGame()
			{
				var graphics = new GraphicsDeviceManager(this);
				((IGraphicsDeviceManager)Services.GetService(typeof(IGraphicsDeviceManager))).CreateDevice();
			}
		}

		private static void PrintUsage()
		{
			Console.WriteLine("Usage: myra-export <file.xmmp> [options]");
			Console.WriteLine();
			Console.WriteLine("Exports a Myra .xmmp UI project to a C# main file and a designer file.");
			Console.WriteLine();
			Console.WriteLine("Options:");
			Console.WriteLine("  --namespace <ns>   Namespace of the generated classes (overrides the project's ExportOptions)");
			Console.WriteLine("  --class <name>     Name of the generated classes (overrides the project's ExportOptions)");
			Console.WriteLine("  --output <dir>     Output directory (overrides the project's ExportOptions)");
		}

		private static string GetOptionValue(string[] args, string name)
		{
			for (var i = 0; i < args.Length - 1; ++i)
			{
				if (args[i] == name)
				{
					return args[i + 1];
				}
			}

			return null;
		}

		private static int Main(string[] args)
		{
			try
			{
				if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
				{
					PrintUsage();
					return args.Length == 0 ? 1 : 0;
				}

				var file = args[0];
				if (!File.Exists(file))
				{
					throw new FileNotFoundException($"Could not find '{file}'.");
				}

				using (var game = new HeadlessGame())
				{
					MyraEnvironment.Game = game;

					var fullPath = Path.GetFullPath(file);
					var directory = Path.GetDirectoryName(fullPath);
					var assetManager = AssetManager.CreateFileAssetManager(directory);
					var project = Project.LoadFromXml(File.ReadAllText(fullPath), assetManager);

					// Fall back to sensible defaults when the project does not define export options
					var options = project.ExportOptions;
					options.Namespace = GetOptionValue(args, "--namespace") ?? options.Namespace ?? "MyraExport";
					options.Class = GetOptionValue(args, "--class") ?? options.Class ?? Path.GetFileNameWithoutExtension(file);
					options.OutputPath = GetOptionValue(args, "--output") ?? options.OutputPath ?? directory;

					Directory.CreateDirectory(options.OutputPath);

					using (var exporter = new ExporterCS(project))
					{
						var written = exporter.Export();
						if (written.Length == 0)
						{
							Console.WriteLine("Nothing to export.");
							return 0;
						}

						Console.WriteLine("Success. Following files had been written:");
						foreach (var path in written)
						{
							Console.WriteLine(path);
						}
					}
				}

				return 0;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Error: " + ex.Message);
				return 1;
			}
		}
	}
}