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
            foreach (ImageItem item in this.Children.OfType<ImageItem>())
            {
                Shape rc = (Shape)item.Content;
                rc.StrokeThickness = 2.0 / Viewer.scaleRatio;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 鼠标左键没有按下则不处理
            if(e.LeftButton != MouseButtonState.Pressed)
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
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Rectangle;
            item.Ratio     = Viewer.scaleRatio;
            item.Content = new Rectangle()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };
            item.Width  = rc.Width;
            item.Height = rc.Height;

            Canvas.SetLeft(item,Math.Max(0,column1));
            Canvas.SetTop(item,Math.Max(0,row1));
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawRectangle2(double row, double column, double phi, double length1, double length2, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Rectangle;
            item.Content = new Rectangle()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };
            item.Width  = 2 * length1;
            item.Height = 2 * length2;
            item.RenderTransform = new RotateTransform(phi, 0, 0);

            Canvas.SetLeft(item, Math.Max(0, column-length1));
            Canvas.SetTop(item, Math.Max(0, row-length2));
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawLine(double rowbegin, double columnbegin, double rowend, double columnend, string name, Brush color, bool bFilled=true)
        {
            Rect rubberband = new Rect(new Point(columnbegin,rowbegin),new Point(columnend,rowend));

            ImageItem item = new ImageItem();
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Line;
            item.Content = new Line()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
                X1 = 0,
                Y1 = 0,
                X2 = columnend - columnbegin,
                Y2 = rowend - rowbegin
            };
            item.Width  = rubberband.Width;
            item.Height = rubberband.Height;

            Canvas.SetLeft(item, Math.Max(0, rubberband.Left));
            Canvas.SetTop(item, Math.Max(0, rubberband.Top));

            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawCircle(double row, double column, double radius, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Circle;
            item.Content = new Ellipse()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };

            double left = column - radius;
            double top  = row - radius;
            item.Width  = 2 * radius;
            item.Height = 2 * radius;
            Canvas.SetLeft(item, left);
            Canvas.SetTop(item, top);
            this.Children.Add(item);
        }

        public void DrawEllipse(double Row, double Column, double Angle, double Ra, double Rb, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Ellipse;
            item.Content = new Ellipse()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
            };

            double left   = Column - Rb;
            double top    = Row - Ra;
            item.Width    = 2 * Ra;
            item.Height   = 2 * Rb;
            item.RenderTransform = new RotateTransform(Angle, 0, 0);
            Canvas.SetLeft(item, left);
            Canvas.SetTop(item, top);
            this.Children.Add(item);
        }

        public void DrawPolygon(List<Point> pts, string name, Brush color, bool bFilled)
        {
            ImageItem item = new ImageItem();
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Polygon;
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
            item.Width  = rc.Width;
            item.Height = rc.Height;
            Canvas.SetLeft(item, Math.Max(0, rc.Left));
            Canvas.SetTop(item, Math.Max(0, rc.Top));

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
                segments.Add(new LineSegment(new Point(pts[i].X - rc.Left,pts[i].Y - rc.Top), true));
            }
            PathFigure figure = new PathFigure(new Point(pts.First().X - rc.Left, pts.First().Y - rc.Top), segments, bFilled);
            PathGeometry mypathGeometry = new PathGeometry();
            mypathGeometry.Figures.Add(figure);

            ImageItem item = new ImageItem();
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Any;
            item.Content = new Path()
            {
                Stroke = color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
                Data = mypathGeometry,
            };

            item.Width  = rc.Width;
            item.Height = rc.Height;
            Canvas.SetLeft(item, rc.Left);
            Canvas.SetTop(item, rc.Top);

            this.Children.Add(item);
            this.DeselectAll();
        }

        public void DrawCross(double Row, double Column, double Angle, string name, int Length, Brush Color)
        {
            // 生成路径集合
            List<LineSegment> segment1 = new List<LineSegment>();
            segment1.Add(new LineSegment(new Point(Length-1, Length/2.0), true));
            List<LineSegment> segment2 = new List<LineSegment>();
            segment2.Add(new LineSegment(new Point(Length/2.0, Length-1), true));

            PathFigure figure1 = new PathFigure(new Point(0, Length/2.0), segment1, true);
            PathFigure figure2 = new PathFigure(new Point(Length / 2.0, 0), segment2, true);
            PathGeometry mypathGeometry = new PathGeometry();
            mypathGeometry.Figures.Add(figure1);
            mypathGeometry.Figures.Add(figure2);

            ImageItem item = new ImageItem();
            item.ItemName  = name;
            item.ItemType  = ShapeMode.Cross;
            item.Content = new Path()
            {
                Stroke = Color,
                StrokeThickness = 2.0 / Viewer.scaleRatio,
                Data = mypathGeometry,
            };

            item.Width  = Length;
            item.Height = Length;
            item.RenderTransform = new RotateTransform(Angle, 0, 0);
            Canvas.SetLeft(item, Column - Length/2.0);
            Canvas.SetTop(item, Row - Length/2.0);
            this.Children.Add(item);
            this.DeselectAll();
        }

        public void RemoveRegion(string name, ShapeMode shape)
        {
            IEnumerable<ImageItem> selectedItems = null;
            if (string.IsNullOrEmpty(name) && shape != ShapeMode.None)
            {
               selectedItems  = from items in this.Children.OfType<ImageItem>()
                                where items.ItemType == shape
                                select items;
            }
            else if(shape == ShapeMode.None && !string.IsNullOrEmpty(name))
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.ItemName == name
                                select items;
            }
            else if(shape == ShapeMode.None && string.IsNullOrEmpty(name))
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

        public List<ImageItem> GetRegions(string name,ShapeMode shape)
        {
            if(string.IsNullOrEmpty(name) && shape == ShapeMode.None)
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
            else if(shape == ShapeMode.None)
            {
                selectedItems = from items in this.Children.OfType<ImageItem>()
                                where items.Name ==name
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

            return new Rect(Math.Max(0, minX - 5), Math.Max(0, minY - 5), maxX - minX + 10, maxY - minY + 10);
        }

        #endregion

        protected override Size MeasureOverride(Size constraint)
        {
            Size size = new Size();
            foreach (UIElement element in Children)
            {
                double left = Canvas.GetLeft(element);
                double top  = Canvas.GetTop(element);
                left = double.IsNaN(left) ? 0 : left;
                top  = double.IsNaN(top) ? 0 : top;

                element.Measure(constraint);

                Size desiredSize = element.DesiredSize;
                if (!double.IsNaN(desiredSize.Width) && !double.IsNaN(desiredSize.Height))
                {
                    size.Width  = Math.Max(size.Width, left + desiredSize.Width);
                    size.Height = Math.Max(size.Height, top + desiredSize.Height);
                }
            }

            // add some extra margin
            size.Width  += 10;
            size.Height += 10;
            return size;
        }
    }
}
