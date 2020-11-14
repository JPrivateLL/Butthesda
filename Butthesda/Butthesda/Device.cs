using Buttplug.Client;
using Buttplug.Core.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace Butthesda
{
	public delegate void Notify();

	public class Device
	{
		private readonly TimeSpan IntermediateUpdateInterval = new TimeSpan(0, 0, 0, 0, 100); //100 milliseconds

		public static List<Device> devices = new List<Device>();

		private static readonly SemaphoreSlim _clientLock = new SemaphoreSlim(1);

		public readonly String name;
		public readonly ButtplugClientDevice device;
		public readonly ButtplugClient client;
		public bool active;



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
				this.memory_Scanner.AnimationTimeResetted -= On_Animation_Timer_Reset;
			memory_Scanner.AnimationTimeResetted += On_Animation_Timer_Reset;
			this.memory_Scanner = memory_Scanner;
		}


		private void On_Animation_Timer_Reset(object sender, EventArgs e)
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


		public event Notify EventAdded;
		public event Notify EventRemoved;
		public event Notify EventsCleared;

		public Running_Event AddEvent(string name, List<FunScriptAction> actions, bool synced_by_animation)
		{
			Running_Event new_event = new Running_Event(name, actions, synced_by_animation);
			EventAdded?.Invoke();
			lock (running_events)
			{
				running_events.Add(new_event);
			}
			ForceUpdate();
			return new_event;
		}

		public static void Stop_All()
		{

			foreach (Device device in devices)
			{
				lock (device.running_events)
				{
					device.EventsCleared?.Invoke();
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

			while (true)
			{
				Thread.Sleep(10);
				//if (!gameRunning) continue;
				//check if there are running events
				bool has_events = false;
				lock (running_events)
				{
					has_events = running_events.Count != 0;
				}


				if (!has_events)
				{
					await Set(0);
					await Set(0, 300);
					continue;
				}


				DateTime timeNow = DateTime.Now;
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
								EventRemoved?.Invoke();
							}
						}

						//find earliest time at wich point we need to do a update again
						foreach (Running_Event running_event in running_events)
						{
							if (nextUpdate < running_event.nextTime)
							{
								prevUpdate = nextUpdate;
								nextUpdate = running_event.nextTime;
							}
						}

						//calculate what position of all events at next point (update)
						foreach (Running_Event running_event in running_events)
						{
							positions.Add(running_event.GetPosition(nextUpdate) / 99.0d);
						}
					}

					if (positions.Count == 0)
					{
						//No events are plaing on this device so we can set it back to position 0
						await Set(0, 1000);
						continue;
					}


					//avarage the positions to get final position
					double position = 0;
					foreach (double p in positions)
					{
						position += p;
					}
					position /= positions.Count;



					old_position = new_position;
					new_position = position;
					//calculate duration to next point
					uint duration = (uint)(nextUpdate - timeNow).TotalMilliseconds;

					await Set(new_position, duration);

					//we need to set it here so it gets the first time
					nextIntermediateUpdate = timeNow;
				}

				if (timeNow >= nextIntermediateUpdate)
				{
					nextIntermediateUpdate = timeNow + IntermediateUpdateInterval;

					double position = ((double)(timeNow - prevUpdate).TotalMilliseconds / (double)(nextUpdate - prevUpdate).TotalMilliseconds * (new_position - old_position)) + old_position;
					position = Math.Max(Math.Min(position, 1d), 0d);
					Console.WriteLine(String.Format("new:{0}, old:{1}, pos:{2}", new_position, old_position, old_position));
					await Set(position);
				}
			}
		}




		private void ForceUpdate()
		{
			//set time to zero so the update loop is force to do a update
			prevUpdate = new DateTime(0);
			nextUpdate = new DateTime(0);
		}


		private double currentPos = 0;
		public async Task Set(double position, uint duration)
		{
			if (client == null) return;

			try
			{
				await _clientLock.WaitAsync();

				double deltaPos = Math.Abs(position - currentPos);
				if (deltaPos != 0)
				{
					currentPos = position;

					if (device.AllowedMessages.ContainsKey(typeof(LinearCmd)))
					{
						await device.SendLinearCmd(duration, position);
					}

					//ButtplugMessage response = await _client.SendDeviceMessage(device, message);
					//await CheckResponse(response);
				}
			}
			finally
			{
				_clientLock.Release();
			}
		}


		public async Task Set(double position)
		{

			if (client == null) return;
			try
			{
				await _clientLock.WaitAsync();

				if (device.AllowedMessages.ContainsKey(typeof(VibrateCmd)))
				{
					await device.SendVibrateCmd(position);
				} else if (device.AllowedMessages.ContainsKey(typeof(RotateCmd)))
				{
					await device.SendRotateCmd(StrengthToRotation(position), random(2) == 0);
				}

			}
			finally
			{
				_clientLock.Release();
			}
		}

		private static UInt32 StrengthToRotation(double strength)
		{
			return Convert.ToUInt32(Math.Pow(strength, 2) * 80);
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

		public Running_Event() : this("", new List<FunScriptAction>(), false) { }

		public Running_Event(string name, List<FunScriptAction> actions, bool synced_by_animation)
		{
			this.name = name;
			this.synced_by_animation = synced_by_animation;
			this.actions = actions;
			this.ended = false;
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
				if (!synced_by_animation)//uf its synced we dont remove it because the animation might run again.
				{
					ended = true;
				}
				return;
			}

			FunScriptAction action = actions[current_step];
			current_step++;

			prevPosition = nextPosition;
			prevTime = nextTime;

			nextPosition = action.Position;
			nextTime = timeStarted + action.TimeStamp;

			while (nextTime < date)
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
