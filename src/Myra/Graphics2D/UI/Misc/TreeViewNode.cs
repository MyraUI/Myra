using System;
using Myra.Graphics2D.UI.Styles;
using Myra.Events;


#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// A node in a tree view that can contain content and have child nodes.
	/// </summary>
	public class TreeViewNode : ContentControl, ITreeViewNode
	{
		private readonly GridLayout _layout = new GridLayout();
		private readonly TreeView _topTree;
		private readonly VerticalStackPanel _childNodesStackPanel;
		private readonly ToggleButton _mark;
		private Widget _content;

		/// <summary>
		/// Gets or sets a value indicating whether the node is expanded to show its child nodes.
		/// </summary>
		public bool IsExpanded
		{
			get { return _mark.IsPressed; }

			set { _mark.IsPressed = value; }
		}

		/// <summary>
		/// Gets the toggle button used to expand or collapse the node.
		/// </summary>
		public ToggleButton Mark => _mark;

		internal VerticalStackPanel ChildNodesGrid => _childNodesStackPanel;

		/// <summary>
		/// Gets the height of the node's content area in pixels.
		/// </summary>
		public int ContentHeight => _layout.GetRowHeight(0);

		/// <summary>
		/// Gets the number of child nodes under this node.
		/// </summary>
		public int ChildNodesCount => _childNodesStackPanel.Children.Count;

		internal bool RowVisible { get; set; }

		/// <summary>
		/// Gets or sets the parent node of this node in the hierarchy.
		/// </summary>
		public TreeViewNode ParentNode { get; internal set; }

		/// <summary>
		/// Gets or sets the widget displayed as the node's content.
		/// </summary>
		public override Widget Content
		{
			get => _content;

			set
			{
				if (_content == value)
				{
					return;
				}

				if (_content != null)
				{
					Children.Remove(_content);
				}

				_content = value;

				if (_content != null)
				{
					Grid.SetColumn(_content, 1);
					Children.Add(_content);
				}
			}
		}


		internal TreeViewNode(TreeView topTree, string styleName = Stylesheet.DefaultStyleName)
		{
			_layout.ColumnSpacing = 2;
			_layout.RowSpacing = 2;
			ChildrenLayout = _layout;

			_topTree = topTree;

			if (_topTree != null)
			{
				_topTree.AllNodes.Add(this);
			}

			_mark = new ToggleButton(null)
			{
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Content = new Image()
			};

			_mark.PressedChanged += (s, a) =>
			{
				_childNodesStackPanel.Visible = _mark.IsPressed;
			};

			Children.Add(_mark);

			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;

			_layout.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
			_layout.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

			_layout.RowsProportions.Add(new Proportion(ProportionType.Auto));
			_layout.RowsProportions.Add(new Proportion(ProportionType.Auto));

			// Second is yet another grid holding child nodes
			_childNodesStackPanel = new VerticalStackPanel
			{
				Visible = false,
			};
			Grid.SetColumn(_childNodesStackPanel, 1);
			Grid.SetRow(_childNodesStackPanel, 1);

			Children.Add(_childNodesStackPanel);

			SetStyle(styleName);

			UpdateMark();
		}

		/// <summary>
		/// Updates the visibility of the mark (expand/collapse button) based on the number of child nodes.
		/// </summary>
		protected virtual void UpdateMark()
		{
			_mark.Visible = _childNodesStackPanel.Children.Count > 0;
		}

		/// <summary>
		/// Removes all child nodes from this node and updates the mark visibility.
		/// </summary>
		public virtual void RemoveAllSubNodes()
		{
			_childNodesStackPanel.Children.Clear();
			UpdateMark();
		}

		/// <summary>
		/// Adds a new child node to this node with the specified content.
		/// </summary>
		/// <param name="content">The widget to display as the child node's content.</param>
		/// <returns>The newly created child node.</returns>
		public TreeViewNode AddSubNode(Widget content)
		{
			var result = new TreeViewNode(_topTree, StyleName)
			{
				ParentNode = this,
				Content = content
			};
			Grid.SetRow(result, _childNodesStackPanel.Children.Count);

			_childNodesStackPanel.Children.Add(result);

			UpdateMark();

			return result;
		}

		/// <summary>
		/// Gets a child node by its index within this node.
		/// </summary>
		/// <param name="index">The zero-based index of the child node.</param>
		/// <returns>The child node at the specified index.</returns>
		public TreeViewNode GetSubNode(int index)
		{
			return (TreeViewNode)_childNodesStackPanel.Children[index];
		}

		/// <summary>
		/// Removes a specific child node from this node and clears related state in the tree view.
		/// </summary>
		/// <param name="subNode">The child node to remove.</param>
		public void RemoveSubNode(TreeViewNode subNode)
		{
			_childNodesStackPanel.Children.Remove(subNode);
			_topTree.AllNodes.Remove(subNode);
			if (_topTree != null)
			{
				if (_topTree.SelectedNode == subNode)
				{
					_topTree.SelectedNode = null;
				}

				if (_topTree.HoverRow == subNode)
				{
					_topTree.HoverRow = null;
				}
			}
		}

		/// <summary>
		/// Removes the child node at the specified index and clears related state in the tree view.
		/// </summary>
		/// <param name="index">The zero-based index of the child node to remove.</param>
		public void RemoveSubNodeAt(int index)
		{
			var subNode = (TreeViewNode)_childNodesStackPanel.Children[index];
			_childNodesStackPanel.Children.RemoveAt(index);
			_topTree.AllNodes.Remove(subNode);
			if (_topTree.SelectedNode == subNode)
			{
				_topTree.SelectedNode = null;
			}
		}

		/// <summary>
		/// Applies the specified tree view node style to this node and its components.
		/// </summary>
		/// <param name="style">The tree style to apply.</param>
		public void ApplyTreeViewNodeStyle(TreeStyle style)
		{
			ApplyWidgetStyle(style);

			if (style.MarkStyle != null)
			{
				_mark.ApplyButtonStyle(style.MarkStyle);
				if (style.MarkStyle.ImageStyle != null)
				{
					var image = (Image)_mark.Content;
					image.ApplyPressableImageStyle(style.MarkStyle.ImageStyle);
				}
			}
		}

		/// <summary>
		/// Applies the style with the specified name from the stylesheet to this tree view node.
		/// </summary>
		/// <param name="stylesheet">The stylesheet containing the style to apply.</param>
		/// <param name="name">The name of the tree style to apply.</param>
		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplyTreeViewNodeStyle(stylesheet.TreeStyles.SafelyGetStyle(name));
		}
	}
}