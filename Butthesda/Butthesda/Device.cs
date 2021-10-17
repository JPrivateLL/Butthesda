using Buttplug;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Butthesda
{
	public delegate void Notify();


	public class Device
	{
		public event EventHandler Notification_Message;
		public event EventHandler Warning_Message;
		public event EventHandler Error_Message;
		public event EventHandler Debug_Message;
		public event EventHandler EventListUpdated;

		private readonly TimeSpan IntermediateUpdateInterval = new TimeSpan(0, 0, 0, 0, 10); //10 milliseconds

		public static List<Device> devices = new List<Device>();

		private static readonly SemaphoreSlim _clientLock = new SemaphoreSlim(1);

		public readonly String name;
		public readonly ButtplugClientDevice device;
		public readonly ButtplugClient client;
		public bool active;

		public double MinPosition = 0d;
		public double MaxPosition = 1d;

		private readonly Thread thread;

		public enum BodyPart
		{
			Head = 0,
			Body,
			Breast,
			Belly,
			Feet,
			Mouth,
			Vaginal,
			Clit,
			Anal
		}

		public enum EventType
		{
			Shock = 0,
			Damage,
			Penetrate,
			Vibrate,
			Equip
		}

		private readonly List<Running_Event> running_events;
		public int Running_Event_Count() { return running_events.Count; }

		public Device(String name, ButtplugClient client, ButtplugClientDevice device)
		{
			this.name = name;
			this.device = device;
			this.client = client;
			this.active = false;
			this.running_events = new List<Running_Event>();

			thread = new Thread(UpdateLoop)
			{
				IsBackground = true
			};
			thread.Start();


		}

		Memory_Scanner memory_Scanner;
		public void SetMemoryEvents(Memory_Scanner memory_Scanner)
		{
			if (this.memory_Scanner != null)
			{
				this.memory_Scanner.AnimationTimeResetted -= On_Animation_Timer_Reset;
				this.memory_Scanner.GamePaused -= On_Game_Paused;
				this.memory_Scanner.GameResumed -= On_Game_Resumed;
			}
			memory_Scanner.GamePaused += On_Game_Paused;
			memory_Scanner.GameResumed += On_Game_Resumed;
			memory_Scanner.AnimationTimeResetted += On_Animation_Timer_Reset;
			this.memory_Scanner = memory_Scanner;
		}

		bool Game_Running = true;

		private void On_Game_Paused(object sender, EventArgs e)
		{
			Game_Running = false;
		}

		private void On_Game_Resumed(object sender, EventArgs e)
		{
			Game_Running = true;
			ForceUpdate();
		}

		private void On_Animation_Timer_Reset(object sender, EventArgs e)
		{
			lock (running_events)
			{
				foreach (Running_Event running_event in running_events)
				{
					if (running_event.synced_by_animation)
					{
						running_event.Reset();
						ForceUpdate();
					}
				}
			}
		}



		public Running_Event AddEvent(string name, List<FunScriptAction> actions, bool synced_by_animation = false, bool repeating = false)
		{
			Running_Event new_event = new Running_Event(name, actions, synced_by_animation, repeating);
			lock (running_events)
			{
				running_events.Add(new_event);
			}
			ForceUpdate();
			EventListUpdated?.Invoke(this, EventArgs.Empty);
			return new_event;
		}

		public static void Stop_All()
		{

			foreach (Device device in devices)
			{
				lock (device.running_events)
				{
					device.EventListUpdated?.Invoke(device, EventArgs.Empty);
					device.running_events.Clear();
					device.ForceUpdate();
				}
			}
		}

		DateTime prevUpdate = new DateTime(0);
		DateTime nextUpdate = new DateTime(0);
		DateTime nextIntermediateUpdate = new DateTime(0);



		private async void UpdateLoop()
		{

			double old_position = 0;
			double new_position = 0;
			double cur_position = 0;
			bool paused = false;
			while (true)
			{
				Thread.Sleep(1);
				//if (!gameRunning) continue;
				//check if there are running events
				bool has_events = false;
				lock (running_events)
				{
					has_events = running_events.Count != 0;
				}



				if (!has_events)
				{
					old_position = 0;
					new_position = 0;
					await Set(0);
					await Set(0, 300);
					continue;
				}


				DateTime timeNow = DateTime.Now;
				if (!Game_Running)
				{
					if (!paused)
					{
						paused = true;
						double position = ((double)(timeNow - prevUpdate).TotalMilliseconds / (double)(nextUpdate - prevUpdate).TotalMilliseconds * (new_position - old_position)) + old_position;
						await Set(position, 100);//pause at current possition
						await Set(0);
					}
					continue;
				}

				if (paused)
				{
					paused = false;
					ForceUpdate();
				}


				if (timeNow >= nextUpdate)
				{

					List<double> positions = new List<double>();

					lock (running_events)
					{
						//update all events and find next update time
						foreach (Running_Event running_event in running_events)
						{
							running_event.Update(timeNow);
						}

						//remove events that are done
						for (int i = running_events.Count - 1; i >= 0; i--)
						{
							if (running_events[i].ended)
							{
								running_events.RemoveAt(i);
								EventListUpdated?.Invoke(this, EventArgs.Empty);
							}
						}

						//find earliest time at wich point we need to do a update again
						foreach (Running_Event running_event in running_events)
						{
							if (nextUpdate < running_event.nextTime)
							{
								prevUpdate = timeNow;
								nextUpdate = running_event.nextTime;
							}
						}

						//Find positions of all events at next update location
						foreach (Running_Event running_event in running_events)
						{
							positions.Add(running_event.GetPosition(nextUpdate) / 99.0d);
						}
					}

					if (positions.Count == 0)
					{
						//No events are plaing on this device so we can set it back to position 0
						old_position = 0;
						new_position = 0;
						await Set(0, 1000);
						continue;
					}


					//avarage the positions to get final position
					double position = 0;
					foreach (double p in positions)
					{
						position += p;
					}

					new_position = Math.Min(position / positions.Count, 99d);
					old_position = cur_position;


					Debug_Message?.Invoke(this, new StringArg(String.Format("Current position:{0}, New position:{1}", cur_position, new_position)));
					//calculate duration to next point
					uint duration = (uint)(nextUpdate - timeNow).TotalMilliseconds;
					try
					{
						await Set(new_position, duration);
					}catch { }

				//We want to update it directly
				nextIntermediateUpdate = timeNow;
				}

				if (timeNow >= nextIntermediateUpdate)
				{
					nextIntermediateUpdate = timeNow + IntermediateUpdateInterval;
					double old_cur_position = cur_position;
					cur_position = ((double)(timeNow - prevUpdate).TotalMilliseconds / (double)(nextUpdate - prevUpdate).TotalMilliseconds * (new_position - old_position)) + old_position;
					if (Double.IsNaN(cur_position)||Double.IsInfinity(cur_position)) cur_position = old_cur_position;

					double test = Math.Abs(new_position - old_position) / (double)(nextUpdate - prevUpdate).TotalMilliseconds * 200;
					Debug_Message?.Invoke(this, new StringArg(String.Format("Intermediate Update Strength: {0} or {1}", cur_position, test)));
					try
					{
						//await Set(test);
						await Set(cur_position);
					}
					catch { }
					
					
				}
			}
		}




		private void ForceUpdate()
		{
			Debug_Message?.Invoke(this, new StringArg("ForceUpdate"));
			//set time to zero so the update loop is force to do a update
			prevUpdate = new DateTime(0);
			nextUpdate = new DateTime(0);
		}


		private double currentPos = 0;
		public async Task Set(double position, uint duration)
		{
			if (client == null) return;
			if (!device.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.LinearCmd)) return;
			
			position = Math.Max(Math.Min(position, 1d), 0d);

			if (currentPos == position) return;
			currentPos = position;

			position = position * (MaxPosition - MinPosition) + MinPosition;
			
			try
			{
				await _clientLock.WaitAsync();
				await device.SendLinearCmd(duration, position);
				//ButtplugMessage response = await _client.SendDeviceMessage(device, message);
				//await CheckResponse(response);
			}
			finally
			{
				_clientLock.Release();
			}
		}


		public async Task Set(double position)
		{

			if (client == null) return;
			

			position = Math.Max(Math.Min(position, 1d), 0d);
			if (currentPos == position) return;
			Debug_Message?.Invoke(this, new StringArg(String.Format("Set Strength: {0}", position)));
			bool direction = currentPos > position;
			currentPos = position;

			if (position != 0)
			{
				position = position * (MaxPosition - MinPosition) + MinPosition;
			}

			try
			{
				await _clientLock.WaitAsync();
				if (device.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.VibrateCmd))
				{
					await device.SendVibrateCmd(position);
				}
				else if (device.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.RotateCmd))
				{
					await device.SendRotateCmd(Math.Pow(position, 2), direction);
				}

					
			}
			finally
			{
				_clientLock.Release();
			}


		}


		public void SetType(BodyPart bodyPart, EventType eventType, bool set)
		{
			this.type[(int)bodyPart, (int)eventType] = set;
		}

		public bool HasType(BodyPart bodyPart, EventType eventType)
		{
			return this.type[(int)bodyPart, (int)eventType];
		}

		private readonly bool[,] type = new bool[Enum.GetNames(typeof(BodyPart)).Length, Enum.GetNames(typeof(EventType)).Length];

		public override string ToString()
		{
			return name;
		}
	}

	public class Running_Event
	{
		public string name;
		public bool repeating;
		private readonly List<FunScriptAction> actions;
		DateTime timeStarted;

		uint nextPosition;
		public DateTime nextTime;

		uint prevPosition;
		DateTime prevTime;

		int current_step;
		public bool ended;
		public readonly bool synced_by_animation;

		public void End()
		{
			ended = true;
		}

		public static Running_Event Empty()
		{
			return new Running_Event("", new List<FunScriptAction>(), false);
		}

		public Running_Event(string name, List<FunScriptAction> actions, bool synced_by_animation, bool repeating = false)
		{
			this.name = name;
			this.synced_by_animation = synced_by_animation;
			this.repeating = repeating;
			this.actions = actions;
			ended = false;
			Reset();
		}


		public void Reset()
		{
			timeStarted = DateTime.Now;
			prevTime = timeStarted;
			nextTime = timeStarted;
			nextPosition = 0;
			prevPosition = 0;
			current_step = 0;
		}


		public void Update(DateTime date)
		{

			//We dont need to update if time didnt pass
			if (date <= nextTime)
			{
				return;
			}

			//no more steps lets mark it for removal
			if (current_step >= actions.Count)
			{
				if (repeating)
				{
					Reset();
				}
				else
				{
					if (!synced_by_animation)//uf its synced we dont remove it because the animation might run again.
					{
						ended = true;
					}
					return;
				}
				

			}

			FunScriptAction action = actions[current_step];
			current_step++;

			prevPosition = nextPosition;
			prevTime = nextTime;

			nextPosition = action.Position;
			nextTime = timeStarted + action.TimeStamp;

			while (nextTime < date && !ended)
			{
				this.Update(date);
			}
		}

		public uint GetPosition(DateTime date)
		{

			if (ended)
			{
				return 0;
			}

			if (date >= nextTime)
			{
				return nextPosition;
			}
			else if (date <= prevTime)
			{
				return prevPosition;
			}
			else
			{
				//map position between old and new position based on current time
				uint position = (uint)((float)(date - prevTime).TotalMilliseconds / (float)(nextTime - prevTime).TotalMilliseconds * (nextPosition - prevPosition)) + prevPosition;
				return Math.Max(Math.Min(position, 99), 0);
			}


		}
	}
}
