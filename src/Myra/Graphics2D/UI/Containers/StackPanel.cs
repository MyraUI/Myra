using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;
using Myra.Attributes;
using Myra.MML;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Base class for containers that arrange children in a stack (either horizontally or vertically).
	/// </summary>
	public abstract class StackPanel : Container
	{
		/// <summary>
		/// Attached property that specifies the proportion type for a child element in a stack panel.
		/// </summary>
		public static readonly AttachedPropertyInfo<ProportionType> ProportionTypeProperty =
			AttachedPropertiesRegistry.Create(typeof(StackPanel), "ProportionType",
				ProportionType.Auto, AttachedPropertyOption.AffectsMeasure);
		/// <summary>
		/// Attached property that specifies the proportion value for a child element in a stack panel.
		/// </summary>
		public static readonly AttachedPropertyInfo<float> ProportionValueProperty =
			AttachedPropertiesRegistry.Create(typeof(StackPanel), "ProportionValue",
				1.0f, AttachedPropertyOption.AffectsMeasure,
				new Attribute[] { new RangeAttribute(0.0f) });

		private readonly StackPanelLayout _layout;
		private readonly ObservableCollection<Proportion> _proportions = new ObservableCollection<Proportion>();
		private bool _childrenDirty = true;

		/// <summary>
		/// Gets the orientation of the stack panel (horizontal or vertical).
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public abstract Orientation Orientation { get; }

		/// <summary>
		/// Gets or sets a value indicating whether grid lines should be shown for debugging.
		/// </summary>
		[Category("Debug")]
		[DefaultValue(false)]
		public bool ShowGridLines { get; set; }

		/// <summary>
		/// Gets or sets the color of the grid lines shown for debugging.
		/// </summary>
		[Category("Debug")]
		[DefaultValue("White")]
		public Color GridLinesColor { get; set; }

		/// <summary>
		/// Gets or sets the spacing between child elements.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(0)]
		public int Spacing
		{
			get => _layout.Spacing;
			set => _layout.Spacing = value;
		}

		/// <summary>
		/// Gets or sets the default proportion for child elements.
		/// </summary>
		[Browsable(false)]
		public Proportion DefaultProportion
		{
			get => _layout.DefaultProportion;
			set => _layout.DefaultProportion = value;
		}

		/// <summary>
		/// Gets the proportions collection that defines how space is distributed among children.
		/// </summary>
		[Browsable(false)]
		[Obsolete("Use StackPanel.GetProportion/StackPanel.SetProportion")]
		[SkipSave]
		public ObservableCollection<Proportion> Proportions => _proportions;

		/// <summary>
		/// Initializes a new instance of the <see cref="StackPanel"/> class.
		/// </summary>
		protected StackPanel()
		{
			_layout = new StackPanelLayout(Orientation);
			ChildrenLayout = _layout;
			GridLinesColor = Color.White;

			_proportions.CollectionChanged += (s, e) => InvalidateChildren();
		}

		/// <summary>
		/// Gets the size of the cell at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the cell.</param>
		/// <returns>The size of the cell.</returns>
		public int GetCellSize(int index) => _layout.GetCellSize(index);

		private void InvalidateChildren()
		{
			_childrenDirty = true;
		}

		/// <summary>
		/// Updates the proportions of child elements in the stack panel.
		/// </summary>
		protected void UpdateChildren()
		{
			if (!_childrenDirty)
			{
				return;
			}

			var index = 0;
			foreach (var widget in ChildrenCopy)
			{
				if (index < _proportions.Count)
				{
					var prop = _proportions[index];
					SetProportionType(widget, prop.Type);
					SetProportionValue(widget, prop.Value);
				}

				++index;
			}

			_childrenDirty = false;
		}

		/// <summary>
		/// Measures the size required for all child elements in the stack panel.
		/// </summary>
		/// <param name="availableSize">The available size for measurement.</param>
		/// <returns>The measured size of the stack panel.</returns>
		protected override Point InternalMeasure(Point availableSize)
		{
			UpdateChildren();

			return base.InternalMeasure(availableSize);
		}

		/// <summary>
		/// Arranges child elements in the stack panel according to the stack orientation.
		/// </summary>
		protected override void InternalArrange()
		{
			UpdateChildren();

			base.InternalArrange();
		}

		/// <summary>
		/// Renders the stack panel and optionally its grid lines for debugging.
		/// </summary>
		/// <param name="context">The render context.</param>
		public override void InternalRender(RenderContext context)
		{
			base.InternalRender(context);

			if (!ShowGridLines)
			{
				return;
			}

			var bounds = ActualBounds;

			int i;
			for (i = 0; i < _layout.GridLinesX.Count; ++i)
			{
				var x = _layout.GridLinesX[i] + bounds.Left;
				context.FillRectangle(new Rectangle(x, bounds.Top, 1, bounds.Height), GridLinesColor);
			}

			for (i = 0; i < _layout.GridLinesY.Count; ++i)
			{
				var y = _layout.GridLinesY[i] + bounds.Top;
				context.FillRectangle(new Rectangle(bounds.Left, y, bounds.Width, 1), GridLinesColor);
			}
		}

		/// <summary>
		/// Handles changes to attached properties on the stack panel.
		/// </summary>
		/// <param name="propertyInfo">Information about the changed property.</param>
		public override void OnAttachedPropertyChanged(BaseAttachedPropertyInfo propertyInfo)
		{
			base.OnAttachedPropertyChanged(propertyInfo);

			if (propertyInfo.Id == ProportionTypeProperty.Id ||
				propertyInfo.Id == ProportionValueProperty.Id)
			{
				InvalidateChildren();
			}
		}

		/// <summary>
		/// Copies properties from another stack panel to this one.
		/// </summary>
		/// <param name="w">The source stack panel to copy from.</param>
		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var stackPanel = (StackPanel)w;

			ShowGridLines = stackPanel.ShowGridLines;
			GridLinesColor = stackPanel.GridLinesColor;
			Spacing = stackPanel.Spacing;
			DefaultProportion = stackPanel.DefaultProportion;
		}

		/// <summary>
		/// Gets the proportion type for a widget in a stack panel.
		/// </summary>
		/// <param name="widget">The widget.</param>
		/// <returns>The proportion type of the widget.</returns>
		public static ProportionType GetProportionType(Widget widget) => ProportionTypeProperty.GetValue(widget);

		/// <summary>
		/// Sets the proportion type for a widget in a stack panel.
		/// </summary>
		/// <param name="widget">The widget.</param>
		/// <param name="value">The proportion type to set.</param>
		public static void SetProportionType(Widget widget, ProportionType value) => ProportionTypeProperty.SetValue(widget, value);

		/// <summary>
		/// Gets the proportion value for a widget in a stack panel.
		/// </summary>
		/// <param name="widget">The widget.</param>
		/// <returns>The proportion value of the widget.</returns>
		public static float GetProportionValue(Widget widget) => ProportionValueProperty.GetValue(widget);

		/// <summary>
		/// Sets the proportion value for a widget in a stack panel.
		/// </summary>
		/// <param name="widget">The widget.</param>
		/// <param name="value">The proportion value to set.</param>
		public static void SetProportionValue(Widget widget, float value) => ProportionValueProperty.SetValue(widget, value);
	}
}
