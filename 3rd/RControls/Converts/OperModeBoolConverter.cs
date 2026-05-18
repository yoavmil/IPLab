using System;
using System.Globalization;
using System.Windows.Data;

namespace RControls.Converts
{
	public class OperModeBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return false;

			OpMode srcmode = (OpMode)value;
			OpMode tarmode = (OpMode)Enum.Parse(typeof(OpMode), (string)parameter);
			if (srcmode == tarmode)
				return true;

			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return null;

			OpMode tarmode = (OpMode)Enum.Parse(typeof(OpMode), (string)parameter);
			if ((bool)value == true)
				return tarmode;

			return null;
		}
	}
}
