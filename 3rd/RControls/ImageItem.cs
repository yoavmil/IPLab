using RControls.Thumbs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RControls
{
	public class ImageItem : ContentControl
	{
		#region 名称
		public string ItemName { get; set; }
		#endregion

		#region 类型
		public ShapeMode ItemType { get; set; }
		#endregion

		#region 当前缩放比例
		public double Ratio { get; set; } = 1.0;
		#endregion

		#region 依赖属性
		public bool IsSelected
		{
			get { return (bool)GetValue(IsSelectedProperty); }
			set { SetValue(IsSelectedProperty, value); }
		}
		public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(ImageItem), new FrameworkPropertyMetadata(false));


		public static readonly DependencyProperty MoveThumbTemplateProperty =
			DependencyProperty.RegisterAttached("MoveThumbTemplate", typeof(ControlTemplate), typeof(ImageItem));

		public static ControlTemplate GetMoveThumbTemplate(UIElement element)
		{
			return (ControlTemplate)element.GetValue(MoveThumbTemplateProperty);
		}

		public static void SetMoveThumbTemplate(UIElement element, ControlTemplate value)
		{
			element.SetValue(MoveThumbTemplateProperty, value);
		}
		#endregion

		static ImageItem()
		{
			FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageItem), new FrameworkPropertyMetadata(typeof(ImageItem)));
		}

		public ImageItem()
		{
			this.Loaded += new RoutedEventHandler(this.DesignerItem_Loaded);
		}

		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseDown(e);

			ImageCanvas designer = VisualTreeHelper.GetParent(this) as ImageCanvas;
			if (designer != null && designer.Viewer.IsSelectable == true)
			{
				// 按住Shift或者Ctrl可切换当前Item的选中状态
				if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != ModifierKeys.None)
				{
					this.IsSelected = !this.IsSelected;
				}
				else
				{
					if (!this.IsSelected)
					{
						designer.DeselectAll();
						//this.IsSelected = true;
					}
				}
			}

			e.Handled = false;
		}

		private void DesignerItem_Loaded(object sender, RoutedEventArgs e)
		{
			if (this.Template != null)
			{
				ContentPresenter contentPresenter =
					this.Template.FindName("PART_ContentPresenter", this) as ContentPresenter;

				MoveThumb thumb =
					this.Template.FindName("PART_MoveThumb", this) as MoveThumb;

				if (contentPresenter != null && thumb != null)
				{
					UIElement contentVisual =
						VisualTreeHelper.GetChild(contentPresenter, 0) as UIElement;

					if (contentVisual != null)
					{
						ControlTemplate template =
							ImageItem.GetMoveThumbTemplate(contentVisual) as ControlTemplate;

						if (template != null)
						{
							thumb.Template = template;
						}
					}
				}
			}
		}
	}
}
