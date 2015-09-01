using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MonitorTile {

	public partial class WindowOptions : Window {

		/* ATTRIBUTES ====================================================== */

		private Boolean WindowFlag;

		/* CLASS CONSTRUCTOR(S) ============================================ */

		public WindowOptions() {

			InitializeComponent();

		}

		/* PUBLIC PROCEDURES =============================================== */

		/* PRIVATE PROCEDURES ============================================== */

		/* EVENT PROCEDURES ================================================ */

		private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {

			DragMove();

		}

		private void WidgetCommit_Click(object sender, RoutedEventArgs e) {

			DialogResult = true;

			Close();

		}

		private void TextBox_GotFocus(object sender, RoutedEventArgs e) {

			TextBox Control = (TextBox)sender;

			if (Control != null) {
				Control.SelectAll();
			}

		}

		private void TextBox_MouseDown(object sender, MouseButtonEventArgs e) {

			TextBox Control = (TextBox)sender;

			if (Control != null && Control.IsKeyboardFocusWithin != true) {
				Control.Focus(); e.Handled = true;
			}

		}

		private void OptionsWindow_Activated(object sender, EventArgs e) {

			if (WindowFlag != true) {
				WindowFlag = true;
				TextAccountEmail.Focus();
			}

		}

	}

}
