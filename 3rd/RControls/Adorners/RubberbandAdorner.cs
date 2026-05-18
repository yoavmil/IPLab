using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RControls.Adorners
{
	public class RubberbandAdorner : Adorner
	{
		private Point? startPoint, endPoint;
		private Shape rubberband;
		private ImageCanvas imageCanvas;
		private VisualCollection visuals;
		private Canvas adornerCanvas;

		protected override int VisualChildrenCount
		{
			get
			{
				return this.visuals.Count;
			}
		}

		public RubberbandAdorner(ImageCanvas imageCanvas, Point? dragStartPoint)
			: base(imageCanvas)
		{
			this.imageCanvas = imageCanvas;
			this.startPoint  = dragStartPoint;

			this.adornerCanvas = new Canvas();
			this.adornerCanvas.Background = Brushes.Transparent;
			this.visuals = new VisualCollection(this);
			this.visuals.Add(this.adornerCanvas);

			this.rubberband = CreateRealShape(this.imageCanvas.Viewer.DrawShape);
			this.rubberband.Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215));
			this.rubberband.StrokeThickness = 1.0 / this.imageCanvas.Viewer.scaleRatio;
			this.rubberband.Fill = new SolidColorBrush(Color.FromArgb(70, 0, 120, 215));

			this.adornerCanvas.Children.Add(this.rubberband);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				if (!this.IsMouseCaptured)
				{
					this.CaptureMouse();
				}

				// 更新橡皮筋形状
				endPoint = e.GetPosition(this);
				UpdateRubberband();

				// 选择模式更新选择
				if (imageCanvas.Viewer.OperationMode == OpMode.Select)
					UpdateSelection();

				e.Handled = true;
			}
		}

		protected override void OnMouseUp(MouseButtonEventArgs e)
		{
			// 右键结束绘制
			if (e.ChangedButton == MouseButton.Right && imageCanvas.Viewer.OperationMode == OpMode.Draw)
			{
				// 释放鼠标
				if (this.IsMouseCaptured)
				{
					this.ReleaseMouseCapture();
				}

				// 删除装饰器
				AdornerLayer adornerLayer = this.Parent as AdornerLayer;
				if (adornerLayer != null)
				{
					adornerLayer.Remove(this);
				}

				// 绘制形状(避免鼠标误点,设置最小形状起始点距离为5个像素)
				if (endPoint.HasValue &&
					Math.Abs(startPoint.Value.X - endPoint.Value.X) > 5 &&
					Math.Abs(startPoint.Value.Y - endPoint.Value.Y) > 5)
					CreateDesignerItem(imageCanvas.Viewer.DrawShape);

				// 绘制完成后，将模式切换回选择模式
				imageCanvas.Viewer.OperationMode = OpMode.Select;
			}
			else if (e.ChangedButton == MouseButton.Left)
			{
				// 绘制模式下可点击鼠标添加绘制点
				if (imageCanvas.Viewer.OperationMode == OpMode.Draw)
				{
					// 更新橡皮筋形状
					this.endPoint = e.GetPosition(this);
					this.UpdateRubberband();
				}
				else if (imageCanvas.Viewer.OperationMode == OpMode.Select)
				{
					// 选择模式则完成选择绘框
					AdornerLayer adornerLayer = this.Parent as AdornerLayer;
					if (adornerLayer != null)
					{
						adornerLayer.Remove(this);
					}
				}
			}
		}

		protected override Size ArrangeOverride(Size arrangeBounds)
		{
			this.adornerCanvas.Arrange(new Rect(arrangeBounds));
			return arrangeBounds;
		}

		protected override Visual GetVisualChild(int index)
		{
			return this.visuals[index];
		}

		#region 更新橡皮筋形状

		private void UpdateRubberband()
		{
			switch (imageCanvas.Viewer.DrawShape)
			{
				case ShapeMode.Rectangle:
					{
						UpdateRectangleItem();
					}
					break;
				case ShapeMode.Line:
					{
						UpdateLineItem();
					}
					break;
				case ShapeMode.Circle:
					{
						UpdateCircleItem();
					}
					break;
				case ShapeMode.Ellipse:
					{
						UpdateEllipseItem();
					}
					break;
				case ShapeMode.Polygon:
					{
						UpdatePolygonItem();
					}
					break;
				case ShapeMode.Any:
					{
						UpdateAnyItem();
					}
					break;
				default:
					{
						UpdateRectangleItem();
					}
					break;
			}
		}

		private void UpdateRectangleItem()
		{
			double left = Math.Min(this.startPoint.Value.X, this.endPoint.Value.X);
			double top = Math.Min(this.startPoint.Value.Y, this.endPoint.Value.Y);
			double width = Math.Abs(this.startPoint.Value.X - this.endPoint.Value.X);
			double height = Math.Abs(this.startPoint.Value.Y - this.endPoint.Value.Y);

			Rectangle rc = this.rubberband as Rectangle;
			rc.Width    = width;
			rc.Height   = height;
			rc.StrokeThickness = 2.0 / this.imageCanvas.Viewer.scaleRatio;
			Canvas.SetLeft(this.rubberband, left);
			Canvas.SetTop(this.rubberband, top);
		}

		private void UpdateLineItem()
		{
			Line l = this.rubberband as Line;

			l.X1 = startPoint.Value.X;
			l.Y1 = startPoint.Value.Y;
			l.X2 = endPoint.Value.X;
			l.Y2 = endPoint.Value.Y;
			l.StrokeThickness = 2.0 / this.imageCanvas.Viewer.scaleRatio;
			Canvas.SetLeft(this.rubberband, 0);
			Canvas.SetTop(this.rubberband, 0);
		}

		private void UpdateCircleItem()
		{
			double deltaX = Math.Abs(this.startPoint.Value.X - this.endPoint.Value.X);
			double deltaY = Math.Abs(this.startPoint.Value.Y - this.endPoint.Value.Y);
			double radius = Math.Sqrt(Math.Pow(deltaX, 2.0) + Math.Pow(deltaY, 2.0));
			double left = this.startPoint.Value.X - radius;
			double top = this.startPoint.Value.Y - radius;

			Ellipse circle = this.rubberband as Ellipse;
			circle.Width    = 2 * radius;
			circle.Height   = 2 * radius;
			circle.StrokeThickness = 2.0 / this.imageCanvas.Viewer.scaleRatio;
			Canvas.SetLeft(this.rubberband, left);
			Canvas.SetTop(this.rubberband, top);
		}

		private void UpdateEllipseItem()
		{
			double deltaX = Math.Abs(this.startPoint.Value.X - this.endPoint.Value.X);
			double deltaY = Math.Abs(this.startPoint.Value.Y - this.endPoint.Value.Y);
			double radius = Math.Sqrt(Math.Pow(deltaX, 2.0) + Math.Pow(deltaY, 2.0));
			double left = this.startPoint.Value.X - radius;
			double top = this.startPoint.Value.Y - 25;

			Ellipse circle = this.rubberband as Ellipse;
			circle.Width    = 2 * radius;
			circle.Height   = 50;
			circle.StrokeThickness = 2.0 / this.imageCanvas.Viewer.scaleRatio;
			Canvas.SetLeft(this.rubberband, left);
			Canvas.SetTop(this.rubberband, top);
		}

		private void UpdatePolygonItem()
		{
			Polyline polyline = this.rubberband as Polyline;
			polyline.Points.Add(endPoint.Value);
		}

		private void UpdateAnyItem()
		{
			Polyline polyline = this.rubberband as Polyline;
			polyline.Points.Add(endPoint.Value);
		}

		#endregion

		#region 选择模式更新选择的形状
		private void UpdateSelection()
		{
			Rect rubberBand = new Rect(this.startPoint.Value, this.endPoint.Value);
			foreach (ImageItem item in this.imageCanvas.Children.OfType<ImageItem>())
			{
				Rect itemRect = VisualTreeHelper.GetDescendantBounds(item);
				Rect itemBounds = item.TransformToAncestor(imageCanvas).TransformBounds(itemRect);

				if (rubberBand.Contains(itemBounds))
				{
					item.IsSelected = true;
				}
				else
				{
					item.IsSelected = false;
				}
			}
		}
		#endregion

		#region 创建形状

		private Shape CreateRealShape(ShapeMode shapeMode)
		{
			switch (shapeMode)
			{
				case ShapeMode.Rectangle:
					{
						return new Rectangle();
					}
				case ShapeMode.Line:
					{
						return new Line();
					}
				case ShapeMode.Circle:
				case ShapeMode.Ellipse:
					{
						return new Ellipse();
					}
				case ShapeMode.Polygon:
					{
						return new Polyline();
					}
				case ShapeMode.Any:
					{
						return new Polyline();
					}
				default:
					return new Rectangle();
			}
		}

		private void CreateDesignerItem(ShapeMode shapeMode)
		{
			switch (shapeMode)
			{
				case ShapeMode.Rectangle:
					{
						CreateRectangleItem();
					}
					break;
				case ShapeMode.Line:
					{
						CreateLineItem();
					}
					break;
				case ShapeMode.Circle:
					{
						CreateCircleItem();
					}
					break;
				case ShapeMode.Ellipse:
					{
						CreateEllipseItem();
					}
					break;
				case ShapeMode.Polygon:
					{
						CreatePolygonItem();
					}
					break;
				case ShapeMode.Any:
					{
						CreateAnyItem();
					}
					break;
				default:
					{
						CreateRectangleItem();
					}
					break;
			}
		}

		private void CreateRectangleItem()
		{
			Rect rubberband = new Rect(startPoint.Value, endPoint.Value);

			ImageItem item = new ImageItem();
			item.ItemType  = ShapeMode.Rectangle;
			item.Content = new Rectangle()
			{
				Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
				StrokeThickness = 1.0 / this.imageCanvas.Viewer.scaleRatio,
			};
			item.Width  = rubberband.Width;
			item.Height = rubberband.Height;

			Canvas.SetLeft(item, Math.Max(0, rubberband.Left));
			Canvas.SetTop(item, Math.Max(0, rubberband.Top));

			this.imageCanvas.Children.Add(item);
			this.imageCanvas.DeselectAll();
		}

		private void CreateLineItem()
		{
			Rect rubberband = new Rect(startPoint.Value, endPoint.Value);

			ImageItem item = new ImageItem();
			item.ItemType = ShapeMode.Line;
			item.Content = new Line()
			{
				Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
				StrokeThickness = 1.0 / this.imageCanvas.Viewer.scaleRatio,
				X1 = 0,
				Y1 = 0,
				X2 = endPoint.Value.X - startPoint.Value.X,
				Y2 = endPoint.Value.Y -  startPoint.Value.Y
			};
			item.Width  = rubberband.Width;
			item.Height = rubberband.Height;

			//Canvas.SetLeft(item, 0);
			//Canvas.SetTop(item, 0);
			Canvas.SetLeft(item, Math.Max(0, rubberband.Left));
			Canvas.SetTop(item, Math.Max(0, rubberband.Top));

			this.imageCanvas.Children.Add(item);
			this.imageCanvas.DeselectAll();
		}

		private void CreateCircleItem()
		{
			ImageItem item = new ImageItem();
			item.ItemType   = ShapeMode.Circle;
			item.Content = new Ellipse()
			{
				Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
				StrokeThickness = 1.0 / this.imageCanvas.Viewer.scaleRatio,
			};

			double deltaX = Math.Abs(this.startPoint.Value.X - this.endPoint.Value.X);
			double deltaY = Math.Abs(this.startPoint.Value.Y - this.endPoint.Value.Y);
			double radius = Math.Sqrt(Math.Pow(deltaX, 2.0) + Math.Pow(deltaY, 2.0));
			double left = this.startPoint.Value.X - radius;
			double top = this.startPoint.Value.Y - radius;
			item.Width      = 2 * radius;
			item.Height     = 2 * radius;
			Canvas.SetLeft(item, left);
			Canvas.SetTop(item, top);
			this.imageCanvas.Children.Add(item);
		}

		private void CreateEllipseItem()
		{
			ImageItem item = new ImageItem();
			item.ItemType   = ShapeMode.Ellipse;
			item.Content = new Ellipse()
			{
				Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
				StrokeThickness = 1.0 / this.imageCanvas.Viewer.scaleRatio,
			};

			double deltaX = Math.Abs(this.startPoint.Value.X - this.endPoint.Value.X);
			double deltaY = Math.Abs(this.startPoint.Value.Y - this.endPoint.Value.Y);
			double radius = Math.Sqrt(Math.Pow(deltaX, 2.0) + Math.Pow(deltaY, 2.0));
			double left = this.startPoint.Value.X - radius;
			double top = this.startPoint.Value.Y - 25;
			item.Width      = 2 * radius;
			item.Height     = 50;
			Canvas.SetLeft(item, left);
			Canvas.SetTop(item, top);
			this.imageCanvas.Children.Add(item);
		}

		private void CreatePolygonItem()
		{
			Polyline polyline = this.rubberband as Polyline;

			ImageItem item = new ImageItem();
			item.ItemType  = ShapeMode.Polygon;
			item.Content = new Polygon()
			{
				Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
				StrokeThickness = 1.0 / this.imageCanvas.Viewer.scaleRatio,
			};

			// 获取点集包围盒
			Rect rc = GetBounds(polyline.Points.ToList());
			Polygon polygon = item.Content as Polygon;
			foreach (var pt in polyline.Points)
			{
				polygon.Points.Add(new Point(pt.X - rc.Left, pt.Y - rc.Top));
			}
			item.Width  = rc.Width;
			item.Height = rc.Height;
			Canvas.SetLeft(item, Math.Max(0, rc.Left));
			Canvas.SetTop(item, Math.Max(0, rc.Top));

			this.imageCanvas.Children.Add(item);
			this.imageCanvas.DeselectAll();
		}

		private void CreateAnyItem()
		{
			// 获取点集包围盒
			Polyline polyline = this.rubberband as Polyline;
			Rect rc = GetBounds(polyline.Points.ToList());

			// 生成路径集合
			List<LineSegment> segments = new List<LineSegment>();
			for (int i = 1; i < polyline.Points.Count; i++)
			{
				// 此处要相对于包围盒进行偏移
				segments.Add(new LineSegment(new Point(polyline.Points[i].X - rc.Left,
					polyline.Points[i].Y - rc.Top), true));
			}
			PathFigure figure = new PathFigure(new Point(polyline.Points.First().X - rc.Left, polyline.Points.First().Y - rc.Top), segments, true);
			PathGeometry mypathGeometry = new PathGeometry();
			mypathGeometry.Figures.Add(figure);

			ImageItem item = new ImageItem();
			item.ItemType = ShapeMode.Any;
			item.Content = new Path()
			{
				Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
				StrokeThickness = 1.0 / this.imageCanvas.Viewer.scaleRatio,
				Data = mypathGeometry,
			};

			item.Width  = rc.Width;
			item.Height = rc.Height;
			Canvas.SetLeft(item, rc.Left);
			Canvas.SetTop(item, rc.Top);

			this.imageCanvas.Children.Add(item);
		}

		private Rect GetBounds(List<Point> pts)
		{
			double minX = 1000000;
			double minY = 1000000;
			double maxX = 0;
			double maxY = 0;

			foreach (var pt in pts)
			{
				if (pt.X < minX)
					minX = pt.X;
				if (pt.Y < minY)
					minY = pt.Y;
				if (pt.X > maxX)
					maxX = pt.X;
				if (pt.Y > maxY)
					maxY = pt.Y;
			}

			return new Rect(Math.Max(0, minX - 5), Math.Max(0, minY - 5), maxX - minX + 10, maxY - minY + 10);
		}

		#endregion
	}
}
