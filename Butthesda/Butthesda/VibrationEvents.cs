using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Butthesda
{



    public class VibrationEvents
    {
        public event EventHandler Notification_Message;
        public event EventHandler Warning_Message;
        public event EventHandler Error_Message;
        public event EventHandler Debug_Message;

        public event EventHandler<StringArg> SexLab_Animation_Changed;

        string Game_Path;
        public VibrationEvents(string Game_Path)
        {
            this.Game_Path = Game_Path;
        }

        public void Init()
		{
            Init_SexLabAnimations();
            Init_Events();
        }



        private List<Data_Actor> events = new List<Data_Actor>();


        private void Init_Events()
        {
            events = new List<Data_Actor>();
            string other_dir = Game_Path + @"\FunScripts";
            string[] mod_dirs = Directory.GetDirectories(other_dir);
            foreach (string mod_dir in mod_dirs)
            {
				if (Path.GetFileName(mod_dir).ToLower() == "sexlab")
				{
                    continue;
				}

                string[] event_dirs = Directory.GetDirectories(mod_dir);
                foreach (string event_dir in event_dirs)
                {
                    string name = Path.GetFileName(event_dir).ToLower();
                    events.Add(new Data_Actor(name, event_dir));
                }
            }

            int funscript_count = 0;
            foreach (Data_Actor p_d in events)
            {
                if (p_d == null) continue;
                foreach (Data_BodyPart b_d in p_d.bodypart_datas)
                {
                    if (b_d == null) continue;
                    foreach (Data_EventType e_d in b_d.eventType_datas)
                    {
                        funscript_count++;
                    }
                }
            }
            Notification_Message?.Invoke(this, new StringArg(String.Format("Registered {0} lose events, with a total of {1} funscripts", events.Count, funscript_count)));
        }




        private List<Data_Animation> SexLab_Animations = new List<Data_Animation>();
        private Data_Actor SexLab_Orgasm_Event = new Data_Actor();
        private void Init_SexLabAnimations()
        {
            SexLab_Animations = new List<Data_Animation>();
            SexLab_Orgasm_Event = new Data_Actor();
            string sexlab_dir = Game_Path + @"\FunScripts\SexLab";
            string[] mod_dirs = Directory.GetDirectories(sexlab_dir);

            foreach (string mod_dir in mod_dirs)
            {
                if (mod_dir.ToLower() == "orgasm")
                {
                    SexLab_Orgasm_Event = new Data_Actor("orgasm", mod_dir);
                    continue;
                }

                string[] animation_dirs = Directory.GetDirectories(mod_dir);
                foreach (string animation_dir in animation_dirs)
                {
                    string name = Path.GetFileName(animation_dir).ToLower();
                    SexLab_Animations.Add(new Data_Animation(name, animation_dir));
                }
            }


            int funscript_count = 0;

            foreach(Data_Animation sl_a in SexLab_Animations)
			{
				foreach (Data_Stage s_d in sl_a.stages)
				{
                    if (s_d == null) continue;
                    foreach (Data_Actor p_d in s_d.positions)
					{
                        if (p_d == null) continue;
                        foreach (Data_BodyPart b_d in p_d.bodypart_datas)
						{
                            if (b_d == null) continue;
							foreach (Data_EventType e_d in b_d.eventType_datas)
							{
                                funscript_count++;
                            }
						}
					}
				}
			}

            Notification_Message?.Invoke(this,new StringArg(String.Format("Registered {0} SexLab animations, with a total of {1} funscripts", SexLab_Animations.Count, funscript_count)));
        }



        /// <summary>adds funscript to device. use bodyparts_specific to only select certain funscripts related to bodypart
        /// </summary>
        private Running_Event PlayEvent(Data_Actor data_event, bool synced_by_animation = false, bool repeating = false, Device.BodyPart bodyparts_specific = 0)
        {

            List<Running_Event_BodyPart> running_Event_BodyParts = new List<Running_Event_BodyPart>();


            foreach (Data_BodyPart data_bodypart in data_event.bodypart_datas)
            {
                if (data_bodypart == null) { continue; }
                if (bodyparts_specific != 0 && !bodyparts_specific.HasFlag(data_bodypart.bodyPart)) { continue; }// only vibrate on given body parts

                Device.BodyPart bodyPart_id = data_bodypart.bodyPart;

                foreach (Data_EventType data_eventType in data_bodypart.eventType_datas)
                {
                    if (data_eventType == null) { continue; };
                    Device.EventType eventType = data_eventType.eventType;
                    List<Device> devices = new List<Device>();
                    foreach (Device device in Device.devices)
					{
                        if (device.HasType(bodyPart_id, eventType)) devices.Add(device);
                    }

                    if (devices.Count == 0) continue;
                    running_Event_BodyParts.Add(new Running_Event_BodyPart(data_eventType.actions, devices, bodyPart_id));
                }
            }

            return new Running_Event(data_event.name, running_Event_BodyParts, synced_by_animation, repeating);
        }

        public Running_Event PlayEvent(string name, bool repeating = false, Device.BodyPart bodyparts_specific = 0)
        {
            name = name.ToLower();
            foreach (Data_Actor event_data in events)
            {
                if (event_data.name == name)
                {
                    Notification_Message?.Invoke(this, new StringArg("Playing event: " + name));
                    return PlayEvent(event_data, repeating: repeating, bodyparts_specific: bodyparts_specific);
                }
            }
            Warning_Message?.Invoke(this, new StringArg("Count not find: " + name));
            return  Running_Event.Empty();
        }


        private Data_Animation Sexlab_Playing_Animation = new Data_Animation();
        private Running_Event sexLab_running_Event = Running_Event.Empty();

		public int Sexlab_Position { get; private set; } = 0;
        public int Sexlab_Stage { get; private set; } = 0;
        public string Sexlab_Name { get; private set; } = "";
        private Running_Event sexLab_running_Event_orgasm = Running_Event.Empty();

        public bool SexLab_StartAnimation(string name, int stage, int position, bool usingStrappon)
        {
            SexLab_StopAnimation();

            name = name.ToLower();
            Sexlab_Name = name;
            foreach (Data_Animation animation in SexLab_Animations)
            {
                if (animation.name == name)
                {
                    

                    Sexlab_Playing_Animation = animation;
                    Sexlab_Stage = stage;
                    Sexlab_Position = position;

                    //lets not do this here because the animation is not started yet
                    //it starts playing when stage updates
                    //UpdateSexLabEvent();

                    return true;
                }
            }
            Warning_Message?.Invoke(this, new StringArg("Can't find SexLab animation: " + name));
            return false;
        }

        public void SexLab_StopAnimation()
        {
            sexLab_running_Event.End();//end old event
            sexLab_running_Event = Running_Event.Empty();
            Sexlab_Playing_Animation = new Data_Animation();
        }

        public void SexLab_SetStage(int stage)
        {
            Sexlab_Stage = stage;
            SexLab_Update_Event();
        }

        public void SexLab_SetPosition(int pos)
        {
            Sexlab_Position = pos;
            SexLab_Update_Event();
        }

        //return the current playing sexlab event
        public void SexLab_Update_Event()
        {
            SexLab_Animation_Changed?.Invoke(this, new StringArg(String.Format("{0} S-{1}, P-{2}", Sexlab_Name, Sexlab_Stage,Sexlab_Position)));
            sexLab_running_Event.End();
            Data_Stage stage_data = Sexlab_Playing_Animation.stages[Sexlab_Stage];
            if (stage_data != null)
            {
                Data_Actor position_data = stage_data.positions[Sexlab_Position];
                sexLab_running_Event = PlayEvent(position_data, synced_by_animation: true);
            }
        }

        public void SexLab_Start_Orgasm()
        {
            sexLab_running_Event_orgasm = PlayEvent(SexLab_Orgasm_Event);

        }

        public void SexLab_Stop_Orgasm()
        {
            sexLab_running_Event_orgasm.End();
            sexLab_running_Event_orgasm = Running_Event.Empty();
        }
    }


    class Data_Animation
    {
        public string name;
        public Data_Stage[] stages = new Data_Stage[50];
        public Data_Animation(string name, string dir)
        {
            this.name = name;
            String[] stage_dirs = Directory.GetDirectories(dir);
            foreach (string stage_dir in stage_dirs)
            {
                int index = Int32.Parse(Path.GetFileName(stage_dir).Substring(1));
                stages[index - 1] = new Data_Stage(stage_dir);
            }

        }
        public Data_Animation()
        {
            name = "none";
        }
    }

    class Data_Stage
    {
        public Data_Actor[] positions = new Data_Actor[10];
        public Data_Stage(String stage_dir)
        {
            String[] position_dirs = Directory.GetDirectories(stage_dir);

            foreach (String position_dir in position_dirs)
            {
                int index = Int32.Parse(Path.GetFileName(position_dir).Substring(1));
                positions[index - 1] = new Data_Actor(position_dir);
            }
        }

    }

    class Data_Actor
    {
        public string name = "";
        public Data_BodyPart[] bodypart_datas = new Data_BodyPart[Enum.GetNames(typeof(Device.BodyPart)).Length];

        public Data_Actor(string name, string position_dir)
        {
            this.name = name;
            string[] bodyPart_dirs = Directory.GetDirectories(position_dir);

            foreach (string bodyPart_dir in bodyPart_dirs)
            {
                string s_eventType = Path.GetFileName(bodyPart_dir);

                Device.BodyPart bodyPart;
                try
                {
                    bodyPart = (Device.BodyPart)Enum.Parse(typeof(Device.BodyPart), s_eventType, true);
                }
                catch
                {
                    continue;
                }
                
                int index = Array.IndexOf(Enum.GetValues(bodyPart.GetType()), bodyPart);

                bodypart_datas[index] = new Data_BodyPart(bodyPart_dir, bodyPart);

            }
        }

        public Data_Actor(string position_dir) : this("", position_dir)
        {
        }

        public Data_Actor()
        {
        }

    }

    class Data_BodyPart
    {
        public Data_EventType[] eventType_datas = new Data_EventType[Enum.GetNames(typeof(Device.EventType)).Length];
        public Device.BodyPart bodyPart;
        public Data_BodyPart(string bodyPart_dir, Device.BodyPart bodyPart)
        {
            this.bodyPart = bodyPart;

            string[] eventType_dirs = Directory.GetFiles(bodyPart_dir);
            foreach (string eventType_dir in eventType_dirs)
            {
                string s_eventType = Path.GetFileName(eventType_dir).ToLower();
                bool is_funscript = false;
                bool is_estim = false;
                if (s_eventType.EndsWith(".funscript"))
				{
                    is_funscript = true;
                    s_eventType = s_eventType.Remove(s_eventType.Length - ".funscript".Length);
				}
				else if(s_eventType.EndsWith(".mp3"))
				{
                    is_estim = true;
                    s_eventType = s_eventType.Remove(s_eventType.Length - ".mp3".Length);
				}
				else
				{
                    continue;
				}
                

                Device.EventType eventType;
                try
                {
                    eventType = (Device.EventType)Enum.Parse(typeof(Device.EventType), s_eventType, true);
                }
                catch
                {
                    continue;
                }


                int index = Array.IndexOf(Enum.GetValues(eventType.GetType()), eventType);

                if(eventType_datas[index] == null)
				{
                    eventType_datas[index] = new Data_EventType(eventType);
                }

				if (is_estim)
				{
                    eventType_datas[index].Add_Estim(eventType_dir);
                }
                else if (is_funscript)
				{
                    eventType_datas[index].Add_Funscript(eventType_dir);
                }
                
                

            }
        }

    }

    class Data_EventType
    {
        public List<FunScriptAction> actions;
        public Device.EventType eventType;
        public string estim_file = "";

        public Data_EventType(Device.EventType eventType)
        {
            this.eventType = eventType;
        }

        public void Add_Funscript(string file)
		{
            actions = FunScriptLoader.Load(file).ToList();
        }

        public void Add_Estim(string file)
		{
            estim_file = file;
        }

    }
}
