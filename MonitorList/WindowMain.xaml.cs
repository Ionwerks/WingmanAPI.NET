using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MonitorList {

	public partial class WindowMain : Window, IDisposable {

		/* ATTRIBUTES ====================================================== */

		private static BitmapImage[] StatusIcons = new BitmapImage[] {
			new BitmapImage(new Uri("pack://application:,,,/Images/FormStatus0.ico")),
			new BitmapImage(new Uri("pack://application:,,,/Images/FormStatus1.ico")),
			new BitmapImage(new Uri("pack://application:,,,/Images/FormStatus2.ico")),
			new BitmapImage(new Uri("pack://application:,,,/Images/FormStatus3.ico"))
		};

		private static ImageBrush[] PauseGlyphs = new ImageBrush[] {
			new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Images/GlyphSuspend.png"))),
			new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Images/GlyphProceed.png")))
		};

		private WingmanAPI.Monitor Monitor = new WingmanAPI.Monitor();
		private String AccountEmail, AccountEmailDefault;
		private String ReportingKey, ReportingKeyDefault;
		private Boolean DeveloperSession;
		private Boolean? WindowFlag; // Startup=Null, Activated=True, Shutdown=False

		/* CLASS CONSTRUCTOR(S) ============================================ */

		public WindowMain() {

			InitializeComponent();

			Monitor.Started += Monitor_Started;
			Monitor.UpdateSources += Monitor_UpdateSources;
			Monitor.UpdateTasks += Monitor_UpdateTasks;
			Monitor.Stopped += Monitor_Stopped;
			Monitor.Echo += Monitor_Echo;

			AccountEmailDefault = GetRegistryString("WebAccount", null, @"SOFTWARE\Ionwerks\Wingman"); // In case the Wingman client is installed.
			ReportingKeyDefault = GetRegistryString("WebAuthority", null, @"SOFTWARE\Ionwerks\Wingman"); // In case the Wingman client is installed.
			AccountEmail = GetRegistryString("WebAccount", AccountEmailDefault, @"SOFTWARE\Ionwerks\WingmanAPI");
			ReportingKey = GetRegistryString("WebAuthority", ReportingKeyDefault, @"SOFTWARE\Ionwerks\WingmanAPI");
			DeveloperSession = GetRegistryString("WebDevelopment", "False", @"SOFTWARE\Ionwerks\WingmanAPI").Equals("True", StringComparison.OrdinalIgnoreCase);

			System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;

			ListSources.ItemsSource = Monitor.SourcesView;
			ListTasks.ItemsSource = Monitor.TasksView;

		}

		public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

		protected virtual void Dispose(bool Disposing) { if (Disposing) Monitor.Dispose(); }

		/* PUBLIC PROCEDURES =============================================== */

		/* PRIVATE PROCEDURES ============================================== */

		private void MonitorStart() {

			if (Monitor.ConnectionState == WingmanAPI.Monitor.State.Errored || String.IsNullOrEmpty(AccountEmail) || String.IsNullOrEmpty(ReportingKey)) {
				OptionsDialog(true);
			} else {
				Monitor.Start(AccountEmail, ReportingKey, DeveloperSession);
			}

		}

		private void SourcesRefresh() {

			Icon = StatusIcons[(int)Monitor.Status];

			if (ListSources.SelectedItem == null) ListSources.SelectedIndex = 0;

		}

		private void TasksRefresh() { TasksRefresh(false); }
		private void TasksRefresh(Boolean Cleared) {

			SourcesRefresh();

			if (Cleared) ListTasks.SelectedIndex = 0; // Select the highest status task.

		}

		private void OptionsDialog(Boolean Errored) {

			WindowOptions OptionsWindow = new WindowOptions();
			OptionsWindow.Owner = this;
			OptionsWindow.TextAccountEmail.Text = AccountEmail;
			OptionsWindow.TextReportingKey.Text = ReportingKey;
			OptionsWindow.TickDeveloperSession.IsChecked = DeveloperSession;

			if (OptionsWindow.ShowDialog() == true) {
				Boolean Connect = Errored;
				if (OptionsWindow.TextAccountEmail.Text.Trim() != AccountEmail) {
					AccountEmail = OptionsWindow.TextAccountEmail.Text.Trim();
					if (String.IsNullOrEmpty(AccountEmail) || AccountEmail == AccountEmailDefault) {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").DeleteValue("WebAccount", false);
					} else {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").SetValue("WebAccount", AccountEmail);
					}
					Connect = true;
				}
				if (OptionsWindow.TextReportingKey.Text.Trim() != ReportingKey) {
					ReportingKey = OptionsWindow.TextReportingKey.Text.Trim();
					if (String.IsNullOrEmpty(ReportingKey) || ReportingKey == ReportingKeyDefault) {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").DeleteValue("WebAuthority", false);
					} else {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").SetValue("WebAuthority", ReportingKey);
					}
					Connect = true;
				}
				if (OptionsWindow.TickDeveloperSession.IsChecked != DeveloperSession) {
					DeveloperSession = OptionsWindow.TickDeveloperSession.IsChecked.Value;
					Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").SetValue("WebDevelopment", DeveloperSession ? "True" : "False");
					Connect = true;
				}
				if (Monitor.ConnectionState >= WingmanAPI.Monitor.State.Started) {
					Monitor.Stop();
				} else {
					Monitor.Start(AccountEmail, ReportingKey, DeveloperSession);
				}
			} else if (Errored) {
				Close();
			}

		}

		/* PRIVATE PROCEDURES (Static) ===================================== */

		private static string GetRegistryString(string Name, string Default, string Path) {

			String Result = null;

			if (Result == null) { // Per-user setting has precedence...
				using (RegistryKey Key = Registry.CurrentUser.OpenSubKey(Path)) {
					if (Key != null) Result = (string)Key.GetValue(Name);
				}
			}

			if (Result == null) { // ...if not use any per-machine setting...
				using (RegistryKey Key = Registry.LocalMachine.OpenSubKey(Path)) {
					if (Key != null) Result = (string)Key.GetValue(Name);
				}
			}

			return (Result != null ? Result : Default); // ...or else the default.

		}

		/* EVENT PROCEDURES ================================================ */

		private void Monitor_Started(object sender, EventArgs e) {

			if (Monitor.SessionKey == null) {
				Title = "Wingman Monitor - Awaiting Session";
			} else {
				Title = String.Format("Wingman Monitor - {0}", Monitor.Account);
			}

		}

		void Monitor_UpdateSources(object sender, EventArgs e) {

			SourcesRefresh();

		}

		void Monitor_UpdateTasks(object sender, WingmanAPI.Monitor.UpdateTasksEventArgs e) {

			TasksRefresh(e.Cleared);

		}

		private void Monitor_Stopped(object sender, EventArgs e) {

			Title = "Wingman Monitor";
			Icon = new BitmapImage(new Uri("pack://application:,,,/Images/FormStatus0.ico"));

			ButtonPause.Background = PauseGlyphs[0];

			if (WindowFlag != false) MonitorStart();

		}

		private void Monitor_Echo(object sender, WingmanAPI.Monitor.EchoEventArgs e) {

			IInputElement FocusedControl = FocusManager.GetFocusedElement(this);

			TextRequestLog.Text = TextRequestLog.Text.Substring(Math.Max(TextRequestLog.Text.Length - 4096, 0)) + (TextRequestLog.Text.Length > 0 ? "\r\n" : String.Empty) + e.Text;
			TextRequestLog.Focus();
			TextRequestLog.CaretIndex = TextRequestLog.Text.Length;
			TextRequestLog.ScrollToEnd();

			FocusManager.SetFocusedElement(this, FocusedControl);

		}

		private void MainWindow_Activated(object sender, EventArgs e) {

			if (WindowFlag == null) {
				WindowFlag = true;
				MonitorStart();
			}

		}

		private void MainWindow_Closing(object sender, CancelEventArgs e) {

			WindowFlag = false; // Prevents monitor restart.

			if (Monitor.ConnectionState >= WingmanAPI.Monitor.State.Started) Monitor.Stop();

		}

		private void MainWindow_Closed(object sender, EventArgs e) {

			MainWindow_Closing(sender, null); // In case Closing isn't raised (i.e. at Shutdown perhaps).

		}

		private void TaskClear_Click(object sender, RoutedEventArgs e) {

			Monitor.ClearAlertID = ((WingmanAPI.Task)((Button)sender).DataContext).ID;

		}

		private void ListSources_SelectionChanged(object sender, SelectionChangedEventArgs e) {

			Monitor.SelectedSource = (WingmanAPI.Source)ListSources.SelectedItem;

		}

		private void WebLogin_Click(object sender, RoutedEventArgs e) {

			if (Monitor.ConnectionState == WingmanAPI.Monitor.State.Connected) {
				Monitor.DoWebLogin = true;
			} else {
				MessageBox.Show("Not connected.", "Web Login", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}

		}

		private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {

			DragMove();

		}

		private void WidgetSuspend_Click(object sender, RoutedEventArgs e) {

			if (Monitor.ConnectionState == WingmanAPI.Monitor.State.Connected) {
				if (Monitor.Paused) {
					Monitor.Paused = false;
					ButtonPause.Background = PauseGlyphs[0];
				} else {
					Monitor.Paused = true;
					ButtonPause.Background = PauseGlyphs[1];
				}
			}

		}

		private void WidgetOptions_Click(object sender, RoutedEventArgs e) {

			OptionsDialog(false);

		}

		private void WidgetDismiss_Click(object sender, RoutedEventArgs e) {

			WindowState = WindowState.Minimized;

		}

		private void WidgetDiscard_Click(object sender, RoutedEventArgs e) {

			Close();

		}

	}

}
