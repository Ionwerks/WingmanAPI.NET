using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace MonitorTile {

	public class StatusBrush : IValueConverter {

		public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) {

			switch ((WingmanAPI.Status)((int)value)) {
			case WingmanAPI.Status.Dormant:
				return new SolidColorBrush(Color.FromRgb(0x6C, 0x6D, 0x70));
			case WingmanAPI.Status.Success:
				return new SolidColorBrush(Color.FromRgb(0x8C, 0xD1, 0x3E));
			case WingmanAPI.Status.Caution:
				return new SolidColorBrush(Color.FromRgb(0xFF, 0xB2, 0x41));
			case WingmanAPI.Status.Failure:
				return new SolidColorBrush(Color.FromRgb(0xFF, 0x54, 0x3F));
			default:
				return null;
			}

		}

		public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }

	}

}
