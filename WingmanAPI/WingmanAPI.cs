using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Data;
using System.Windows.Threading;

namespace WingmanAPI {

	public enum Status { Dormant, Success, Caution, Failure }

	public enum TaskType { Undefined, HeartBeat, SysMetric, DiskSpace, FileCheck, ProcessUp, LogReview, EventLogs, NetResult, DOSOutput }

	public class Source : INotifyPropertyChanged {
		public event PropertyChangedEventHandler PropertyChanged;
		public Int64 ID { get; set; }
		public Int32 Order { get; set; }
		public Boolean Current { get; set; }
		private Status _Status;
		public Status Status { get { return _Status; } set { if (_Status != value) { _Status = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Status")); } } }
		private String _Description;
		public String Description { get { return _Description; } set { if (_Description != value) { _Description = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Description")); } } }
		public String Platform {
			get {
				switch (ID & 0x1F000000000000) {
				case 0x00000000000000: return "Unspecified OS";
				case 0x01000000000000: return "Unspecified 64-Bit OS";
				case 0x02000000000000: return "Windows XP";
				case 0x03000000000000: return "Windows XP x64";
				case 0x04000000000000: return "Windows Vista";
				case 0x05000000000000: return "Windows Vista x64";
				case 0x06000000000000: return "Windows 7";
				case 0x07000000000000: return "Windows 7 x64";
				case 0x08000000000000: return "Windows 8"; // & 8.1
				case 0x09000000000000: return "Windows 8 x64"; // & 8.1
				case 0x0A000000000000: return "Windows 10";
				case 0x0B000000000000: return "Windows 10 x64";
				case 0x0E000000000000: return "Unix/Linux Workstation I386";
				case 0x0F000000000000: return "Unix/Linux Workstation x86-64";
				case 0x12000000000000: return "Windows Server 2003";
				case 0x13000000000000: return "Windows Server 2003 x64";
				case 0x14000000000000: return "Windows Server 2008"; // & 2008 R2
				case 0x15000000000000: return "Windows Server 2008 x64"; // & 2008 R2
				case 0x16000000000000: return "Windows Server 2012"; // & 2012 R2
				case 0x17000000000000: return "Windows Server 2012 x64"; // & 2012 R2
				case 0x18000000000000: return "Windows Server 2016";
				case 0x19000000000000: return "Windows Server 2016 x64";
				case 0x1E000000000000: return "Unix/Linux Server i386";
				case 0x1F000000000000: return "Unix/Linux Server x86-64";
				default: return "Windows";
				}
			}
		}
	}

	public class Task : INotifyPropertyChanged {
		public event PropertyChangedEventHandler PropertyChanged;
		public Int64 ID { get; set; }
		public Int32 Order { get; set; }
		public Int64 Source { get; set; }
		public TaskType Type { get; set; }
		private Status _Status;
		public Status Status { get { return _Status; } set { if (_Status != value) { _Status = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Status")); } } }
		private Status _Pending;
		public Status Pending { get { return _Pending; } set { if (_Pending != value) { _Pending = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Pending")); } } }
		public DateTime? Alerted { get; set; }
		private String _Description;
		public String Description { get { return _Description; } set { if (_Description != value) { _Description = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Description")); } } }
		private String _Detail;
		public String Detail { get { return (_Detail ?? "No alerts."); } set { if (_Detail != value) { _Detail = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Detail")); } } }
	}

	public class Monitor : IDisposable {

		/* EVENTS ========================================================== */

		public class EchoEventArgs : EventArgs {public string Text { get; internal set; } public EchoEventArgs(string Echo) { Text = Echo; }}
		public class UpdateTasksEventArgs : EventArgs {public bool Cleared { get; internal set; } public UpdateTasksEventArgs(bool Clear) { Cleared = Clear; }}

		public event EventHandler Started;
		public event EventHandler<EchoEventArgs> Echo;
		public event EventHandler UpdateSources;
		public event EventHandler<UpdateTasksEventArgs> UpdateTasks;
		public event EventHandler Stopped;

		/* ATTRIBUTES ====================================================== */

		public enum State { Errored = -1, Stopped, Started, Connected }

		public State ConnectionState { get; internal set; }
		public Version ServiceVersion { get; internal set; }
		public String Account { get; internal set; }
		public String SessionKey { get; internal set; }
		public Int32 SessionAccess { get; internal set; } // 1:Query 2:Query/Clear 3:Query/Clear/Login(Reporting)  4:Query/Clear/Login(Reporting/Groups).
		public TimeSpan SessionLife { get; internal set; }
		public DateTime Refresh { get; internal set; }
		public Status Status { get; internal set; }

		public ObservableCollection<Source> Sources { get; internal set; }
		public ObservableCollection<Task> Tasks { get; internal set; }
		public ICollectionView SourcesView { get; internal set; }
		public ICollectionView TasksView { get; internal set; }

		private Source _SelectedSource;
		public Source SelectedSource {get {return _SelectedSource;} set {if (_SelectedSource == value) return; _SelectedSource = value; if (_SelectedSource == null || _SelectedSource.Current) TasksView.Refresh(); if (ConnectionState > State.Stopped) UpdateWait.Set(); }}
		private Int64? _ClearAlertID; // Heartbeat tasks have a 0 ID so this has to be nullable.
		public Int64? ClearAlertID { get { return _ClearAlertID; } set { if (_ClearAlertID == value || _ClearAlertID.HasValue) return; _ClearAlertID = value; if (ConnectionState >= State.Started) UpdateWait.Set(); } }
		private Boolean _DoWebLogin;
		public Boolean DoWebLogin { get { return _DoWebLogin; } set { if (_DoWebLogin == value || _DoWebLogin) return; _DoWebLogin = value; if (ConnectionState >= State.Started) UpdateWait.Set(); } }
		private Boolean _Paused;
		public Boolean Paused { get { return _Paused; } set { if (_Paused == value) return; _Paused = value; if (ConnectionState >= State.Started) UpdateWait.Set(); } }

		private HttpRequestCachePolicy CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
		private AutoResetEvent UpdateExit = new AutoResetEvent(false);
		private AutoResetEvent UpdateWait = new AutoResetEvent(false);
		private Dispatcher UpdateDispatcher;
		private DateTime LastRequest;

		private Regex ParseSource = new Regex(@"(\d+)\s(\d)\s(.+)"); // <SourceID> <Status> <SourceName>
		private struct ParsedSource { public static int ID = 1; public static int Status = 2; public static int Description = 3; public static int Count = 4;}
		private Regex ParseTask = new Regex(@"(\d+)\s(\d)\s(.+)"); // <TaskID> <TaskType> <TaskDescription>
		private struct ParsedTask { public static int ID = 1; public static int Type = 2; public static int Description = 3; public static int Count = 4;}
		private Regex ParseAlert = new Regex(@"(\d+)\s(\d)\s(\d)\s(\d+)\s(.+?)\r(.+)"); // <TaskID> <Status> <StatusPending> <AlertTimeStamp> <TaskDescription\rAlertDetail1\rAlertDetail2...\rAlertDetailN>
		private struct ParsedAlert { public static int ID = 1; public static int Status = 2; public static int Pending = 3; public static int Alerted = 4; public static int Description = 5; public static int Detail = 6; public static int Count = 7;}

		private const String API_VERSION = "0.1";
		private const String URL_LOGIN = "https://ionwerks.net/wingman/query";
		private const String URL_QUERY = "http://ionwerks.net/wingman/query";

		/* CLASS CONSTRUCTOR(S) ============================================ */

		public Monitor() {

			Sources = new ObservableCollection<Source>();
			SourcesView = CollectionViewSource.GetDefaultView(Sources);
			SourcesView.SortDescriptions.Add(new SortDescription("Status", ListSortDirection.Descending));
			SourcesView.SortDescriptions.Add(new SortDescription("Order", ListSortDirection.Ascending));

			Tasks = new ObservableCollection<Task>();
			TasksView = CollectionViewSource.GetDefaultView(Tasks);
			TasksView.Filter = new Predicate<object>(x => ((Task)x).Source == (SelectedSource == null ? 0 : SelectedSource.ID));
			TasksView.SortDescriptions.Add(new SortDescription("Status", ListSortDirection.Descending));
			TasksView.SortDescriptions.Add(new SortDescription("Order", ListSortDirection.Ascending));

		}

		public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

		protected virtual void Dispose(bool Disposing) {

			if (Disposing) {
				if (ConnectionState > State.Stopped) {
					ConnectionState = State.Stopped;
					UpdateWait.Set();
					UpdateExit.WaitOne();
				}
				UpdateWait.Dispose();
				UpdateExit.Dispose();
			}
		
		}

		/* PUBLIC PROCEDURES =============================================== */

		public void Start(string AccountEmail, string ReportingKey, bool DevelopmentSession) {

			if (ConnectionState > State.Stopped) throw new Exception("Already started.");

			UpdateDispatcher = Dispatcher.CurrentDispatcher;

			ConnectionState = State.Started;

			BackgroundWorker UpdateThread = new BackgroundWorker();
			UpdateThread.DoWork += MonitorWork;
			UpdateThread.RunWorkerCompleted += MonitorDone;
			UpdateThread.RunWorkerAsync(new Tuple<string, string, bool>(AccountEmail, ReportingKey, DevelopmentSession));

		}

		public void Stop() {

			ConnectionState = State.Stopped;

			UpdateWait.Set();

		}

		/* PRIVATE PROCEDURES ============================================== */

		private void MonitorWork(object sender, DoWorkEventArgs e) {

			Tuple<string, string, bool> Parameters = (Tuple<string, string, bool>)e.Argument;
			String[] ReplyFields = null;
			MatchCollection Matched = null;
			GroupCollection Matches = null;
			TimeSpan WaitPeriod = TimeSpan.Zero;

			while (ConnectionState >= State.Started) {
				if (ConnectionState != State.Connected) {
					#region Establish a session.
					switch (MakeRequest(String.Format("{0}?v={1}&a={2}&r={3}{4}", URL_LOGIN, API_VERSION, Parameters.Item1, Parameters.Item2, (Parameters.Item3 ? "&d" : String.Empty)), ref ReplyFields)) {
					case "OK": // OK|0.1|o1Wk2YIE4h7DIvPglNrz|1|3600|Development Session
						UpdateDispatcher.Invoke((Action)delegate {
							if (ReplyFields.Length >= 6 && ReplyFields[1] == API_VERSION) { // Service should confirm it supports our API version by passing the same back (otherwise it will return the current/latest).
								ServiceVersion = new Version(ReplyFields[1]);
								SessionKey = ReplyFields[2];
								SessionAccess = int.Parse(ReplyFields[3]);
								SessionLife = TimeSpan.FromSeconds(int.Parse(ReplyFields[4]));
								Account = ReplyFields[5];
								Refresh = DateTime.MinValue;
								Status = Status.Dormant;
								ConnectionState = State.Connected;
								if (Started != null) Started.Invoke(this, new EventArgs());
							} else {
								ConnectionState = State.Errored;
							}
						});
						break;
					case "NA": // NA|180|Not available. /* A session has been recently established or relinquished. */
						UpdateDispatcher.Invoke((Action)delegate {
							Refresh = DateTime.Now.AddSeconds(Double.Parse(ReplyFields[1])); // ReplyFields[1] tells us in how many more seconds we should retry for a session.
							if (Started != null) Started.Invoke(this, new EventArgs());
						});
						break;
					case "NO": // NO|Malformed request. // NO|Invalid account or key. // NO|Unsupported version. // NO|Not enabled for this account.
					case "KO": // KO|Service error.
					default:
						ConnectionState = State.Errored;
						break;
					}
					#endregion
				}
				if (ConnectionState == State.Connected && Refresh <= DateTime.Now) {
					#region Refresh the list of sources.
					switch (MakeRequest(String.Format("{0}?k={1}&s", URL_QUERY, SessionKey), ref ReplyFields)) {
					case "OK": // OK|180 (No Change) or OK|180|<Source1>|<Source2>|...<SourceN>
						UpdateDispatcher.Invoke((Action)delegate {
							Refresh = DateTime.Now.AddSeconds(Double.Parse(ReplyFields[1])); // ReplyFields[1] tells us in how many more seconds we can refresh the source list.
							if (ReplyFields.Length > 2) { // Response includes source data.
								Int32 Index = 1; // Maintains a consistent source order (within Status grouping).
								for (int i = 0; i < Sources.Count; i++) Sources[i].Status = Status.Dormant; // Mark existing sources with a zero Status (so we can delete any that aren't updated).
								for (int i = 2; i < ReplyFields.Length - 1; i++) { // The source list is terminated (by a LF) so we leave off the last (empty) item.
									if ((Matched = ParseSource.Matches(ReplyFields[i])).Count == 1 && (Matches = Matched[0].Groups).Count == ParsedSource.Count) { // Parse (and confirm match count).
										Int64 SourceID = Int64.Parse(Matches[ParsedSource.ID].ToString());
										Source Existing = Sources.SingleOrDefault(x => x.ID == SourceID);
										if (Existing == null) {
											Sources.Add(new Source() {
												ID = SourceID,
												Order = Index++,
												Current = false,
												Status = (Status)Int32.Parse(Matches[ParsedSource.Status].ToString()),
												Description = Matches[ParsedSource.Description].ToString()
											});
										} else {
											Existing.Order = Index++;
											Existing.Current = false;
											Existing.Status = (Status)Int32.Parse(Matches[ParsedSource.Status].ToString());
											Existing.Description = Matches[ParsedSource.Description].ToString();
										}
									} else {} // Parse failure - should probably error.
								}
								for (int i = Sources.Count - 1; i >= 0; i--) { // Sources not updated (with a non-zero Status) no longer exist...
									if (Sources[i].Status == Status.Dormant) {
										for (int j = Tasks.Count - 1; j >= 0; j--) {
											if (Tasks[j].Source == Sources[i].ID) Tasks.Remove(Tasks[j]);
										}
										if (SelectedSource == Sources[i]) SelectedSource = null;
										Sources.Remove(Sources[i]);
									}
								}
								SourcesView.Refresh();
								Status = (Status)(Sources.Max(x => (Int32?)x.Status) ?? 0); // Revise the overall monitor status from the status of all sources.
								if (UpdateSources != null) UpdateSources.Invoke(this, new EventArgs());
							}
						});
						break;
					case "NA": // NA|180|Not available. /* We've somehow failed to observe the polling strictures, if we want to recover we need to observe the new list refresh pause. */
						UpdateDispatcher.Invoke((Action)delegate {
							Refresh = DateTime.Now.AddSeconds(Double.Parse(ReplyFields[1])); // ReplyFields[1] tells us in how many more seconds we can refresh the source list.
						});
						break;
					case "NO": // NO|Malformed request. // NO|Session not found.
					case "KO": // KO|Service error.
					default:
						ConnectionState = State.Errored;
						break;
					}
					#endregion
				}
				if (ConnectionState == State.Connected && SelectedSource != null && SelectedSource.Current != true) {
					#region Refresh the list of tasks for the current source.
					Int64 SourceID = SelectedSource.ID; // Take a copy in case it changes while we're awaiting a response.
					switch (MakeRequest(String.Format("{0}?k={1}&s={2}", URL_QUERY, SessionKey, SourceID), ref ReplyFields)) {
					case "OK": // OK|180 (No Change) or OK|180| (No Summary/Alerted Tasks) or OK|180|<TasksSummary>|<Alert1>|<Alert2>|...<AlertN> or OK|0| (Source not found, presumed removed).
						UpdateDispatcher.Invoke((Action)delegate {
							if (ReplyFields.Length > 1) { // Response includes task/alert data.
								Int32 Index = 1; // Maintains a consistent task order (within Status grouping).
								foreach (Task Task in Tasks.Where(x => x.Source == SourceID)) Task.Status = Status.Dormant; // Mark existing tasks with a zero Status (so we can delete any that aren't updated).
								foreach (String Task in ReplyFields[1].Split(new[] { '\r' }, StringSplitOptions.RemoveEmptyEntries)) { // ReplyFields[1] is a summary of tasks prefixed by '\r'. Will be empty if the heartbeat does not transmit a Task Summary.
									if ((Matched = ParseTask.Matches(Task)).Count == 1 && (Matches = Matched[0].Groups).Count == ParsedTask.Count) { // Parse (and confirm match count).
										Int64 TaskID = Int64.Parse(Matches[ParsedTask.ID].ToString());
										Task Existing = Tasks.SingleOrDefault(x => x.Source == SourceID && x.ID == TaskID);
										if (Existing == null) {
											Tasks.Add(new Task() { // Add a new task.
												ID = TaskID,
												Order = Index++,
												Source = SourceID,
												Type = (TaskType)Int32.Parse(Matches[ParsedTask.Type].ToString()),
												Status = Status.Success,
												Pending = Status.Success,
												Alerted = null,
												Description = Matches[ParsedTask.Description].ToString(),
												Detail = null
											});
										} else { // Reset existing task status fields, if it's still alerted it'll be revised again below.
											Existing.Order = Index++;
											Existing.Status = Status.Success;
											Existing.Pending = Status.Success;
											Existing.Alerted = null;
											Existing.Description = Matches[ParsedTask.Description].ToString();
											Existing.Detail = null;
										}
									} else {} // Parse failure - should probably error.
								}
								for (int i = 2; i < ReplyFields.Length; i++) { // ReplyFields[2] onward are active alerts.
									if ((Matched = ParseAlert.Matches(ReplyFields[i])).Count == 1 && (Matches = Matched[0].Groups).Count == ParsedAlert.Count) { // Parse (and confirm match count).
										Int64 TaskID = Int64.Parse(Matches[ParsedAlert.ID].ToString());
										Status TaskStatus = (Status)Int32.Parse(Matches[ParsedAlert.Status].ToString());
										Task Existing = Tasks.SingleOrDefault(x => x.Source == SourceID && x.ID == TaskID);
										if (Existing == null) { // Add a new alerted task.
											Tasks.Add(new Task() {
												ID = TaskID,
												Order = Index++,
												Source = SourceID,
												Type = TaskType.Undefined,
												Status = TaskStatus,
												Pending = (Status)Int32.Parse(Matches[ParsedAlert.Pending].ToString()),
												Alerted = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Double.Parse(Matches[ParsedAlert.Alerted].ToString())).ToLocalTime(), // Unix-style 'epoch' timestamp.
												Description = Matches[ParsedAlert.Description].ToString(),
												Detail = Matches[ParsedAlert.Detail].ToString()
											});
										} else { // Amend existing task status fields.
											Existing.Status = TaskStatus;
											Existing.Pending = (Status)Int32.Parse(Matches[ParsedAlert.Pending].ToString());
											Existing.Alerted = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Double.Parse(Matches[ParsedAlert.Alerted].ToString())).ToLocalTime(); // Unix-style 'epoch' timestamp.
											Existing.Description = Matches[ParsedAlert.Description].ToString();
											Existing.Detail = Matches[ParsedAlert.Detail].ToString();
										}
									} else {} // Parse failure - should probably error.
								}
								for (int i = Tasks.Count - 1; i >= 0; i--) { // Tasks not updated (with a non-zero Status) no longer exist...
									if (Tasks[i].Status == Status.Dormant) Tasks.Remove(Tasks[i]);
								}
								TasksView.Refresh();
								Source Source = Sources.SingleOrDefault(x => x.ID == SourceID);
								if (Source != null) {
									Source.Status = (Status)(Tasks.Where(y => y.Source == SourceID).Max(z => (Int32?)z.Status) ?? 1); // Revise the updated source status from the status of its tasks.
									Source.Current = true;
								}
								SourcesView.Refresh();
								Status = (Status)(Sources.Max(x => (Int32?)x.Status) ?? 0); // Revise the overall monitor status from the status of all sources.
								if (UpdateTasks != null) UpdateTasks.Invoke(this, new UpdateTasksEventArgs(false));
							}
						});
						break;
					case "NA": // NA|180|Not available. /* We've somehow failed to observe the polling strictures, if we want to recover we need to observe the new list refresh pause. */
						UpdateDispatcher.Invoke((Action)delegate {
							Refresh = DateTime.Now.AddSeconds(Double.Parse(ReplyFields[1])); // ReplyFields[1] tells us in how many more seconds we can refresh the source list.
						});
						break;
					case "NO": // NO|Malformed request. // NO|Session not found. // DEVELOPMENT SESSION ONLY: NO|Invalid source.
					case "KO": // KO|Service error.
					default:
						ConnectionState = State.Errored;
						break;
					}
					#endregion
				}
				if (ConnectionState == State.Connected && SelectedSource != null && ClearAlertID.HasValue) {
					#region Clear the specified alert.
					Int64 SourceID = SelectedSource.ID; // Take a copy in case it changes while we're awaiting a response.
					Int64 AlertID = ClearAlertID.Value; // Ditto.
					switch (MakeRequest(String.Format("{0}?k={1}&s={2}&c={3}", URL_QUERY, SessionKey, SourceID, AlertID), ref ReplyFields)) {
					case "OK": // OK|1 or OK|2|\r- Local drive C:\ has less than 5% free space (3.57GB available).
						UpdateDispatcher.Invoke((Action)delegate {
							Task Cleared = Tasks.SingleOrDefault(x => x.Source == SourceID && x.ID == AlertID); // Revise our copy of the task.
							if (Cleared != null) {
								if ((Status)Int32.Parse(ReplyFields[1]) < Status.Caution && Cleared.Type == TaskType.Undefined) { // Task is no longer alerted and was not defined other than as an alert (i.e. there is no Task Summary or the task was deleted since the alert was posted) so we remove it.
									Tasks.Remove(Cleared);
								} else {
									Cleared.Status = (Status)Int32.Parse(ReplyFields[1]); // ReplyFields[1] contains the new Status.
									Cleared.Pending = Cleared.Status; // After a 'Clear' there can be nothing else pending.
									Cleared.Alerted = (Cleared.Status > Status.Success ? DateTime.Now : (DateTime?)null); // After a 'Clear' a task may still be alerted, if so, record the time we cleared (downgraded) it.
									Cleared.Detail = (ReplyFields.Length > 2 ? Regex.Replace(ReplyFields[2], "^\\r", String.Empty) : null); // ReplyFields[2] contains new alert Detail (typically, but not always, empty), strip any leading CR.
								}
							}
							TasksView.Refresh();
							Source Source = Sources.SingleOrDefault(x => x.ID == SourceID);
							if (Source != null) {
								Source.Status = (Status)(Tasks.Where(y => y.Source == SourceID).Max(z => (Int32?)z.Status) ?? 1); // Revise the updated source status from the status of its tasks.
							}
							SourcesView.Refresh();
							Status = (Status)(Sources.Max(x => (Int32?)x.Status) ?? 0); // Revise the overall monitor status from the status of all sources.
							if (UpdateTasks != null) UpdateTasks.Invoke(this, new UpdateTasksEventArgs(true));
						});
						break;
					case "NA": // NA|Not enabled for this account. /* The ability to clear alerts has been disabled by the account holder. */
						break;
					case "NO": // NO|Malformed request. // NO|Session not found. // DEVELOPMENT SESSION ONLY: NO|Invalid task.
					case "KO": // KO|Service error.
					default:
						ConnectionState = State.Errored;
						break;
					}
					_ClearAlertID = null;
					#endregion
				}
				if (ConnectionState == State.Connected && DoWebLogin) {
					#region Open a pre-authorized website session.
					switch (MakeRequest(String.Format("{0}?k={1}&w", URL_QUERY, SessionKey), ref ReplyFields)) {
					case "OK": // OK|<URL>
						System.Diagnostics.Process.Start(ReplyFields[1]); // ReplyFields[1] contains a link to a pre-authorized login, valid for 60 seconds.
						break;
					case "NA": // NA|Not enabled for this account. /* The ability to login via the API has not been enabled by the account holder. */
						break;
					case "NO": // NO|Invalid session key.
					case "KO": // KO|Service error.
					default:
						ConnectionState = State.Errored;
						break;
					}
					_DoWebLogin = false;
					#endregion
				}
				if (ConnectionState >= State.Started) {
					#region Wait for the next thing to do.
					if (ConnectionState != State.Connected) {
						WaitPeriod = Refresh - DateTime.Now; // Wait for a session.
					} else if (Paused) {
						WaitPeriod = (LastRequest + SessionLife - TimeSpan.FromSeconds(5)) - DateTime.Now; // Keep the session alive.
					} else if (SelectedSource == null || SelectedSource.Current) {
						WaitPeriod = Refresh - DateTime.Now; // Wait to update source list.
					} else {
						WaitPeriod = TimeSpan.Zero; // Don't wait.
					}
					if (WaitPeriod > TimeSpan.Zero) {
						if (WaitPeriod < TimeSpan.FromMilliseconds(1)) WaitPeriod = TimeSpan.FromMilliseconds(1);
						if (Echo != null) UpdateDispatcher.BeginInvoke(Echo, this, new EchoEventArgs(String.Format("Waiting {0}...", WaitPeriod)));
						UpdateWait.WaitOne(WaitPeriod);
					}
					#endregion
				}
			}

			if (SessionKey != null) MakeRequest(String.Format("{0}?x={1}", URL_QUERY, SessionKey), ref ReplyFields); // Relinquish session.

		}

		private void MonitorDone(object sender, RunWorkerCompletedEventArgs e) {

			Paused = false;
			DoWebLogin = false;
			ClearAlertID = null;
			SelectedSource = null;

			Tasks.Clear(); TasksView.Refresh();
			Sources.Clear(); SourcesView.Refresh();

			Status = Status.Dormant;
			Refresh = DateTime.MinValue;
			Account = null;
			SessionAccess = 0;
			SessionLife = TimeSpan.Zero;
			SessionKey = null;
			ServiceVersion = null;

			if (Stopped != null) Stopped.Invoke(this, new EventArgs());

			UpdateExit.Set();

		}

		private string MakeRequest(string Query, ref string[] ReplyFields) {

			HttpWebRequest Request = (HttpWebRequest)System.Net.WebRequest.Create(Query);
			String Buffer;

			Request.CachePolicy = CachePolicy;

			LastRequest = DateTime.Now;

			if (Echo != null) UpdateDispatcher.BeginInvoke(Echo, this, new EchoEventArgs(Query));

			try {
				using (HttpWebResponse Response = (HttpWebResponse)Request.GetResponse())
				using (StreamReader Reader = new StreamReader(Response.GetResponseStream())) {
					ReplyFields = (Buffer = Reader.ReadToEnd()).Split('\n');
					if (Echo != null) UpdateDispatcher.BeginInvoke(Echo, this, new EchoEventArgs(Buffer));
				}
			} catch (Exception x) {
				ReplyFields = null;
				if (Echo != null) UpdateDispatcher.BeginInvoke(Echo, this, new EchoEventArgs(String.Format("ERROR: {0}", x.Message)));
			}

			if (ReplyFields == null || ReplyFields.Length == 0) {
				return null;
			} else {
				return ReplyFields[0];
			}

		}

		/* EVENT PROCEDURES ================================================ */

	}

}
