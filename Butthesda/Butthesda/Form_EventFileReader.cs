using Gma.System.MouseKeyHook;
using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Butthesda
{
	public partial class Form_EventFileReader : Form
	{
		Thread Thread_eventFileScanner;
		public bool RequestRestart = false;
		private readonly Memory_Scanner memory_scanner;
		private readonly VibrationEvents vibrationEvents;
		private readonly EventFileScanner eventFileScanner;
		private readonly string Game_Path;
		private readonly IKeyboardMouseEvents m_GlobalHook;
		private readonly Recorder _rec1;
		private readonly Recorder _rec2;

		public Form_EventFileReader(string Game_Name, string Game_Path)
		{
			this.Game_Path = Game_Path;

			InitializeComponent();

			//load presets
			checkBox_ShowWarnings.Checked = Properties.Settings.Default.Show_warnings;
			checkBox_ShowErrors.Checked = Properties.Settings.Default.Show_errors;
			checkBox_ShowNotifications.Checked = Properties.Settings.Default.Show_notifications;
			checkBox_ShowDebug.Checked = Properties.Settings.Default.Show_debug_info;

			vibrationEvents = new VibrationEvents(Game_Path);
			memory_scanner = new Memory_Scanner(Game_Name);
			eventFileScanner = new EventFileScanner(Game_Path, memory_scanner, vibrationEvents);


			RecorderOptions options = new RecorderOptions
			{
				RecorderMode = RecorderMode.Video,
				RecorderApi = RecorderApi.WindowsGraphicsCapture,
				IsThrottlingDisabled = false,
				IsHardwareEncodingEnabled = true,
				IsLowLatencyEnabled = false,
				IsMp4FastStartEnabled = false,
				IsFragmentedMp4Enabled = false,
				IsLogEnabled = true,
				LogSeverityLevel = LogLevel.Debug,
				LogFilePath = @"C:\Users\jpriv\Desktop\Nieuwe map\",

				AudioOptions = new AudioOptions
				{
					IsAudioEnabled = false,
				},
				VideoOptions = new VideoOptions
				{
					BitrateMode = BitrateControlMode.Quality,
					Bitrate = 7000 * 1000,
					Framerate = 60,
					Quality = 70,
					IsFixedFramerate = true,
					EncoderProfile = H264Profile.Main
				}
			};

			_rec1 = Recorder.CreateRecorder(options);
			_rec2 = Recorder.CreateRecorder(options);


			m_GlobalHook = Hook.GlobalEvents();
			m_GlobalHook.OnCombination(new Dictionary<Combination, Action>
			{
				{Combination.TriggeredBy(Keys.PageUp), WaitForRecording},
			});
			m_GlobalHook.OnCombination(new Dictionary<Combination, Action>
			{
				{Combination.TriggeredBy(Keys.Home), WaitForContinuesRecording},
			});
			m_GlobalHook.OnCombination(new Dictionary<Combination, Action>
			{
				{Combination.TriggeredBy(Keys.End),StopRecording},
			});

		}
		private string record_to = @"C:\Users\jpriv\Desktop\Nieuwe map\";
		private string running_sexlab_animation_text;
		private bool _waitForRecording = false;
		private bool _recording = false;

		void WaitForRecording(object sender, EventArgs e)
		{
			if (_recording)
			{
				memory_scanner.AnimationTimeResetted -= WaitForRecording;
				_waitForRecording = false;
				_recording = false;
				_rec1.Stop();
				WriteTo_EventViewer("done recording");

			}
			else
			{
				WriteTo_EventViewer("recording");
				_recording = true;
				for (int i = 0; true; i++)
				{
					string file = String.Format("{0}{1} ({2}).mp4", record_to, running_sexlab_animation_text, i);
					if (!File.Exists(file))
					{
						_rec1.Record(file);
						break;
					}

				}


			}
		}
		void WaitForRecording()
		{
			if (_waitForRecording) return;
			_waitForRecording = true;

			WriteTo_EventViewer("Waiting for recording");


			memory_scanner.AnimationTimeResetted += WaitForRecording;

		}

		bool _recSwitch = false;
		DateTime Recording_Since = DateTime.Now;
		void WaitForContinuesRecording(object sender, EventArgs e)
		{
			if (DateTime.Now < Recording_Since + new TimeSpan(0, 0, 0, 0, 500)) return;

			Recorder r1;
			Recorder r2;
			if (_recSwitch)
			{
				r1 = _rec1;
				r2 = _rec2;

			}
			else
			{
				r1 = _rec2;
				r2 = _rec1;
			}
			_recSwitch = !_recSwitch;
			
			r1.Stop();

			_recording = true;
			
			WriteTo_EventViewer("recording new");
			for (int i = 0; true; i++)
			{
				string file = String.Format("{0}{1} ({2}{3}).mp4", record_to, running_sexlab_animation_text, i, _recSwitch);
				if (!File.Exists(file))
				{
					r2.Record(file);
					Recording_Since = DateTime.Now;
					break;
				}
			}
		}

		void WaitForContinuesRecording()
		{
			if (_waitForRecording) return;
			_waitForRecording = true;

			WriteTo_EventViewer("Waiting for continues recording");

			memory_scanner.AnimationTimeResetted += WaitForContinuesRecording;
		}

		void StopRecording()
		{
			memory_scanner.AnimationTimeResetted -= WaitForRecording;
			memory_scanner.AnimationTimeResetted -= WaitForContinuesRecording;
			_rec1.Stop();
			_rec2.Stop();
			_waitForRecording = false;
			_recording = false;
		}

		public void Init()
		{
			vibrationEvents.Notification_Message += Notivication_Message;
			vibrationEvents.Warning_Message += Warning_Message;
			vibrationEvents.Error_Message += Error_Message;
			vibrationEvents.Debug_Message += Debug_Message;
			vibrationEvents.SexLab_Animation_Changed += Sexlab_Animation_Changed;

			memory_scanner.Notification_Message += Notivication_Message;
			memory_scanner.Warning_Message += Warning_Message;
			memory_scanner.Error_Message += Error_Message;
			memory_scanner.Debug_Message += Debug_Message;
			memory_scanner.AnimationEvent += Memory_Scanner_AnimationEvent;
			memory_scanner.AnimationTimeResetted += Memory_Scanner_AnimationTimeResetted;
			memory_scanner.AnimationTimeUpdated += Memory_Scanner_AnimationTimeUpdated;
			memory_scanner.GamePaused += Memory_Scanner_GamePaused;
			memory_scanner.GameResumed += Memory_Scanner_GameResumed;
			memory_scanner.GameOpened += Memory_Scanner_GameOpened;
			memory_scanner.GameClosed += Memory_Scanner_GameClosed;


			eventFileScanner.Notification_Message += Notivication_Message;
			eventFileScanner.Error_Message += Error_Message;
			eventFileScanner.Debug_Message += Debug_Message;
			eventFileScanner.Warning_Message += Warning_Message;
			eventFileScanner.Save_Loaded += EventFileScanner_SaveLoaded;
			eventFileScanner.Arousal_Updated += EventFileScanner_ArousalUpdated;
			eventFileScanner.DD_Device_Update += EventFileScanner_DD_DeviceUpdate;




			foreach (Device d in Device.devices)
			{
				d.EventListUpdated += EventList_Device_Updated;


				d.Notification_Message += Notivication_Message;
				d.Error_Message += Error_Message;
				d.Debug_Message += Debug_Message;
				d.Warning_Message += Warning_Message;

				d.SetMemoryEvents(memory_scanner);
			}


			Thread_eventFileScanner = new Thread(new ThreadStart(eventFileScanner.Run))
			{
				IsBackground = true,
				Name = "eventFileScanner"
			};
			Thread_eventFileScanner.Start();


			//We need to start it after the constructor is done, because we need the event handlers setup first
			new Thread(() =>
			{
				Thread.CurrentThread.IsBackground = true;

				vibrationEvents.Init();
				bool success = memory_scanner.Init();

				Thread.Sleep(5000);//we dont want to restart to fast after eachother
				if (!success)
				{
					Warning_Message(this, new StringArg("Program will try to inject in 5s"));
					Request_Restart();
				}


			}).Start();



		}

		private void Sexlab_Animation_Changed(object sender, StringArg e)
		{
			running_sexlab_animation_text = e.String;
		}

		private void Request_Restart()
		{
			Thread_eventFileScanner.Abort();
			memory_scanner.Close();

			RequestRestart = true;
			Invoke((MethodInvoker)(() => this.Close()));
		}



		private void Memory_Scanner_GameClosed(object sender, EventArgs e)
		{
			File.WriteAllText(Game_Path + @"\FunScripts\link.txt", string.Empty);
			Invoke((MethodInvoker)(() => game_running = false));
			Update_Game_State();
		}


		private void Memory_Scanner_GameOpened(object sender, EventArgs e)
		{
			Request_Restart();
		}


		private void Notivication_Message(object sender, EventArgs e)
		{
			if (!checkBox_ShowNotifications.Checked) return;
			if (sender.GetType().Name == "Memory_Scanner" && !checkBox_MemoryScanner.Checked) return;
			if (sender.GetType().Name == "EventFileScanner" && !checkBox_EventFileScanner.Checked) return;
			if (sender.GetType().Name == "Device" && !checkBox_DebugDevices.Checked) return;
			StringArg e2 = (StringArg)e;
			WriteTo_EventViewer(sender.GetType().Name + " - " + e2.String, Color.Black);
		}


		private void Error_Message(object sender, EventArgs e)
		{
			if (!checkBox_ShowErrors.Checked) return;
			if (sender.GetType().Name == "Memory_Scanner" && !checkBox_MemoryScanner.Checked) return;
			if (sender.GetType().Name == "EventFileScanner" && !checkBox_EventFileScanner.Checked) return;
			if (sender.GetType().Name == "Device" && !checkBox_DebugDevices.Checked) return;
			StringArg e2 = (StringArg)e;
			WriteTo_EventViewer(sender.GetType().Name + " - " + e2.String, Color.Red);
		}


		private void Debug_Message(object sender, EventArgs e)
		{
			if (!checkBox_ShowDebug.Checked) return;
			if (sender.GetType().Name == "Memory_Scanner" && !checkBox_MemoryScanner.Checked) return;
			if (sender.GetType().Name == "EventFileScanner" && !checkBox_EventFileScanner.Checked) return;
			if (sender.GetType().Name == "Device" && !checkBox_DebugDevices.Checked) return;
			StringArg e2 = (StringArg)e;
			WriteTo_EventViewer(sender.GetType().Name + " - " + e2.String, Color.Gray);
		}


		private void Warning_Message(object sender, EventArgs e)
		{
			if (!checkBox_ShowWarnings.Checked) return;
			if (sender.GetType().Name == "Memory_Scanner" && !checkBox_MemoryScanner.Checked) return;
			if (sender.GetType().Name == "EventFileScanner" && !checkBox_EventFileScanner.Checked) return;
			if (sender.GetType().Name == "Device" && !checkBox_DebugDevices.Checked) return;
			StringArg e2 = (StringArg)e;
			WriteTo_EventViewer(sender.GetType().Name + " - " + e2.String, Color.Orange);
		}



		private void EventFileScanner_DD_DeviceUpdate(object sender, EventArgs e)
		{
			StringArgs e2 = (StringArgs)e;
			Change_Devices(e2.StringArray);
		}

		private void EventFileScanner_ArousalUpdated(object sender, EventArgs e)
		{
			IntArg e2 = (IntArg)e;
			Update_Arousal(e2.Int);

		}
		private void EventFileScanner_SaveLoaded(object sender, EventArgs e)
		{
			StringArg e2 = (StringArg)e;

			Update_Arousal(0);
			Clear_EventViewer();

			Invoke((MethodInvoker)(() => text_detected_mods.Text = e2.String));
		}

		private void Memory_Scanner_GamePaused(object sender, EventArgs e)
		{
			GamePaused(true);
		}
		private void Memory_Scanner_GameResumed(object sender, EventArgs e)
		{
			GamePaused(false);
		}

		private void Memory_Scanner_AnimationTimeUpdated(object sender, EventArgs e)
		{
			FloatArg e2 = (FloatArg)e;

			Invoke((MethodInvoker)(() =>
			{
				label_animation_time.Text = e2.Float.ToString("0.00");
			}));
		}

		private void Memory_Scanner_AnimationTimeResetted(object sender, EventArgs e)
		{
			//WriteTo_EventViewer("- Animation Timer Update!");
		}



		private void Memory_Scanner_AnimationEvent(object sender, EventArgs e)
		{
			StringArg e2 = (StringArg)e;
			//WriteTo_EventViewer("Animation: "+e2.String);
		}


		private void EventList_Device_Updated(object sender, EventArgs e)
		{
			if (!this.IsHandleCreated) return;

			int count = 0;
			foreach(Device d in Device.devices)
			{
				count += d.Running_Event_Count();
			}

			Invoke((MethodInvoker)(() =>
			{
				label_running_events.Text = "Running events: " + count;
			}));

		}


		internal void WriteTo_EventViewer(string text, Color color)
		{
			if (!CheckBox_Debugger.Checked) return;
			if (!this.IsHandleCreated) return;
			Invoke((MethodInvoker)(() =>
			{
				int start = event_viewer.Text.Length;
				event_viewer.AppendText(text + "\n");
				int finished = event_viewer.Text.Length;

				event_viewer.Select(start, finished); //Select text within 0 and 8
				event_viewer.SelectionColor = color;
			}));
		}

		internal void WriteTo_EventViewer(string text)
		{

			WriteTo_EventViewer(text, Color.Black);
		}

		internal void Clear_EventViewer()
		{
			if (!this.IsHandleCreated) return;
			Invoke((MethodInvoker)(() => event_viewer.Clear()));
		}


		internal void Update_Arousal(int arousal)
		{
			if (arousal > 100) { arousal = 100; }
			if (arousal < 0) { arousal = 0; }
			Invoke((MethodInvoker)(() => progressBar_arousal.Value = arousal));
		}

		internal void Change_Devices(string[] devices)
		{
			Invoke((MethodInvoker)(() =>
			{
				label_vaginal.Text = devices[0];
				label_anal.Text = devices[1];
				label_vaginal_piercing.Text = devices[2];
				label_nipple_piercing.Text = devices[3];

			}));
		}


		bool game_running = true;
		bool game_paused = false;


		internal void GamePaused(bool paused)
		{
			Invoke((MethodInvoker)(() => game_paused = paused));
			Update_Game_State();
		}

		internal void Update_Running_Events(List<Running_Event> list)
		{

			Invoke((MethodInvoker)(() => label_running_events.Text = "running events: " + list.Count));

		}


		private void Update_Game_State()
		{
			string text = label_game_state.Text;
			if (game_running)
			{
				if (game_paused)
				{
					text = "Paused";
				}
				else
				{
					text = "Running";
				}
			}
			else
			{
				text = "Not running";
			}

			Invoke((MethodInvoker)(() => label_game_state.Text = text));
		}

		private void Form_EventFileReader_FormClosing(object sender, FormClosingEventArgs e)
		{
			m_GlobalHook.Dispose();

			memory_scanner.Close();
			Thread_eventFileScanner.Abort();
			Running_Event.Stop_All();
		}



		private void Event_viewer_TextChanged(object sender, EventArgs e)
		{
			if (!CheckBox_Debugger.Checked) return;
			event_viewer.SelectionStart = event_viewer.Text.Length;
			// scroll it automatically
			event_viewer.ScrollToCaret();
		}







		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{

		}

		private void CheckBox_ShowDebug_CheckedChanged(object sender, EventArgs e)
		{
			Properties.Settings.Default.Show_debug_info = checkBox_ShowDebug.Checked;
			Properties.Settings.Default.Save();
		}

		private void CheckBox_ShowNotification_CheckedChanged(object sender, EventArgs e)
		{
			Properties.Settings.Default.Show_notifications = checkBox_ShowNotifications.Checked;
			Properties.Settings.Default.Save();
		}

		private void CheckBox_ShowErrors_CheckedChanged(object sender, EventArgs e)
		{
			Properties.Settings.Default.Show_errors = checkBox_ShowErrors.Checked;
			Properties.Settings.Default.Save();
		}

		private void CheckBox_ShowWarnings_CheckedChanged(object sender, EventArgs e)
		{
			Properties.Settings.Default.Show_warnings = checkBox_ShowWarnings.Checked;
			Properties.Settings.Default.Save();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			vibrationEvents.Init();
		}
	}
}
