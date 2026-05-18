using System.Windows.Input;

namespace RControls.Interactivity
{
	public static class ControlCommands
	{
		/// <summary>
		///     查看模式
		/// </summary>
		public static RoutedCommand ViewMode { get; } = new RoutedCommand(nameof(ViewMode), typeof(ControlCommands));

		/// <summary>
		///     选择模式
		/// </summary>
		public static RoutedCommand SelectMode { get; } = new RoutedCommand(nameof(SelectMode), typeof(ControlCommands));

		/// <summary>
		///     移动模式
		/// </summary>
		public static RoutedCommand MoveMode { get; } = new RoutedCommand(nameof(MoveMode), typeof(ControlCommands));

		/// <summary>
		///     绘制模式
		/// </summary>
		public static RoutedCommand DrawMode { get; } = new RoutedCommand(nameof(DrawMode), typeof(ControlCommands));

		/// <summary>
		///     保存图像
		/// </summary>
		public static RoutedCommand SaveImage { get; } = new RoutedCommand(nameof(SaveImage), typeof(ControlCommands));

		/// <summary>
		///     保存窗口
		/// </summary>
		public static RoutedCommand SaveWindow { get; } = new RoutedCommand(nameof(SaveWindow), typeof(ControlCommands));

		/// <summary>
		///     适合窗口
		/// </summary>
		public static RoutedCommand FitWindow { get; } = new RoutedCommand(nameof(FitWindow), typeof(ControlCommands));

		/// <summary>
		///     适合图像
		/// </summary>
		public static RoutedCommand FitImage { get; } = new RoutedCommand(nameof(FitImage), typeof(ControlCommands));

		/// <summary>
		///     绘制矩形
		/// </summary>
		public static RoutedCommand DrawRectangle { get; } = new RoutedCommand(nameof(DrawRectangle), typeof(ControlCommands));

		/// <summary>
		///     绘制线段
		/// </summary>
		public static RoutedCommand DrawLine { get; } = new RoutedCommand(nameof(DrawLine), typeof(ControlCommands));

		/// <summary>
		///     绘制圆形
		/// </summary>
		public static RoutedCommand DrawCircle { get; } = new RoutedCommand(nameof(DrawCircle), typeof(ControlCommands));

		/// <summary>
		///     绘制椭圆
		/// </summary>
		public static RoutedCommand DrawEllipse { get; } = new RoutedCommand(nameof(DrawEllipse), typeof(ControlCommands));

		/// <summary>
		///     绘制多边形
		/// </summary>
		public static RoutedCommand DrawPolygon { get; } = new RoutedCommand(nameof(DrawPolygon), typeof(ControlCommands));

		/// <summary>
		///     绘制任意形状
		/// </summary>
		public static RoutedCommand DrawAny { get; } = new RoutedCommand(nameof(DrawAny), typeof(ControlCommands));
	}
}
