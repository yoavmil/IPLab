using RControls.Adorners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RControls
{
    public partial class ImageCanvas : Canvas
    {
        #region 父控件对象
        public ImageViewer Viewer = null;
        #endregion

        #region 鼠标拖动起始点
        public Point? startPoint = null;
        #endregion

        #region 形状选择
        public IEnumerable<ImageItem> SelectedItems
        {
            get
            {
                var selectedItems = from item in this.Children.OfType<ImageItem>()
                                    where item.IsSelected == true
                                    select item;

                return selectedItems;
            }
        }

        public void DeselectAll()
        {
            foreach (ImageItem item in this.SelectedItems)
            {
                item.IsSelected = false;
            }
        }
        #endregion

        #region 鼠标操作
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            // 根据缩放比例对形状的变宽进行缩放
            RefreshShapeThicknesses();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 鼠标左键没有按下则不处理
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                this.startPoint = null;
            }

            // 选择模式
            if (startPoint.HasValue && Viewer.OperationMode == OpMode.Select && Viewer.IsSelectable == true)
            {
                // 弹出橡皮筋装饰
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if (adornerLayer != null)
                {
                    RubberbandAdorner adorner = new RubberbandAdorner(this, startPoint);
                    if (adorner != null)
                    {
                        adornerLayer.Add(adorner);
                    }
                }
                e.Handled = false;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            // 移动和查看模式不需要在此处理
            if (Viewer.OperationMode == OpMode.Move || Viewer.OperationMode == OpMode.Disable
                || e.ChangedButton == MouseButton.Right)
            {
                // 使该控件作为焦点
                Focus();

                return;
            }

            if (e.Source == this)
            {
                // 获取起始拖动点
                startPoint = new Point?(e.GetPosition(this));

                // 取消所有选择的形状
                DeselectAll();

                // 使该控件作为焦点
                Focus();

                // 绘制模式
                if (startPoint.HasValue && Viewer.OperationMode == OpMode.Draw && Viewer.IsDrawable == true)
                {
                    // 弹出橡皮筋装饰
                    AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                    if (adornerLayer != null)
                    {
                        RubberbandAdorner adorner = new RubberbandAdorner(this, startPoint);
                        if (adorner != null)
                        {
                            adornerLayer.Add(adorner);
                        }
                    }
                    e.Handled = false;
                    return;
                }

                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            startPoint = null;
        }
        #endregion

        #region 绘制形状
        public void DispText(string stext, Point pt, double size = 100, int colSel = 2)
        {
            Color color;//= Color.FromRgb(255, 0, 0);
            switch (colSel)
            {
                case 1:
                    color = Color.FromRgb(255, 0, 0);
                    break;
                case 2:
                    color = Color.FromRgb(0, 255, 0);
                    break;
                case 3:
                    color = Color.FromRgb(0, 0, 255);
                    break;
                default:
                    color = Color.FromRgb(150, 150, 150);
                    break;
            }
            TextBlock nodetext = new TextBlock();

            nodetext.Text = stext;
            nodetext.Foreground = new SolidColorBrush(color);
            nodetext.FontSize = size;


            Canvas.SetLeft(nodetext, pt.X);
            Canvas.SetTop(nodetext, pt.Y);
            this.Children.Add(nodetext);
            this.DeselectAll();
        }

        public void DrawDefect(double row1, double column1, double row2, double column2, string name, Brush color)
        {
            Rect rc = new Rect(new Point(column1, row1), new Point(column2, row2));

            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Rectangle;
            item.Ratio = 1.0;
            item.Content = new Rectangle()
            {
                Stroke = color,
                StrokeThickness = 1.0,
            };
            item.Width = rc.Width;
            item.Height = rc.Height;

            Canvas.SetLeft(item, Math.Max(0, column1));
            Canvas.SetTop(item, Math.Max(0, row1));
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawRectangle(double row1, double column1, double row2, double column2, string name, Brush color, bool bFilled)
        {
            Rect rc = new Rect(new Point(column1, row1), new Point(column2, row2));

            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Rectangle;
            item.Ratio = Viewer.scaleRatio;
            item.Content = new Rectangle()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };
            item.Width = rc.Width;
            item.Height = rc.Height;

            Canvas.SetLeft(item, Math.Max(0, column1));
            Canvas.SetTop(item, Math.Max(0, row1));
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawRectangle2(double row, double column, double phi, double length1, double length2, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Rectangle;
            item.Content = new Rectangle()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };
            item.Width = 2 * length1;
            item.Height = 2 * length2;
            item.RenderTransform = new RotateTransform(phi, 0, 0);

            Canvas.SetLeft(item, Math.Max(0, column - length1));
            Canvas.SetTop(item, Math.Max(0, row - length2));
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawLine(double rowbegin, double columnbegin, double rowend, double columnend, string name, Brush color, bool bFilled = true)
        {
            Rect rubberband = new Rect(new Point(columnbegin, rowbegin), new Point(columnend, rowend));

            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Line;

            item.Content = new Line()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
                X1 = 0,
                Y1 = 0,
                X2 = columnend - columnbegin,
                Y2 = rowend - rowbegin
            };

            item.Width = rubberband.Width;
            item.Height = rubberband.Height;

            Canvas.SetLeft(item, Math.Max(0, rubberband.Left));
            Canvas.SetTop(item, Math.Max(0, rubberband.Top));

            this.Children.Add(item);
            this.DeselectAll();
        }

        //public void DrawCross(double Row, double Column, double Angle, string name, int Length, Brush Color)

        ///传2个数到一维数组，完成绘制2条直线
        public void DrawLine(double[] rowbegin, double[] columnbegin, double[] rowend, double[] columnend, string name, Brush color, bool bFilled = true)
        {
			// fixed with ChatGPT

			for (int i = 0; i < rowbegin.Length; i++)
			{
				double x1Abs = columnbegin[i];
				double y1Abs = rowbegin[i];
				double x2Abs = columnend[i];
				double y2Abs = rowend[i];

				double left = Math.Min(x1Abs, x2Abs);
				double top = Math.Min(y1Abs, y2Abs);

				double w = Math.Abs(x2Abs - x1Abs);
				double h = Math.Abs(y2Abs - y1Abs);

				double thickness = 2.0 / Viewer.scaleRatio;

				// Prevent 0-sized containers (horizontal/vertical lines)
				w = Math.Max(w, thickness);
				h = Math.Max(h, thickness);

				// Local coords inside the item (always within [0..w]/[0..h])
				double x1 = x1Abs - left;
				double y1 = y1Abs - top;
				double x2 = x2Abs - left;
				double y2 = y2Abs - top;

				var item = new ImageItem
				{
					ItemName = name,
					ItemType = ShapeMode.Line,
					Width = w,
					Height = h,
					Content = new Line
					{
						Stroke = color,
						StrokeThickness = thickness,
						X1 = x1,
						Y1 = y1,
						X2 = x2,
						Y2 = y2,
						SnapsToDevicePixels = true
					}
				};

				Canvas.SetLeft(item, left);
				Canvas.SetTop(item, top);

				this.Children.Add(item);
			}
			this.DeselectAll();
        }

        public void DrawCircle(double row, double column, double radius, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Circle;
            item.Content = new Ellipse()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };

            double left = column - radius;
            double top = row - radius;
            item.Width = 2 * radius;
            item.Height = 2 * radius;
            Canvas.SetLeft(item, left);
            Canvas.SetTop(item, top);
            this.Children.Add(item);
        }

        public void DrawEllipse(double Row, double Column, double Angle, double Ra, double Rb, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Ellipse;
            item.Content = new Ellipse()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };

            double left = Column - Rb;
            double top = Row - Ra;
            item.Width = 2 * Ra;
            item.Height = 2 * Rb;
            item.RenderTransform = new RotateTransform(Angle, 0, 0);
            Canvas.SetLeft(item, left);
            Canvas.SetTop(item, top);
            this.Children.Add(item);
        }

        public void RefreshShapeThicknesses()
        {
            foreach (ImageItem item in this.Children.OfType<ImageItem>())
            {
                if (item.Content is Shape shape)
                    shape.StrokeThickness = 2.0 / Viewer.scaleRatio;
            }
        }

        public void DrawPolygon(List<Point> pts, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Polygon;
            item.Content = new Polygon()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };

            // 获取点集包围盒
            Rect rc = GetBounds(pts);
            Polygon polygon = item.Content as Polygon;
            foreach (var pt in pts)
            {
                polygon.Points.Add(new Point(pt.X - rc.Left, pt.Y - rc.Top));
            }
            item.Width = rc.Width;
            item.Height = rc.Height;
            Canvas.SetLeft(item, Math.Max(0, rc.Left));
            Canvas.SetTop(item, Math.Max(0, rc.Top));

            this.Children.Add(item);
            this.DeselectAll();
        }

        /// <summary>
        /// Draws multiple closed polygons as a single Path in absolute image coordinates.
        /// Callers must pre-filter degenerate polygons (zero area/arc-length) before calling.
        /// </summary>
        public void DrawPolygons(IReadOnlyList<List<Point>> polygons, string name, Brush color)
        {
            if (polygons.Count == 0) return;

            var geometry = new PathGeometry();
            foreach (var pts in polygons)
            {
                if (pts.Count < 3) continue;
                var figure = new PathFigure { StartPoint = pts[0], IsClosed = true };
                for (int i = 1; i < pts.Count; i++)
                    figure.Segments.Add(new LineSegment(pts[i], true));
                geometry.Figures.Add(figure);
            }
            if (geometry.Figures.Count == 0) return;

            ImageItem item = new ImageItem
            {
                ItemName = name,
                ItemType = ShapeMode.Polygon,
                Content  = new Path
                {
                    Stroke          = color,
                    StrokeThickness = 2.0 / Viewer.scaleRatio,
                    Data            = geometry,
                    Width           = this.Width,
                    Height          = this.Height,
                }
            };
            item.Width  = this.Width;
            item.Height = this.Height;
            Canvas.SetLeft(item, 0);
            Canvas.SetTop(item, 0);
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawAnyShape(List<Point> pts, string name, Brush color, bool bFilled)
        {
            // 获取点集包围盒
            Rect rc = GetBounds(pts);

            // 生成路径集合
            List<LineSegment> segments = new List<LineSegment>();
            for (int i = 1; i < pts.Count; i++)
            {
                // 此处要相对于包围盒进行偏移
                segments.Add(new LineSegment(new Point(pts[i].X - rc.Left, pts[i].Y - rc.Top), true));
            }
            PathFigure figure = new PathFigure(new Point(pts.First().X - rc.Left, pts.First().Y - rc.Top), segments, bFilled);
            PathGeometry mypathGeometry = new PathGeometry();
            mypathGeometry.Figures.Add(figure);

            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Any;
            item.Content = new Path()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
                Data = mypathGeometry,
            };

            item.Width = rc.Width;
            item.Height = rc.Height;
            Canvas.SetLeft(item, rc.Left);
            Canvas.SetTop(item, rc.Top);

            this.Children.Add(item);
            this.DeselectAll();
        }

        // Method to create a PathGeometry representing a cross
        public PathGeometry CreateCrossGeometry(double length)
        {
            // Define segments for the horizontal and vertical lines of the cross
            List<LineSegment> horizontalLineSegments = new List<LineSegment>
            {
                new LineSegment(new Point(length, length / 2.0), true)
            };
            List<LineSegment> verticalLineSegments = new List<LineSegment>
            {
                new LineSegment(new Point(length / 2.0, length), true)
            };

            // Define PathFigures for horizontal and vertical lines, centered on (length / 2, length / 2)
            PathFigure horizontalLine = new PathFigure(new Point(0, length / 2.0), horizontalLineSegments, false);
            PathFigure verticalLine = new PathFigure(new Point(length / 2.0, 0), verticalLineSegments, false);

            // Combine the PathFigures into a PathGeometry
            PathGeometry crossGeometry = new PathGeometry();
            crossGeometry.Figures.Add(horizontalLine);
            crossGeometry.Figures.Add(verticalLine);

            return crossGeometry;
        }

        public void DrawCross(double Row, double Column, double Angle, string name, int Length, Brush Color)
        {
            var mypathGeometry = CreateCrossGeometry(Length);

            ImageItem item = new ImageItem();
            item.ItemName = name;
            item.ItemType = ShapeMode.Cross;
            item.Content = new Path()
            {
                Stroke = Color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
                Data = mypathGeometry,
            };

            item.MinWidth = item.Width = Length;
            item.MinHeight = item.Height = Length;
            item.RenderTransform = new RotateTransform(Angle, 0, 0);
            Canvas.SetLeft(item, Column - Length / 2.0);
            Canvas.SetTop(item, Row - Length / 2.0);
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void RemoveRegion(string name, ShapeMode shape)
        {

            IEnumerable<ImageItem> selectedItems = null;
            if (string.IsNullOrEmpty(name) && shape != ShapeMode.None)
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.ItemType == shape
                                select items;
            }
            else if (shape == ShapeMode.None && !string.IsNullOrEmpty(name))
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.ItemName == name
                                select items;
            }
            else if (shape == ShapeMode.None && string.IsNullOrEmpty(name))
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                select items;
            }
            else
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.ItemName == name && items.ItemType == shape
                                select items;
            }
            if (selectedItems != null)
            {
                List<ImageItem> lst = selectedItems.ToList();
                foreach (ImageItem it in lst.OfType<ImageItem>())
                {
                    this.Children.Remove(it);
                }
            }
        }

        public List<ImageItem> GetRegions(string name, ShapeMode shape)
        {
            if (string.IsNullOrEmpty(name) && shape == ShapeMode.None)
            {
                return this.Children.OfType<ImageItem>().ToList();
            }

            IEnumerable<ImageItem> selectedItems = null;
            if (string.IsNullOrEmpty(name))
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.ItemType == shape
                                select items;
            }
            else if (shape == ShapeMode.None)
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.Name == name
                                select items;
            }
            else
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.Name == name && items.ItemType == shape
                                select items;
            }

            return selectedItems.ToList();
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
            return new Rect(Math.Max(0, minX), Math.Max(0, minY), maxX - minX, maxY - minY);
            //return new Rect(Math.Max(0, minX - 5), Math.Max(0, minY - 5), maxX - minX + 10, maxY - minY + 10);
        }

        #endregion

        protected override Size MeasureOverride(Size constraint)
        {
            Size size = new Size();
            foreach (UIElement element in Children)
            {
                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);
                left = double.IsNaN(left) ? 0 : left;
                top = double.IsNaN(top) ? 0 : top;

                element.Measure(constraint);

                Size desiredSize = element.DesiredSize;
                if (!double.IsNaN(desiredSize.Width) && !double.IsNaN(desiredSize.Height))
                {
                    size.Width = Math.Max(size.Width, left + desiredSize.Width);
                    size.Height = Math.Max(size.Height, top + desiredSize.Height);
                }
            }

            // add some extra margin
            size.Width += 10;
            size.Height += 10;
            return size;
        }
    }
}
