using Myra.Attributes;

namespace Myra.Samples;

internal class SupersamplingSettings
{
	[Range(2.0f, 8.0f)]
	public float FontResolutionFactor { get; set; } = 4.0f;


	[Range(2.0f, 8.0f)]
	public int KernelWidth { get; set; } = 4;

	[Range(2.0f, 8.0f)]
	public int KernelHeight { get; set; } = 4;
}
