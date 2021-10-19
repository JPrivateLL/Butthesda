using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;

namespace Butthesda
{


    class EventFileScanner
    {
        public event EventHandler Notification_Message;
        public event EventHandler Warning_Message;
        public event EventHandler Error_Message;
        public event EventHandler Debug_Message;

        public event EventHandler Arousal_Updated;
        public event EventHandler Save_Loaded;
        public event EventHandler DD_Device_Update;

        public bool inMenu = false;
        public int arousal = 50;



        public DD_Device_Type[] dd_devices = new DD_Device_Type[Enum.GetNames(typeof(DD_Device_Location)).Length];

        public enum DD_Device_Location
        {
            Vaginal = 0,
            Anal,
            VaginalPiecing,
            NipplePiercing
        }

        public enum DD_Device_Type
        {
            none = 0,
            pump,
            magic,
            normal
        }

        private readonly string Game_Path;
        private readonly VibrationEvents vibrationEvents;

        public EventFileScanner(string Game_Path, Memory_Scanner memory_scanner, VibrationEvents vibrationEvents)
        {
            this.Game_Path = Game_Path;
            this.vibrationEvents = vibrationEvents;
            memory_scanner.AnimationEvent += MemoryScanner_AnimationEvent;
        }


        private string last_idle = "";
        private Running_Event running_animation_event = Running_Event.Empty();
        private void MemoryScanner_AnimationEvent(object sender, StringArg e)
        {
            string animation = e.String;
            Device.BodyPart bodyparts_specific;

            switch (animation)
            {
                case "FootRight":
                case "FootLeft":
                case "JumpDown":

                case "IdleChairSitting":
                case "idleChairGetUp":

                case "tailCombatIdle":
                case "tailSneakIdle":
                case "IdleStop":

                case "weaponSwing":
                case "weaponLeftSwing":

                case "tailMTLocomotion":
                case "tailSneakLocomotion":
                case "tailCombatLocomotion":
                    if (last_idle == "JumpUp") running_animation_event.End();
                    if (!running_animation_event.ended) break; //only run one event per device per time

                    if (last_idle == animation) break; //prevent idle spam when slowly rotating arround with weapon drawn
                    last_idle = animation;  

                    DD_MoveAnimation_Event("dd device footstep");
                    break;
                case "JumpUp":
                    last_idle = animation;
                    DD_MoveAnimation_Event("dd device jumpup");
                    break;
                case "JumpLandEnd":
                    last_idle = animation;
                    DD_MoveAnimation_Event("dd device jumplandend");
                    break;
            }
        }

        public Device.BodyPart DD_Type_To_BodyPart(DD_Device_Location dd_location)
        {
            if (dd_location == DD_Device_Location.Vaginal) return Device.BodyPart.Vaginal;
            if (dd_location == DD_Device_Location.Anal) return Device.BodyPart.Anal;
            if (dd_location == DD_Device_Location.VaginalPiecing) return Device.BodyPart.Clit;
            return Device.BodyPart.Breast;
        }

        private void DD_MoveAnimation_Event(String eventName)
		{
            Device.BodyPart bodyparts_specific = 0;
            for (int i = 0; i < dd_devices.Length; i++)
            {
                if (dd_devices[i] == DD_Device_Type.none) continue; // check if dd_device is used
                                                                    // get bodypart to which the dd_device belongs
                Device.BodyPart test = DD_Type_To_BodyPart((DD_Device_Location)i);
                bodyparts_specific = bodyparts_specific | test;
            }

            if (bodyparts_specific == 0) return;

            running_animation_event = vibrationEvents.PlayEvent(eventName, bodyparts_specific: bodyparts_specific);

        }


        private void PlaySound()
        {
            using (var soundPlayer = new SoundPlayer(@"c:\Windows\Media\Windows Information Bar.wav"))
            {
                soundPlayer.PlaySync();
            }
        }



        private List<Custom_Running_Event> Custom_Running_Events = new List<Custom_Running_Event>();

        public void Run()
        {
            Thread.Sleep(100);
            string path = Game_Path + @"\FunScripts\link.txt";

            const Int32 BufferSize = 128;
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize))
            {
                long line_nr = 0;
                bool loading_game = true;
                bool skipping_old = true;
                List<Running_Event> dd_vibrations = new List<Running_Event>();
                while (true)
                {
                    String line = streamReader.ReadLine();
                    
                    if (line == null)
                    {
                        skipping_old = false; //we skipped all old lines
                        Thread.Sleep(10);
                    }
                    else
                    {
                        line_nr++;
                        line = line.ToLower().Replace("\'", "\"");

                        //Console.WriteLine(line);
                        if (!line.StartsWith("{")) {
                            Debug_Message?.Invoke(this, new StringArg(String.Format("ESP msg - {0}",line)));
                            continue;
                        };

                        JObject json;
                        try { json = JObject.Parse(line); }
                        catch
                        {
                            Warning_Message?.Invoke(this, new StringArg(String.Format("Couldn't format line {0}: {1}",line_nr, line)));
                            continue;
                        };

                        string mod_property = (string)json.Property("mod");
                        string event_property = (string)json.Property("event");
                        string type_property = (string)json.Property("type");


                        //this code runs mainly purpose is to skip old lines of code in the event file
                        if (loading_game || skipping_old)
                        {
                            bool allowed = false;
                            if(mod_property == "game")
                            {
                                if(event_property == "loading save" || event_property == "loading save done")
                                {
                                    allowed = true;
                                }
                                
                            }

                            if (mod_property == "dd")
                            {
                                if(event_property == "(de)equiped")
                                {
                                    allowed = true;
                                }
                            }

                            if (mod_property =="sla")  
                            {
                                allowed = true;
                            }

                            if (!allowed)
                            {
                                continue;
                            }

                            
                        }

                        switch (mod_property)
                        {
                            case "game":
                                switch (event_property)
                                {
                                    case "damage":
                                        
                                        break;
                                    case "loading save":
                                        loading_game = true;

                                        string mods_found = "";

                                        if ((bool)json.Property("sla_running"))
                                        {
                                            mods_found += "- SexLab Arousal\n";
                                        }
                                        if ((bool)json.Property("dd_running"))
                                        {
                                            mods_found += "- Deviouse Devices\n";
                                        }
                                        if ((bool)json.Property("mme_running"))
                                        {
                                            mods_found += "- Milk Mod Economy\n";
                                        }
                                        if ((bool)json.Property("bf_running"))
                                        {
                                            mods_found += "- Being Female\n";
                                        }
                                        if ((bool)json.Property("sgo_running"))
                                        {
                                            mods_found += "- Soul Gem Oven\n";
                                        }
                                        Notification_Message?.Invoke(this, new StringArg("Loading save game"));
                                        Save_Loaded?.Invoke(this, new StringArg(mods_found));
                                        break;
                                    case "loading save done":
                                        loading_game = false;
                                        Notification_Message?.Invoke(this, new StringArg("Save game Loaded"));
                                        break;
                                    case "menu opened"://TODO might need to re add this if the animation timer based check doest work good enough
                                        //form_EventFileReader.GamePaused(true);
                                        inMenu = true;
                                        break;
                                    case "menu closed":
                                        //form_EventFileReader.GamePaused(false);
                                        inMenu = false;
                                        break;

                                }
                                break;
                            case "sla":
                                arousal = (int)json.Property("arousal");
                                Arousal_Updated?.Invoke(this, new IntArg(arousal));
                                break;
                            case "dd":

                                switch (event_property)
                                {
                                    case "(de)equiped":

                                        string[] devices_names = new string[Enum.GetNames(typeof(DD_Device_Location)).Length];

                                        //check which device was (de)equiped
                                        for (int i = 0; i < dd_devices.Length; i++)
                                        {
                                            DD_Device_Type dd_device_current_type = dd_devices[i];
                                            String[] test = Enum.GetNames(typeof(DD_Device_Location));
                                            string s_location = Enum.GetNames(typeof(DD_Device_Location))[i].ToLower();
                                            string s_location_type = (string)json.Property(s_location);
                                            string[] s_location_types = Array.ConvertAll(Enum.GetNames(typeof(DD_Device_Type)), d => d.ToLower());
                                            DD_Device_Type dd_device_new_type = (DD_Device_Type)Array.IndexOf(s_location_types, s_location_type);

                                            //check if this was the device that changed
                                            //And we dont run any event when the game was loading
                                            if (!(loading_game || skipping_old) && dd_device_current_type != dd_device_new_type)
                                            {
                                                //check if device was added or removed
                                                if (dd_device_new_type != DD_Device_Type.none)
                                                {//device was removed
                                                    Notification_Message?.Invoke(this, new StringArg("Deviouse Device equiped: " + s_location + " " + s_location_type));
                                                    vibrationEvents.PlayEvent("dd device equiped " + s_location);
                                                }
                                                else
                                                {//device was added
                                                    Notification_Message?.Invoke(this, new StringArg("Deviouse Device de-equiped: " + s_location + " " + s_location_type));
                                                    vibrationEvents.PlayEvent("dd device de-equiped " + s_location);
                                                }
                                                //break;
                                            }
                                            dd_devices[i] = dd_device_new_type;

                                            devices_names[i] = s_location_type;


                                        }
                                        DD_Device_Update?.Invoke(this, new StringArgs(devices_names));
                                        


                                        break;
                                    case "vibrate effect start":
                                        float arg = (float)json.Property("arg");

                                        //DD sends us (vibStrength * numVibratorsMult) as argument. We want to use vibStrength so we need to devide it by numVibratorsMult
                                        float[] dd_device_multipliers = { 0.7f, 0.3f, 0.25f, 0f, 0.5f };
                                        float dd_device_multiplier = 0f;
                                        for(int i = 0; i < dd_devices.Length; i++)
										{
                                            DD_Device_Type dd_device_current_type = dd_devices[i];
                                            if(dd_device_current_type != DD_Device_Type.none)
											{
                                                dd_device_multiplier += dd_device_multipliers[i];
                                            }
                                        }
										if (dd_device_multiplier > 1){ dd_device_multiplier = 1; }
                                        arg /= dd_device_multiplier;

                                        //numVibratorsMult can be x1.15 if blindfolded, by rounding it down we always get the right number (5*1.15=5.75)
                                        int strength = (int)Math.Floor(arg);

                                        //end old vibrations if they where running
                                        foreach (Running_Event dd_vibration in dd_vibrations)
                                        {
                                            dd_vibration.End();
                                        }

                                        //add new vibration for each dd_device in use
                                        for (int i = 0; i < dd_devices.Length; i++)
										{
                                            string location = Enum.GetNames(typeof(DD_Device_Location))[i].ToLower();
                                            dd_vibrations.Add(vibrationEvents.PlayEvent("dd vibrate strength_" + strength + " " + location, repeating: true));
                                        }
                                        
                                        Notification_Message?.Invoke(this, new StringArg("Deviouse Device vibrate " + strength));
                                        
                                        
										break;
                                    case "vibrate effect stop":
                                        Notification_Message?.Invoke(this, new StringArg("Deviouse Device vibrate stop " + (float)json.Property("arg")));
                                        //end old vibrations if they where running
                                        foreach (Running_Event dd_vibration in dd_vibrations)
                                        {
                                            dd_vibration.End();
                                        }
                                        break;
                                    case "orgasm":
                                        Notification_Message?.Invoke(this, new StringArg("Deviouse Device orgasm " + (float)json.Property("arg")));
                                        break;
                                    case "edged":
                                        Notification_Message?.Invoke(this, new StringArg("Deviouse Device edged " + (float)json.Property("arg")));
                                        break;
                                    case "device event":
                                        string type = (string)json.Property("type");
                                        Notification_Message?.Invoke(this, new StringArg("Deviouse Device event: " + type));


                                        string[] general_events = { "drip", "trip over", "stamina drain", "blindfold mystery", "restraints+armor", "posture collar",
                                            "wet padding", "blindold trip", "nipple piercings","tight corset","plug moan","trip and fall","bump pumps","struggle",
                                            "belted empty","mounted","tight gloves","bra chafing","periodic shock", "arm cuff fumble","draugr plug vibration",
                                            "restrictive collar","mana drain","harness","horny","chaos plug","belt chafing","health drain","organicvibrationeffect",};

                                        /*{ "drip", "trip over" , "stamina drain", "blindfold mystery", "restraints+armor", "posture collar",
                                        "wet padding", "blindold trip", "nipple piercings","tight corset","plug moan","trip and fall","bump pumps","struggle",
                                            "belted empty","mounted","tight gloves","bra chafing","periodic shock", "arm cuff fumble","draugr plug vibration",
                                            "restrictive collar","mana drain","vibration","harness","horny","chaos plug","belt chafing","health drain","organicvibrationeffect",};
                                        */

                                        if (Array.Exists(general_events, element => element  == type)){//general non device related
                                            vibrationEvents.PlayEvent("dd "+type);
                                        }else if(type == "xxx")
										{

										}

                                        break;

                                }
                                break;
                            case "sexlab":

                                string name = (string)json.Property("name");
                                

                                switch (event_property)
                                {
                                    case "animation started":
                                    case "animation changed":
                                        {
                                            int stage = (int)json.Property("stage")-1;
                                            int position = (int)json.Property("pos");
                                            bool usingStrapon = (bool)json.Property("usingstrappon");
                                            string[] tags = json.Property("tags").Value.ToObject<string[]>();

              
                                            Notification_Message?.Invoke(this, new StringArg("SexLab " + event_property + " : \"" + name + "\" stage:" + stage + " position: " + position + " using strapon: " + usingStrapon));
                                            bool found = vibrationEvents.SexLab_StartAnimation(name, stage, position, usingStrapon);
											if (!found)
											{
                                                Notification_Message?.Invoke(this, new StringArg("Using Generic event"));
                                                vibrationEvents.SexLab_StartAnimation("Generic", stage, 0, false);
                                            }
                                            if (event_property == "animation changed")
                                            {
                                                vibrationEvents.SexLab_Update_Event();
                                            }
                                        }
                                        break;
                                    case "animation ended":
                                        Notification_Message?.Invoke(this, new StringArg("SexLab animation stopped"));
                                        vibrationEvents.SexLab_StopAnimation();
                                        break;
                                    case "stage started":
                                        {
                                            int stage = (int)json.Property("stage")-1;
                                            Notification_Message?.Invoke(this, new StringArg("SexLab stage started: " + stage));
                                            vibrationEvents.SexLab_SetStage(stage);
                                        }
                                        break;
                                    case "position changed":
                                        {
                                            int position = (int)json.Property("pos");
                                            Notification_Message?.Invoke(this, new StringArg("SexLab position changed: " + position));
                                            vibrationEvents.SexLab_SetPosition(position);
                                        }
                                        break;
                                    case "orgasm started":
                                        Notification_Message?.Invoke(this, new StringArg("SexLab orgasm"));
                                        vibrationEvents.SexLab_Start_Orgasm();
                                        break;
                                    case "orgasm ended":
                                        Notification_Message?.Invoke(this, new StringArg("SexLab orgasm ended"));
                                        vibrationEvents.SexLab_Stop_Orgasm();
                                        break;
                                }
                                break;
                            case "mme":
                                int mpas = (int)json.Property("mpas");
                                int milkingType = (int)json.Property("milkingtype");
                                switch (event_property)
                                {
                                    case "StartMilkingMachine":

                                        break;
                                    case "StopMilkingMachine":

                                        break;
                                    case "FeedingStage":

                                        break;
                                    case "MilkingStage":

                                        break;
                                    case "FuckMachineStage":

                                        break;
                                }
                                break;
                            case "custom":

                                int id = (int)json.Property("type");

                                if (event_property == "start")
								{
                                    Custom_Running_Events.Add(new Custom_Running_Event(id, vibrationEvents.PlayEvent(type_property)));
                                }
								else
								{
                                    for(int i = Custom_Running_Events.Count-1; i >= 0 ; i--)
									{
                                        Custom_Running_Event custom_Event = Custom_Running_Events[i];
                                        if (custom_Event.id == id)
                                        {
                                            custom_Event.running_Event.End();
                                        }
										if (custom_Event.running_Event.ended)
										{
                                            Custom_Running_Events.RemoveAt(i);
                                        }

                                    }
                                }
                                break;
          
                        }



                    }
                }
            }
        }       
    }

	class Custom_Running_Event
	{
        public int id;
        public Running_Event running_Event;
        public Custom_Running_Event(int id, Running_Event running_Event)
		{
            this.id = id;
            this.running_Event = running_Event;
		}
    }
}
