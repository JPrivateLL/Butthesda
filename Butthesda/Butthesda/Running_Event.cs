using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Butthesda
{

	public class Running_Event_BodyPart
	{
		public Running_Event parent;
		private readonly List<FunScriptAction> actions;
		public List<Device> devices;
		public bool ended;
		public Device.BodyPart bodyPart;
		DateTime timeStarted;

		uint nextPosition;
		public DateTime nextTime;

		uint prevPosition;
		DateTime prevTime;

		int current_step;

		public Running_Event_BodyPart(List<FunScriptAction> actions, List<Device> devices, Device.BodyPart bodyPart)
		{
			this.actions = actions;
			this.devices = devices;
			this.bodyPart = bodyPart;

			ended = false;
			Reset();

			foreach (Device device in devices)
			{
				device.AddEvent(this);
			}
		}

		public void End()
		{
			ended = true;
			foreach (Device device in devices)
			{
				device.RemoveEvent(this);
			}
			parent.Remove_Child(this);
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

			//no more steps? lets mark it for removal
			if (current_step >= actions.Count)
			{
				if (parent.repeating)
				{
					Reset();
				}
				else
				{
					if (!parent.synced_by_animation)//uf its synced we dont remove it because the animation might run again.
					{
						End();
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

	public class Running_Event
	{
		public string name;
		public bool repeating;
		public readonly bool synced_by_animation;

		public bool ended;
		private List<Running_Event_BodyPart> childs;
		private static List<Running_Event> running_Events = new List<Running_Event>();
		public List<Device> devices;


		public void End()
		{
			//reverse loop because we are removing items from the list
			for (int i = childs.Count - 1; i >= 0; i--)
			{
				childs[i].End();
			}
			running_Events.Remove(this); // this is already done in Remove_Child(). ( lets enable it incase list is emptyremove when list is empty)
		}

		public void Remove_Child(Running_Event_BodyPart child)
		{
			childs.Remove(child);
			if (childs.Count == 0){
				ended = true;
				running_Events.Remove(this);//all child events are done remove parent as well
			}
		}

		public static void Stop_All()
		{
			//reverse loop because we are removing items from the list
			for (int i = running_Events.Count - 1; i >= 0; i--)
			{
				running_Events[i].End();
			}
		}

		public int Running_Event_Count() { return running_Events.Count; }

		public static Running_Event Empty()
		{
			return new Running_Event("", new List<Running_Event_BodyPart>(), false);
		}

		public Running_Event(string name, List<Running_Event_BodyPart> childs, bool synced_by_animation, bool repeating = false)
		{
			this.name = name;
			this.synced_by_animation = synced_by_animation;
			this.repeating = repeating;
			this.childs = childs;
			this.ended = false;

			foreach (Running_Event_BodyPart child in childs)
			{
				child.parent = this;
			}

		}
		
	}
}
