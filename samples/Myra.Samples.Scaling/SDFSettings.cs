using Myra.Attributes;

namespace Myra.Samples;

internal class SDFSettings
{
	[Range(32, 128)]
	public float? FixedFontSize { get; set; } = 64.0f;

	public bool Supersampling { get; set; } = true;
}
