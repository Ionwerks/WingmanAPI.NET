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

namespace MonitorTile {

	public partial class WindowMain : Window, IDisposable {

		private static BitmapImage[] TaskIcons = new BitmapImage[] {
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType0.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType1.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType2.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType3.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType4.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType5.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType6.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType7.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType8.png")),
			new BitmapImage(new Uri("pack://application:,,,/Images/TaskType9.png"))
		};

		private static SolidColorBrush[] StatusBrushes = new SolidColorBrush[] {
			new SolidColorBrush(Color.FromRgb(0x35, 0x5A, 0x9D)),
			new SolidColorBrush(Color.FromRgb(0x5A, 0x9F, 0x0C)),
			new SolidColorBrush(Color.FromRgb(0xDC, 0x80, 0x0F)),
			new SolidColorBrush(Color.FromRgb(0xD8, 0x22, 0x0D))
		};

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
		private Int32 WebsiteOpen;
		private Boolean DeveloperSession;
		private Boolean? WindowFlag; // Startup=Null, Activated=True, Shutdown=False
		private WingmanAPI.Task SelectedTask;

		/* CLASS CONSTRUCTOR(S) ============================================ */

		public WindowMain() {

			InitializeComponent();

			Monitor.Started += Monitor_Started;
			Monitor.UpdateSources += Monitor_UpdateSources;
			Monitor.UpdateTasks += Monitor_UpdateTasks;
			Monitor.Stopped += Monitor_Stopped;
			Monitor.Echo += Monitor_Echo;

			AccountEmailDefault = GetRegistryString("WebAccount", null, @"SOFTWARE\Ionwerks\Wingman"); // In case the Wingman client is installed.
			ReportingKeyDefault = GetRegistryString("WebAuthority", null, @"SOFTWARE\Ionwerks\Wingman");
			AccountEmail = GetRegistryString("WebAccount", AccountEmailDefault, @"SOFTWARE\Ionwerks\WingmanAPI");
			ReportingKey = GetRegistryString("WebAuthority", ReportingKeyDefault, @"SOFTWARE\Ionwerks\WingmanAPI");
			WebsiteOpen = Math.Min(2, Math.Max(0, Int32.Parse(GetRegistryString("WebLogin", "0", @"SOFTWARE\Ionwerks\WingmanAPI"))));
			DeveloperSession = GetRegistryString("WebDevelopment", "False", @"SOFTWARE\Ionwerks\WingmanAPI").Equals("True", StringComparison.OrdinalIgnoreCase);

			System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;

		}

		public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

		protected virtual void Dispose(bool Disposing) { if (Disposing) Monitor.Dispose(); }

		/* PUBLIC PROCEDURES =============================================== */

		/* PRIVATE PROCEDURES ============================================== */

		private void MonitorStart() { MonitorStart(false); }
		private void MonitorStart(Boolean Errored) {

			if (Errored || String.IsNullOrEmpty(AccountEmail) || String.IsNullOrEmpty(ReportingKey)) {
				OptionsDialog(true);
			} else {
				Monitor.Start(AccountEmail, ReportingKey, DeveloperSession);
			}

		}

		private void ViewRefresh() { // Should use bindings but life's too short to be typing out irrelevant boilerplate in an example...

			if (Monitor.Connected != true) {
				SelectedTask = null; // In case this was set before disconnection.
				Icon = StatusIcons[(int)WingmanAPI.Status.Dormant];
				Background = StatusBrushes[(int)WingmanAPI.Status.Dormant];
				TextTitle.Text = "Disconnected!";
				ButtonBack.Visibility = Visibility.Collapsed;
				ListItems.ItemsSource = Monitor.SourcesView;
				ImageTaskIcon.Source = null;
				TextTaskDescription.Text = null;
				TextTaskDate.Text = null;
				TextTaskDate.Visibility = ButtonClear.Visibility = Visibility.Collapsed;
				BorderPending.Background = StatusBrushes[(int)WingmanAPI.Status.Dormant];
				TextPending.Visibility = BorderPending.Visibility = Visibility.Collapsed;
				TextTaskDetail.Text = null;
				ViewList.Visibility = Visibility.Collapsed;
				ViewTask.Visibility = Visibility.Collapsed;
				ViewEmpty.Visibility = Visibility.Visible;
			} else if (Monitor.SelectedSource == null) {
				SelectedTask = null; // In case this was set before SelectedSource was removed.
				Icon = StatusIcons[(int)Monitor.Status];
				Background = StatusBrushes[(int)Monitor.Status];
				TextTitle.Text = Monitor.Account;
				ButtonBack.Visibility = Visibility.Collapsed;
				ListItems.ItemsSource = Monitor.SourcesView;
				ImageTaskIcon.Source = null;
				TextTaskDescription.Text = null;
				TextTaskDate.Text = null;
				TextTaskDate.Visibility = ButtonClear.Visibility = Visibility.Collapsed;
				BorderPending.Background = StatusBrushes[(int)WingmanAPI.Status.Dormant];
				TextPending.Visibility = BorderPending.Visibility = Visibility.Collapsed;
				TextTaskDetail.Text = null;
				ViewList.Visibility = Visibility.Visible;
				ViewTask.Visibility = Visibility.Collapsed;
				ViewEmpty.Visibility = (Monitor.SourcesView.IsEmpty ? Visibility.Visible : Visibility.Collapsed);
			} else if (SelectedTask == null || Monitor.Tasks.SingleOrDefault(x => x.Source == Monitor.SelectedSource.ID && x.ID == SelectedTask.ID) == null) { // Check that SelectedTask still exists.
				SelectedTask = null; // In case SelectedTask was removed.
				Icon = StatusIcons[(int)Monitor.SelectedSource.Status];
				Background = StatusBrushes[(int)Monitor.SelectedSource.Status];
				TextTitle.Text = Monitor.SelectedSource.Description;
				ButtonBack.Visibility = Visibility.Visible;
				ListItems.ItemsSource = Monitor.TasksView;
				ImageTaskIcon.Source = null;
				TextTaskDescription.Text = null;
				TextTaskDate.Text = null;
				TextTaskDate.Visibility = ButtonClear.Visibility = Visibility.Collapsed;
				BorderPending.Background = StatusBrushes[(int)WingmanAPI.Status.Dormant];
				TextPending.Visibility = BorderPending.Visibility = Visibility.Collapsed;
				TextTaskDetail.Text = null;
				ViewList.Visibility = Visibility.Visible;
				ViewTask.Visibility = Visibility.Collapsed;
				ViewEmpty.Visibility = (Monitor.TasksView.IsEmpty ? Visibility.Visible : Visibility.Collapsed);
			} else {
				// SelectedTask = SelectedTask;
				Icon = StatusIcons[(int)SelectedTask.Status];
				Background = StatusBrushes[(int)SelectedTask.Status];
				TextTitle.Text = Monitor.SelectedSource.Description;
				ButtonBack.Visibility = Visibility.Visible;
				ListItems.ItemsSource = null;
				ImageTaskIcon.Source = TaskIcons[(int)SelectedTask.Type];
				TextTaskDescription.Text = SelectedTask.Description;
				TextTaskDate.Text = String.Format("Posted {0:g}", SelectedTask.Alerted);
				TextTaskDate.Visibility = ButtonClear.Visibility = (SelectedTask.Status > WingmanAPI.Status.Success ? Visibility.Visible : Visibility.Collapsed);
				BorderPending.Background = StatusBrushes[(int)SelectedTask.Pending];
				TextPending.Visibility = BorderPending.Visibility = (SelectedTask.Pending < SelectedTask.Status ? Visibility.Visible : Visibility.Collapsed);
				TextTaskDetail.Text = SelectedTask.Detail;
				ViewList.Visibility = Visibility.Collapsed;
				ViewTask.Visibility = Visibility.Visible;
				ViewEmpty.Visibility = (Monitor.TasksView.IsEmpty ? Visibility.Visible : Visibility.Collapsed);
			}

		}

		private void OptionsDialog(Boolean Errored) {

			WindowOptions OptionsWindow = new WindowOptions();
			OptionsWindow.Owner = this;
			OptionsWindow.TextAccountEmail.Text = AccountEmail;
			OptionsWindow.TextReportingKey.Text = ReportingKey;
			OptionsWindow.ListWebsiteOpen.SelectedIndex = WebsiteOpen;
			OptionsWindow.TickDeveloperSession.IsChecked = DeveloperSession;

			if (OptionsWindow.ShowDialog().Equals(true)) {
				Boolean Connect = Errored;
				if (OptionsWindow.TextAccountEmail.Text.Trim().Equals(AccountEmail) != true) {
					AccountEmail = OptionsWindow.TextAccountEmail.Text.Trim();
					if (String.IsNullOrEmpty(AccountEmail) || AccountEmail.Equals(AccountEmailDefault)) {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").DeleteValue("WebAccount", false);
					} else {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").SetValue("WebAccount", AccountEmail);
					}
					Connect = true;
				}
				if (OptionsWindow.TextReportingKey.Text.Trim().Equals(ReportingKey) != true) {
					ReportingKey = OptionsWindow.TextReportingKey.Text.Trim();
					if (String.IsNullOrEmpty(ReportingKey) || ReportingKey.Equals(ReportingKeyDefault)) {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").DeleteValue("WebAuthority", false);
					} else {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").SetValue("WebAuthority", ReportingKey);
					}
					Connect = true;
				}
				if (OptionsWindow.ListWebsiteOpen.SelectedIndex.Equals(WebsiteOpen) != true) {
					WebsiteOpen = OptionsWindow.ListWebsiteOpen.SelectedIndex;
					if (WebsiteOpen == 0) {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").DeleteValue("WebLogin", false);
					} else {
						Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").SetValue("WebLogin", WebsiteOpen.ToString());
					}
				}
				if (OptionsWindow.TickDeveloperSession.IsChecked.Equals(DeveloperSession) != true) {
					DeveloperSession = OptionsWindow.TickDeveloperSession.IsChecked.Value;
					Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Ionwerks\WingmanAPI").SetValue("WebDevelopment", DeveloperSession ? "True" : "False");
					Connect = true;
				}
				if (Connect) {
					if (Monitor.Connected == true) {
						Monitor.Stop();
					} else {
						Monitor.Start(AccountEmail, ReportingKey, DeveloperSession);
					}
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
				TextTitle.Text = "Awaiting Session...";
			} else {
				ViewRefresh();
			}

		}

		void Monitor_UpdateSources(object sender, EventArgs e) {

			ViewRefresh();

		}

		void Monitor_UpdateTasks(object sender, WingmanAPI.Monitor.UpdateTasksEventArgs e) {

			ViewRefresh();

		}

		private void Monitor_Stopped(object sender, WingmanAPI.Monitor.StoppedEventArgs e) {

			ViewRefresh();

			ButtonPause.Background = PauseGlyphs[0];

			if (WindowFlag.Equals(false) != true) MonitorStart(e.Errored);

		}

		private void Monitor_Echo(object sender, WingmanAPI.Monitor.EchoEventArgs e) {

			Console.WriteLine(e.Text);

			if (e.Text.IndexOf("Not enabled for this account.") > -1) MessageBox.Show("Not enabled for this account.");

		}

		private void MainWindow_Activated(object sender, EventArgs e) {

			if (WindowFlag.Equals(null)) {
				WindowFlag = true;
				MonitorStart();
			}

		}

		private void MainWindow_Closing(object sender, CancelEventArgs e) {

			WindowFlag = false; // Prevent service restart.

			if (Monitor.Connected == true) Monitor.Stop();

		}

		private void MainWindow_Closed(object sender, EventArgs e) {

			MainWindow_Closing(sender, null); // In case Closing isn't raised (i.e. at Shutdown perhaps).

		}

		private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e) {

			if (e.OriginalSource.GetType() == typeof(Grid)) DragMove();

		}

		private void ButtonClear_Click(object sender, RoutedEventArgs e) {

			Monitor.ClearAlertID = SelectedTask.ID;

		}

		private void ButtonBack_Click(object sender, RoutedEventArgs e) {

			if (SelectedTask != null) { // We're currently viewing a task, go back to the task list.
				SelectedTask = null;
			} else { // We're currently viewing a source, go back to the source list.
				Monitor.SelectedSource = null;
			}

			ViewRefresh();

		}

		private void ListItem_Click(object sender, RoutedEventArgs e) {

			if (Monitor.SelectedSource != null) { // We're currently viewing the task list, move to the selected task (or open Web Reporting).
				if (WebsiteOpen > 0) {
					Monitor.DoWebLogin = true;
				} else {
					SelectedTask = (WingmanAPI.Task)((Button)sender).DataContext;
				}
			} else { // We're currently viewing the source list, move to the selected source (or open Web Reporting).
				if (WebsiteOpen > 1) {
					Monitor.DoWebLogin = true;
				} else {
					Monitor.SelectedSource = (WingmanAPI.Source)((Button)sender).DataContext;
				}
			}

			ViewRefresh();

		}

		private void WidgetSuspend_Click(object sender, RoutedEventArgs e) {

			if (Monitor.Connected ?? false) {
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
