using System;
using System.Globalization;
using System.Windows.Data;

namespace RControls.Converts
{
	public class ShapeModeBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return false;

			ShapeMode srcmode = (ShapeMode)value;
			ShapeMode tarmode = (ShapeMode)Enum.Parse(typeof(ShapeMode), (string)parameter);
			if (srcmode == tarmode)
				return true;

			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return null;

			ShapeMode tarmode = (ShapeMode)Enum.Parse(typeof(ShapeMode), (string)parameter);
			if ((bool)value == true)
				return tarmode;

			return null;
		}
	}
}
