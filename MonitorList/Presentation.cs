using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MonitorList {

	public class StatusImage : IValueConverter {

		public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) {

			return new BitmapImage(new Uri(String.Format("pack://application:,,,/Images/ListStatus{0}.png", (int)value)));

		}

		public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }

	}

	public class DetailVisibility : IValueConverter {

		public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) {

			return ((Boolean)value ? Visibility.Visible : Visibility.Collapsed);

		}

		public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }

	}

	public class AlertsVisibility : IMultiValueConverter {

		public object Convert(object[] values, Type type, object parameter, System.Globalization.CultureInfo culture) {

			return ((Boolean)values[0] && (WingmanAPI.Status)values[1] > WingmanAPI.Status.Success ? Visibility.Visible : Visibility.Collapsed);

		}

		public object[] ConvertBack(object value, Type[] type, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }

	}

	public class PendingVisibility : IMultiValueConverter {

		public object Convert(object[] values, Type type, object parameter, System.Globalization.CultureInfo culture) {

			return ((Boolean)values[0] && (WingmanAPI.Status)values[1] > (WingmanAPI.Status)values[2] ? Visibility.Visible : Visibility.Collapsed);

		}

		public object[] ConvertBack(object value, Type[] type, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }

	}

	public class EmptyVisibility : IValueConverter {

		public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) {

			return ((Int32)value == 0 ? Visibility.Visible : Visibility.Collapsed);

		}

		public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }

	}

}
