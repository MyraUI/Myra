using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Myra.MML;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
using Texture2D = Stride.Graphics.Texture;
#else
using System.Drawing;
using Texture2D = System.Object;
#endif

namespace Myra.Graphics2D.TextureAtlases
{
	/// <summary>
	/// Represents a collection of texture regions derived from a single texture, with support for both LibGDX and Myra XML formats.
	/// </summary>
	public class TextureRegionAtlas
	{
		/// <summary>
		/// The character used to separate atlas names from region names in atlas references.
		/// </summary>
		internal const char Separator = ':';

		private const string TextureAtlasName = "TextureAtlas";
		private const string ImageName = "Image";
		private const string TextureRegionName = "TextureRegion";
		private const string NinePatchRegionName = "NinePatchRegion";
		private const string LeftName = "Left";
		private const string TopName = "Top";
		private const string WidthName = "Width";
		private const string HeightName = "Height";
		private const string NinePatchLeftName = "NinePatchLeft";
		private const string NinePatchTopName = "NinePatchTop";
		private const string NinePatchRightName = "NinePatchRight";
		private const string NinePatchBottomName = "NinePatchBottom";

		/// <summary>
		/// User-specified name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the image file name or path for this texture atlas.
		/// </summary>
		public string Image { get; set; }

		/// <summary>
		/// Gets the dictionary of texture regions keyed by their identifiers.
		/// </summary>
		public Dictionary<string, TextureRegion> Regions { get; } = new Dictionary<string, TextureRegion>();

		/// <summary>
		/// Gets the texture associated with this atlas.
		/// </summary>
		public Texture2D Texture { get; private set; }

		/// <summary>
		/// Gets or sets a texture region by its identifier.
		/// </summary>
		/// <param name="name">The identifier of the region.</param>
		/// <returns>The texture region with the specified identifier.</returns>
		public TextureRegion this[string name]
		{
			get
			{
				return Regions[name];
			}
			set
			{
				Regions[name] = value;
			}
		}

		/// <summary>
		/// Gets a texture region by its identifier, throwing an exception if not found.
		/// </summary>
		/// <param name="id">The identifier of the region.</param>
		/// <returns>The texture region with the specified identifier.</returns>
		/// <exception cref="ArgumentNullException">The region with the specified identifier is not found.</exception>
		public TextureRegion EnsureRegion(string id)
		{
			TextureRegion result;
			if (!Regions.TryGetValue(id, out result))
			{
				throw new ArgumentNullException(string.Format("Could not resolve region '{0}'", id));
			}

			return result;
		}

		/// <summary>
		/// Attempts to extract a texture region name from an asset name in the format "atlasName:regionName".
		/// </summary>
		/// <param name="assetName">The asset name to parse (e.g., "default_ui_skin.xmat:button"). Will be modified to contain only the atlas name.</param>
		/// <param name="regionName">The extracted region name if found (e.g., "button"); otherwise null.</param>
		/// <returns>True if a region name was found and extracted; otherwise false.</returns>
		internal static bool TryGetRegionName(ref string assetName, out string regionName)
		{
			regionName = null;

			for (var i = 0; i < assetName.Length - 1; ++i)
			{
				if (assetName[i] == Separator && char.IsLetterOrDigit(assetName[i + 1]))
				{
					// Found
					regionName = assetName.Substring(i + 1).Trim();
					assetName = assetName.Substring(0, i).Trim();
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Converts the texture atlas to an XML string representation.
		/// </summary>
		/// <returns>An XML string containing all regions in this atlas.</returns>
		public string ToXml()
		{
			var doc = new XDocument();
			var root = new XElement(TextureAtlasName);
			root.SetAttributeValue(ImageName, Image);
			doc.Add(root);

			foreach (var pair in Regions)
			{
				var region = pair.Value;
				var asNinePatch = region as NinePatchRegion;

				var entry = new XElement(asNinePatch != null ? NinePatchRegionName : TextureRegionName);

				entry.SetAttributeValue(BaseContext.IdName, region.Name);
				entry.SetAttributeValue(LeftName, region.Bounds.Left);
				entry.SetAttributeValue(TopName, region.Bounds.Top);
				entry.SetAttributeValue(WidthName, region.Bounds.Width);
				entry.SetAttributeValue(HeightName, region.Bounds.Height);

				if (asNinePatch != null)
				{
					entry.SetAttributeValue(NinePatchLeftName, asNinePatch.Info.Left);
					entry.SetAttributeValue(NinePatchTopName, asNinePatch.Info.Top);
					entry.SetAttributeValue(NinePatchRightName, asNinePatch.Info.Right);
					entry.SetAttributeValue(NinePatchBottomName, asNinePatch.Info.Bottom);
				}

				root.Add(entry);
			}

			return doc.ToString();
		}

		/// <summary>
		/// Loads a texture atlas from Myra XML format.
		/// </summary>
		/// <param name="xml">The atlas data in Myra XML format.</param>
		/// <param name="textureGetter">A function that retrieves the texture given its filename.</param>
		/// <returns>A new TextureRegionAtlas loaded from the provided XML.</returns>
		public static TextureRegionAtlas FromXml(string xml, Func<string, Texture2D> textureGetter)
		{
			var doc = XDocument.Parse(xml);
			var root = doc.Root;

			var result = new TextureRegionAtlas();
			var imageFileAttr = root.Attribute(ImageName);
			if (imageFileAttr == null)
			{
				throw new Exception("Mandatory attribute 'ImageFile' doesnt exist");
			}

			result.Image = imageFileAttr.Value;

			var texture = textureGetter(result.Image);
			result.Texture = texture;
			foreach (XElement entry in root.Elements())
			{
				var id = entry.Attribute(BaseContext.IdName).Value;

				var bounds = new Rectangle(
					int.Parse(entry.Attribute(LeftName).Value),
					int.Parse(entry.Attribute(TopName).Value),
					int.Parse(entry.Attribute(WidthName).Value),
					int.Parse(entry.Attribute(HeightName).Value)
				);

				var isNinePatch = entry.Name == NinePatchRegionName;

				TextureRegion region;
				if (!isNinePatch)
				{
					region = new TextureRegion(texture, bounds);
				}
				else
				{
					var padding = new Thickness
					{
						Left = int.Parse(entry.Attribute(NinePatchLeftName).Value),
						Top = int.Parse(entry.Attribute(NinePatchTopName).Value),
						Right = int.Parse(entry.Attribute(NinePatchRightName).Value),
						Bottom = int.Parse(entry.Attribute(NinePatchBottomName).Value)
					};

					region = new NinePatchRegion(texture, bounds, padding);
				}

				region.Name = id;

				result[id] = region;
			}

			return result;
		}

		/// <summary>
		/// Returns the name of the texture atlas.
		/// </summary>
		/// <returns>The atlas name.</returns>
		public override string ToString() => Name;
	}
}