using Buttplug;
using Microsoft.Win32;
using System;
using System.Windows.Forms;

namespace Butthesda
{
	public partial class Form_ScanDevices : Form
    {

        private readonly ButtplugClient client;
        public Form_ScanDevices(ButtplugClient client)
        {
            this.client = client;

            InitializeComponent();
  
            //create handler that gets triggered when we do scan later
            void HandleDeviceAdded(object aObj, DeviceAddedEventArgs aArgs)
            {
                listbox_update(true, aArgs.Device);
            };

            client.DeviceAdded += HandleDeviceAdded;

            void HandleDeviceRemoved(object aObj, DeviceRemovedEventArgs aArgs)
            {
                listbox_update(false, aArgs.Device);
            };

            client.DeviceRemoved += HandleDeviceRemoved;

            void HandleStopScanning(object aObj, EventArgs aEvent)
            {
                stopping_scan = false;
                scanning = false;
                Console.WriteLine("finished scanning");
            };
            client.ScanningFinished += HandleStopScanning;

            foreach (ButtplugClientDevice buttplugClientDevice in client.Devices)
            {
                listbox_update(true, buttplugClientDevice);
            }
            

            scanning = true;
            stopping_scan = false;
            client.StartScanningAsync();


        }

        private delegate void SafeCallDelegate(bool add, ButtplugClientDevice buttplugClientDevice);

        private void listbox_update(bool add, ButtplugClientDevice buttplugClientDevice)
        {
            if (this.listBox_devices.InvokeRequired)
            {
                var d = new SafeCallDelegate(listbox_update);
                this.Invoke(d, new object[] { add, buttplugClientDevice });
            }
            else
            {
                if (add)
                {
                    Device device = new Device(buttplugClientDevice.Name, client, buttplugClientDevice);
                    this.listBox_devices.Items.Add(device);
                    Console.WriteLine(device.name);
                }
                else
                {
                    foreach(Device device in listBox_devices.Items){
                        if (device.device == buttplugClientDevice)
                        {
                            this.listBox_devices.Items.Add(device);
                        }
                    }
                }
                
            }
        }

        private void CheckedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Device.devices.Clear();
            for (int i = 0; i < listBox_devices.Items.Count; i++) {
                if (listBox_devices.GetItemChecked(i))
                {
                    Device device = (Device)listBox_devices.Items[i];
                    Device.devices.Add(device);


                    //Load saved settings from Registery
                    string[] bodyPartNames = Enum.GetNames(typeof(Device.BodyPart));
                    Device.BodyPart[] bodyPartIds = (Device.BodyPart[])Enum.GetValues(typeof(Device.BodyPart));
                    for (int j = 0; j < bodyPartNames.Length; j++)
                    {
                        String bodyPartName = bodyPartNames[j];
                        Device.BodyPart bodyPartId = bodyPartIds[j];

                        string[] eventTypeNames = Enum.GetNames(typeof(Device.EventType));
                        Device.EventType[] eventTypeIds = (Device.EventType[])Enum.GetValues(typeof(Device.EventType));
                        for (int k = 0; k < eventTypeNames.Length; k++)
                        {
                            String eventTypeName = eventTypeNames[k];
                            Device.EventType eventTypeId = eventTypeIds[k];

                            RegistryKey keyEvent = Registry.CurrentUser.OpenSubKey(String.Format(@"SOFTWARE\Butthesda\Device Settings\{0}\{1}\{2}", device.name, bodyPartName, eventTypeName));

                            if (keyEvent != null && Convert.ToBoolean(keyEvent.GetValue("Checked")))
                            {
                                device.SetType(bodyPartId, eventTypeId, true);
                            }
                        }
                    }

                    RegistryKey key = Registry.CurrentUser.OpenSubKey(String.Format(@"SOFTWARE\Butthesda\Device Settings\{0}", device.name));
                    if (key != null)
                    {
                        int max = Convert.ToInt32(key.GetValue("Max"));
                        if (max == 0) { max = 100; };//default
                        int min = Convert.ToInt32(key.GetValue("Min"));

                        device.MinPosition = (double)min / 100.0d;
                        device.MaxPosition = (double)max / 100.0d;
                    }

                }
            }

            this.Close();
        }

        private void Form_scanDevices_Load(object sender, EventArgs e)
        {

        }

        private bool scanning;
        private bool stopping_scan;
        private void Button_Scan_Click(object sender, EventArgs e)
        {
            if (scanning && !stopping_scan)
            {
                stopping_scan = true;
                client.StopScanningAsync();
                
            }

            if (!scanning && !stopping_scan)
            {
                client.StartScanningAsync();
                scanning = true;
            };
           
        }

        private void ListBox_Devices_ItemSelect(object sender, ItemCheckEventArgs e)
        {
            
            Console.WriteLine();
            Device device = (Device)listBox_devices.Items[e.Index];

            if (e.NewValue == CheckState.Checked) {
                
                device.active = true;
            } else
            {
                device.active = false;
            }
        }
    }
}
