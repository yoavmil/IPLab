using RControls.Adorners;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RControls.Thumbs
{
	public class ResizeThumb : Thumb
	{
		private RotateTransform rotateTransform;
		private double angle;
		private Adorner adorner;
		private Point transformOrigin;
		private ContentControl designerItem;
		private ImageCanvas canvas;

		public ResizeThumb()
		{
			DragStarted += new DragStartedEventHandler(this.ResizeThumb_DragStarted);
			DragDelta += new DragDeltaEventHandler(this.ResizeThumb_DragDelta);
			DragCompleted += new DragCompletedEventHandler(this.ResizeThumb_DragCompleted);
		}

		private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
		{
			this.designerItem = this.DataContext as ContentControl;

			// 多边形和任意形状不能尺寸变换
			ShapeMode shapeMode = (this.designerItem as ImageItem).ItemType;
			if (shapeMode == ShapeMode.Polygon || shapeMode == ShapeMode.Any)
				return;

			if (this.designerItem != null)
			{
				this.canvas = VisualTreeHelper.GetParent(this.designerItem) as ImageCanvas;

				if (this.canvas != null)
				{
					this.transformOrigin = this.designerItem.RenderTransformOrigin;

					this.rotateTransform = this.designerItem.RenderTransform as RotateTransform;
					if (this.rotateTransform != null)
					{
						this.angle = this.rotateTransform.Angle * Math.PI / 180.0;
					}
					else
					{
						this.angle = 0.0d;
					}

					AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this.canvas);
					if (adornerLayer != null)
					{
						this.adorner = new SizeAdorner(this.designerItem);
						adornerLayer.Add(this.adorner);
					}
				}
			}
		}

		private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
		{
			if (this.designerItem != null)
			{
				double deltaVertical = 0, deltaHorizontal = 0;
				switch (VerticalAlignment)
				{
					case System.Windows.VerticalAlignment.Bottom:
						deltaVertical = Math.Min(-e.VerticalChange, this.designerItem.ActualHeight - this.designerItem.MinHeight);
						Canvas.SetTop(this.designerItem, Canvas.GetTop(this.designerItem) + (this.transformOrigin.Y * deltaVertical * (1 - Math.Cos(-this.angle))));
						Canvas.SetLeft(this.designerItem, Canvas.GetLeft(this.designerItem) - deltaVertical * this.transformOrigin.Y * Math.Sin(-this.angle));
						//this.designerItem.Height -= deltaVertical;
						break;
					case System.Windows.VerticalAlignment.Top:
						deltaVertical = Math.Min(e.VerticalChange, this.designerItem.ActualHeight - this.designerItem.MinHeight);
						Canvas.SetTop(this.designerItem, Canvas.GetTop(this.designerItem) + deltaVertical * Math.Cos(-this.angle) + (this.transformOrigin.Y * deltaVertical * (1 - Math.Cos(-this.angle))));
						Canvas.SetLeft(this.designerItem, Canvas.GetLeft(this.designerItem) + deltaVertical * Math.Sin(-this.angle) - (this.transformOrigin.Y * deltaVertical * Math.Sin(-this.angle)));
						//this.designerItem.Height -= deltaVertical;
						break;
					default:
						break;
				}

				switch (HorizontalAlignment)
				{
					case System.Windows.HorizontalAlignment.Left:
						deltaHorizontal = Math.Min(e.HorizontalChange, this.designerItem.ActualWidth - this.designerItem.MinWidth);
						Canvas.SetTop(this.designerItem, Canvas.GetTop(this.designerItem) + deltaHorizontal * Math.Sin(this.angle) - this.transformOrigin.X * deltaHorizontal * Math.Sin(this.angle));
						Canvas.SetLeft(this.designerItem, Canvas.GetLeft(this.designerItem) + deltaHorizontal * Math.Cos(this.angle) + (this.transformOrigin.X * deltaHorizontal * (1 - Math.Cos(this.angle))));
						//this.designerItem.Width -= deltaHorizontal;
						break;
					case System.Windows.HorizontalAlignment.Right:
						deltaHorizontal = Math.Min(-e.HorizontalChange, this.designerItem.ActualWidth - this.designerItem.MinWidth);
						Canvas.SetTop(this.designerItem, Canvas.GetTop(this.designerItem) - this.transformOrigin.X * deltaHorizontal * Math.Sin(this.angle));
						Canvas.SetLeft(this.designerItem, Canvas.GetLeft(this.designerItem) + (deltaHorizontal * this.transformOrigin.X * (1 - Math.Cos(this.angle))));
						//this.designerItem.Width -= deltaHorizontal;
						break;
					default:
						break;
				}

				// 根据形状进行变换尺寸
				ImageItem imageItem = this.designerItem as ImageItem;
				switch (imageItem.ItemType)
				{
					case ShapeMode.Rectangle:
					case ShapeMode.Ellipse:
						{
							imageItem.Width  -= deltaHorizontal;
							imageItem.Height -= deltaVertical;
						}
						break;
					case ShapeMode.Circle:
						{
							if (deltaVertical == 0)
							{
								this.designerItem.Width -= deltaHorizontal;
								this.designerItem.Height = this.designerItem.Width;
							}
							else if (deltaHorizontal == 0)
							{
								this.designerItem.Width -= deltaVertical;
								this.designerItem.Height = this.designerItem.Width;
							}
							else
							{
								this.designerItem.Width -= deltaVertical;
								this.designerItem.Height = this.designerItem.Width;
							}
						}
						break;
					case ShapeMode.Line:
						{
							Line line = imageItem.Content as Line;
							line.X2 -= deltaHorizontal;
							line.Y2 -= deltaVertical;
							imageItem.Width -= deltaHorizontal;
							imageItem.Height -= deltaVertical;
						}
						break;
				}

			}

			e.Handled = true;
		}

		private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
		{
			if (this.adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this.canvas);
				if (adornerLayer != null)
				{
					adornerLayer.Remove(this.adorner);
				}

				this.adorner = null;
			}
		}
	}
}
