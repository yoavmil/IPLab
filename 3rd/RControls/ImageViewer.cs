using Microsoft.Win32;
using RControls.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RControls
{
	#region 操作模式
	public enum OpMode
	{
		Disable, // 不可操作
		Move,    // 移动模式
		Select,  // 选择模式
		Draw     // 绘制模式
	}
	#endregion

	#region 绘制形状
	public enum ShapeMode
	{
		None,       // 不绘制
		Rectangle,  // 矩形
		Line,       // 线段
		Circle,     // 圆形
		Ellipse,    // 椭圆
		Polygon,    // 多边形
		Any,        // 任意形状
		Cross,      // 十字
	}
	#endregion

	[TemplatePart(Name = ElementMainBorder, Type = typeof(Border))]
	[TemplatePart(Name = ElementMainCanvas, Type = typeof(ImageCanvas))]
	[TemplatePart(Name = ElementMainImage, Type = typeof(Image))]
	public class ImageViewer : Control
	{
		static ImageViewer()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageViewer), new FrameworkPropertyMetadata(typeof(ImageViewer)));
		}

		public ImageViewer()
		{
			CommandBindings.Add(new CommandBinding(ControlCommands.ViewMode, MenuItem_ViewClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.MoveMode, MenuItem_MoveClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.SelectMode, MenuItem_SelectClicked));

			CommandBindings.Add(new CommandBinding(ControlCommands.SaveImage, MenuItem_SaveImageClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.SaveWindow, MenuItem_SaveWindowClicked));

			CommandBindings.Add(new CommandBinding(ControlCommands.FitWindow, MenuItem_FitWindowClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.FitImage, MenuItem_FitImageClicked));

			CommandBindings.Add(new CommandBinding(ControlCommands.DrawRectangle, MenuItem_DrawRectangleClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.DrawLine, MenuItem_DrawLineClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.DrawCircle, MenuItem_DrawCircleClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.DrawEllipse, MenuItem_DrawEllipseClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.DrawPolygon, MenuItem_DrawPolygonClicked));
			CommandBindings.Add(new CommandBinding(ControlCommands.DrawAny, MenuItem_DrawAnyClicked));

			this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, Delete_Executed, Delete_Enabled));
		}

		#region 控件常量
		public const string ElementMainCanvas = "PART_MainCanvas";
		public const string ElementMainImage = "PART_MainImage";
		public const string ElementMainBorder = "PART_Border";
		#endregion

		#region 控件对象
		private Border _borderMain;
		private ImageCanvas _canvasMain;
		private Image _imageMain;
		#endregion

		#region 变量定义
		public double scaleRatio = 1.0f;
		private Point? startPoint;          // 鼠标按下起始点
		private bool isPressed = false;    // 左键是否按下
		#endregion

		#region 依赖属性

		#region 显示图像
		public ImageSource SourceImage
		{
			get { return (ImageSource)GetValue(SourceImageProperty); }
			set { SetValue(SourceImageProperty, value); }
		}
		public static readonly DependencyProperty SourceImageProperty =
			DependencyProperty.Register("SourceImage", typeof(ImageSource), typeof(ImageViewer), new PropertyMetadata(default(ImageSource), OnImageChanged));

		/// <summary>
		/// Raised when the user clicks on the image, with coordinates in original image pixels.
		/// </summary>
		public event Action<Point> ImageClicked;

		private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var Ctrl = (ImageViewer)d;
			if (Ctrl != null)
			{
				//第一次默认适应窗口
				if (e.OldValue==null)
					Ctrl.FitToWindow();
			}
		}
		#endregion

		#region 操作模式
		public OpMode OperationMode
		{
			get { return (OpMode)GetValue(OperationModeProperty); }
			set { SetValue(OperationModeProperty, value); }
		}
		public static readonly DependencyProperty OperationModeProperty =
			DependencyProperty.Register("OperationMode", typeof(OpMode), typeof(ImageViewer), new PropertyMetadata(OpMode.Move, OnOpModeChanged));
		private static void OnOpModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{

		}
		#endregion

		#region 绘制形状
		public ShapeMode DrawShape
		{
			get { return (ShapeMode)GetValue(DrawShapeProperty); }
			set { SetValue(DrawShapeProperty, value); }
		}
		public static readonly DependencyProperty DrawShapeProperty =
			DependencyProperty.Register("DrawShape", typeof(ShapeMode), typeof(ImageViewer), new PropertyMetadata(ShapeMode.None, OnShapeModeChanged));

		private static void OnShapeModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{

		}
		#endregion

		#region 图像最大尺寸
		public double MaxImageWidth
		{
			get { return (double)GetValue(MaxImageWidthProperty); }
			set { SetValue(MaxImageWidthProperty, value); }
		}
		public static readonly DependencyProperty MaxImageWidthProperty =
			DependencyProperty.Register("MaxImageWidth", typeof(double), typeof(ImageViewer), new PropertyMetadata(6000.0));


		public double MaxImageHeight
		{
			get { return (double)GetValue(MaxImageHeightProperty); }
			set { SetValue(MaxImageHeightProperty, value); }
		}
		public static readonly DependencyProperty MaxImageHeightProperty =
			DependencyProperty.Register("MaxImageHeight", typeof(double), typeof(ImageViewer), new PropertyMetadata(6000.0));
		#endregion

		#region 是否可选择
		public bool IsSelectable
		{
			get { return (bool)GetValue(IsSelectableProperty); }
			set { SetValue(IsSelectableProperty, value); }
		}
		public static readonly DependencyProperty IsSelectableProperty =
			DependencyProperty.Register("IsSelectable", typeof(bool), typeof(ImageViewer), new PropertyMetadata(true, OnSelectableChanged));

		private static void OnSelectableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var Ctrl = (ImageViewer)d;
			bool bSelectable = (bool)e.NewValue;
			if (Ctrl != null && Ctrl.OperationMode == OpMode.Select && !bSelectable)
			{
				Ctrl.OperationMode = OpMode.Move;
			}
		}
		#endregion

		#region 是否可绘制
		public bool IsDrawable
		{
			get { return (bool)GetValue(IsDrawableProperty); }
			set { SetValue(IsDrawableProperty, value); }
		}
		public static readonly DependencyProperty IsDrawableProperty =
			DependencyProperty.Register("IsDrawable", typeof(bool), typeof(ImageViewer), new PropertyMetadata(true, OnDrawableChanged));

		private static void OnDrawableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var Ctrl = (ImageViewer)d;
			bool bDrawable = (bool)e.NewValue;
			if (Ctrl != null && Ctrl.OperationMode == OpMode.Draw && !bDrawable)
			{
				Ctrl.OperationMode = OpMode.Move;
			}
		}
		#endregion

		#endregion

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			// 获取控件对象指针
			_canvasMain = GetTemplateChild(ElementMainCanvas) as ImageCanvas;
			_imageMain  = GetTemplateChild(ElementMainImage) as Image;
			_borderMain = GetTemplateChild(ElementMainBorder) as Border;

			// 鼠标操作
			if (_borderMain != null)
			{
				_borderMain.PreviewMouseDown  += _borderMain_PreviewMouseDown;
				_borderMain.PreviewMouseUp    += _borderMain_PreviewMouseUp;
				_borderMain.PreviewMouseMove  += _borderMain_PreviewMouseMove;
				_borderMain.PreviewMouseWheel += _borderMain_PreviewMouseWheel;
			}

			if (_canvasMain != null)
			{
				_canvasMain.Viewer = this;
			}
		}

		#region 鼠标操作
		private void _borderMain_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			// 每次缩放（0.1），并记录下当前的缩放比例
			double scale = e.Delta > 0 ? 1 + 0.1 : 1 - 0.1;
			Matrix matrix = _canvasMain.RenderTransform.Value;
			if (scale < 1 && matrix.M11 <= 0.01)
				scale = 1;
			else if (scale > 1 && matrix.M11 > 32)
				scale = 1;
			scaleRatio *= scale;
			Point position = e.GetPosition((IInputElement)sender);
			matrix.ScaleAt(scale, scale, position.X, position.Y);
			_canvasMain.RenderTransform = new MatrixTransform(matrix);

			e.Handled = false;
		}

		private void _borderMain_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			// 按下左键
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				// Fire callback with coordinates in image pixels
				if (SourceImage != null)
				{
					Point clicked = e.GetPosition(_canvasMain);
					ImageClicked?.Invoke(clicked);
				}

				// 左键已按下
				isPressed = true;

				// 当前为移动操作模式
				if (OperationMode == OpMode.Move)
				{
					var border = (Border)sender;
					if (border.CaptureMouse() == false)
					{
						throw new Exception("未能成功捕获鼠标");
					}

					startPoint = e.GetPosition(border);
				}
			}
		}

		private void _borderMain_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			base.OnMouseMove(e);

			// 移动模式
			if (isPressed && startPoint.HasValue && OperationMode == OpMode.Move)
			{
				Matrix matrix = _canvasMain.RenderTransform.Value;
				Point position = e.GetPosition((IInputElement)sender);
				Vector vector = (Vector)(position - startPoint);
				matrix.Translate(vector.X, vector.Y);
				_canvasMain.RenderTransform = new MatrixTransform(matrix);
				startPoint      = position;
			}

			e.Handled = false;
		}

		private void _borderMain_PreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			// 表示在鼠标左键按下后释放
			if (isPressed)
			{
				// 鼠标按下标记清除
				isPressed = false;

				// 释放鼠标捕获
				Mouse.Capture(default);
				//var border = (Border)sender;
				//if (border.IsMouseCaptured)
				//    border.ReleaseMouseCapture();

				// 将坐标点清空
				startPoint = null;
			}
		}

		#endregion

		#region 右键菜单
		private void MenuItem_SelectClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode   = OpMode.Select;
			DrawShape       = ShapeMode.Rectangle;
		}

		private void MenuItem_MoveClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Move;
		}

		private void MenuItem_ViewClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Disable;
		}

		private void MenuItem_DrawCircleClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Draw;
			DrawShape = ShapeMode.Circle;
		}

		private void MenuItem_DrawRectangleClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Draw;
			DrawShape = ShapeMode.Rectangle;
		}

		private void MenuItem_DrawLineClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Draw;
			DrawShape = ShapeMode.Line;
		}

		private void MenuItem_DrawEllipseClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Draw;
			DrawShape = ShapeMode.Ellipse;
		}

		private void MenuItem_DrawPolygonClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Draw;
			DrawShape = ShapeMode.Polygon;
		}

		private void MenuItem_DrawAnyClicked(object sender, ExecutedRoutedEventArgs e)
		{
			OperationMode = OpMode.Draw;
			DrawShape = ShapeMode.Any;
		}

		private void MenuItem_SaveWindowClicked(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				SaveFileDialog saveFileDialog = new SaveFileDialog();
				saveFileDialog.Filter = "Png Files (*.png)|*.png";
				saveFileDialog.AddExtension = true;
				if (saveFileDialog.ShowDialog() == true)
				{
					RenderTargetBitmap bmp = CreateControlSnap(_borderMain);
					PngBitmapEncoder PBE = new PngBitmapEncoder();
					PBE.Frames.Add(BitmapFrame.Create(bmp));
					using (Stream stream = File.Create(saveFileDialog.FileName))
					{
						PBE.Save(stream);
					}
				}

				MessageBox.Show("Image saved");
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to save the image, error message: {ex.Message}");
			}
		}

		private void MenuItem_SaveImageClicked(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				SaveFileDialog saveFileDialog = new SaveFileDialog();
				saveFileDialog.Filter = "Bmp Files (*.bmp)|*.bmp|Tiff Files (*.tiff)|*.tiff";
				saveFileDialog.AddExtension = true;
				if (saveFileDialog.ShowDialog() == true)
				{
					BitmapSource BS = (BitmapSource)SourceImage;

					var extension = System.IO.Path.GetExtension(saveFileDialog.FileName);
					if (extension == ".bmp")
					{
						BmpBitmapEncoder PBE = new BmpBitmapEncoder();
						PBE.Frames.Add(BitmapFrame.Create(BS));
						using (Stream stream = File.Create(saveFileDialog.FileName))
						{
							PBE.Save(stream);
						}
					}
					else if (extension == ".tiff")
					{
						TiffBitmapEncoder PBE = new TiffBitmapEncoder();
						PBE.Frames.Add(BitmapFrame.Create(BS));
						using (Stream stream = File.Create(saveFileDialog.FileName))
						{
							PBE.Save(stream);
						}
					}
                    MessageBox.Show("Image saved!");
                }
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to save the image, error message: {ex.Message}");
			}
		}

		private RenderTargetBitmap CreateControlSnap(FrameworkElement elem)
		{
			// 获取控件渲染图像像素
			RenderTargetBitmap bmp = new RenderTargetBitmap((int)elem.ActualWidth, (int)elem.ActualHeight, 96, 96, PixelFormats.Default);

			VisualBrush sourceBrush = new VisualBrush(elem);
			DrawingVisual drawingVisual = new DrawingVisual();
			DrawingContext drawingContext = drawingVisual.RenderOpen();
			using (drawingContext)
			{
				drawingContext.PushTransform(new ScaleTransform(1.0, 1.0));
				drawingContext.DrawRectangle(sourceBrush, null, new Rect(new Point(0, 0), new Point(elem.RenderSize.Width, elem.RenderSize.Height)));
			}
			bmp.Render(drawingVisual);

			return bmp;
		}

		private void MenuItem_FitWindowClicked(object sender, ExecutedRoutedEventArgs e)
		{
			FitToWindow();
		}

		private void MenuItem_FitImageClicked(object sender, ExecutedRoutedEventArgs e)
		{
			FitToImage();
		}

		#endregion

		#region 共有方法
		public void FitToWindow()
		{
			if (SourceImage == null)
				return;
			if (SourceImage.Width < 1 || SourceImage.Height < 1)
				return;
			if (_borderMain == null)
				return;

			double winWidth = _borderMain.ActualWidth;
			double winHeight = _borderMain.ActualHeight;

			// 使整个图像在窗口可见，取最小缩放比
			double ratio = Math.Min(winWidth / SourceImage.Width, winHeight / SourceImage.Height);

			// 更新变换矩阵，并将图像移动到中间位置
			Matrix matrix = _canvasMain.RenderTransform.Value;
			matrix.M11 = matrix.M22 = ratio;
			scaleRatio = ratio;
			if (winWidth / SourceImage.Width < winHeight / SourceImage.Height)
			{
				matrix.OffsetX = 0;
				matrix.OffsetY = (winHeight - SourceImage.Height * ratio) / 2.0;
			}
			else
			{
				matrix.OffsetX = (winWidth - SourceImage.Width * ratio) / 2.0;
				matrix.OffsetY = 0;
			}
			_canvasMain.RenderTransform = new MatrixTransform(matrix);
			_canvasMain.RefreshShapeThicknesses();
		}

		public void FitToImage()
		{
			scaleRatio = 1.0;
			_canvasMain.RenderTransform = new MatrixTransform(Matrix.Identity);
			_canvasMain.RefreshShapeThicknesses();
		}

		public void ScaleImage(double ratio)
		{
			if (ratio <= 0 && ratio > 16)
				return;

			Matrix matrix = _canvasMain.RenderTransform.Value;
			matrix.M11 = matrix.M22 = ratio;
			_canvasMain.RenderTransform = new MatrixTransform(matrix);
		}

		public void HighlightImage(double row, double col)
		{
			double winWidth = _borderMain.ActualWidth;
			double winHeight = _borderMain.ActualHeight;

			Matrix matrix = Matrix.Identity;
			scaleRatio      = 1.0;
			matrix.M11      = matrix.M22 = 1;
			matrix.OffsetX  = -col + winWidth/2.0;
			matrix.OffsetY  = -row + winHeight/2.0;
			_canvasMain.RenderTransform = new MatrixTransform(matrix);
		}

		#endregion

		#region 键盘操作（删除命令）
		private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			DeleteCurrentSelection();
		}

		private void DeleteCurrentSelection()
		{
			List<ImageItem> lst = _canvasMain.SelectedItems.ToList();
			foreach (ImageItem item in lst.OfType<ImageItem>())
			{
				this._canvasMain.Children.Remove(item);
			}

			this._canvasMain.DeselectAll();
		}

		private void Delete_Enabled(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = IsSelectable && _canvasMain.SelectedItems.Count<ImageItem>() > 0;
			//e.CanExecute = true;
			//e.CanExecute = this.SelectedItems.Count<DesignerItem>() > 0;
		}
		#endregion

		#region 提供外界绘制图形
		public void DispText(string stext, Point pt, double size = 100, int colSel = 2)
		{
			if (_canvasMain != null)
			{
				_canvasMain.DispText(stext, pt, size, colSel);
			}
		}


		public void DrawDefect(double row1, double column1, double row2, double column2, string name, Brush color)
		{
			if (_canvasMain != null)
				_canvasMain.DrawDefect(row1, column1, row2, column2, name, color);
		}

		public void DrawRectangle(double row1, double column1, double row2, double column2, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawRectangle(row1, column1, row2, column2, name, color, bFilled);
		}

		public void DrawRectangle2(double row, double column, double phi, double length1, double length2, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawRectangle2(row, column, phi, length1, length2, name, color, bFilled);
		}

		public void DrawLine(double rowbegin, double columnbegin, double rowend, double columnend, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawLine(rowbegin, columnbegin, rowend, columnend, name, color, bFilled);
		}

		public void DrawLine(double[] rowbegin, double[] columnbegin, double[] rowend, double[] columnend, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawLine(rowbegin, columnbegin, rowend, columnend, name, color, bFilled);
		}

		public void DrawCircle(double row, double column, double radius, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawCircle(row, column, radius, name, color, bFilled);
		}

		public void DrawEllipse(double Row, double Column, double Angle, double Ra, double Rb, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawEllipse(Row, Column, Angle, Ra, Rb, name, color, bFilled);
		}

		public void DrawPolygon(List<Point> pts, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawPolygon(pts, name, color, bFilled);
		}

		public void DrawPolygons(IReadOnlyList<List<Point>> polygons, string name, Brush color)
		{
			if (_canvasMain != null)
				_canvasMain.DrawPolygons(polygons, name, color);
		}

		public void DrawAnyShape(List<Point> pts, string name, Brush color, bool bFilled)
		{
			if (_canvasMain != null)
				_canvasMain.DrawAnyShape(pts, name, color, bFilled);
		}

		public void DrawCross(double Row, double Column, double Angle, string name, int Length, Brush Color)
		{
			if (_canvasMain != null)
			{
				_canvasMain.DrawCross(Row, Column, Angle, name, Length, Color);
			}
		}

		public void RemoveRegion(string name, ShapeMode shape)
		{
			if (_canvasMain != null)
				_canvasMain.RemoveRegion(name, shape);
		}

		public List<ImageItem> GetRegions(string name, ShapeMode shape)
		{
			if (_canvasMain != null)
				return _canvasMain.GetRegions(name, shape);

			return new List<ImageItem>();
		}

		#endregion
	}
}
