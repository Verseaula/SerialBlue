using Android.App;
using Android.Widget;
using Android.OS;
using Android.Views;
using Android.Content;
using Android.Bluetooth;
using Android.Runtime;
using Android.Util;

using System;

namespace SerialBlue
{
    public enum MessageService
    {
        NONE = 0,
        HANDSHAKE,
        CONNECTSTATECHANGE,
        READ,
        WRITE
    }

    public enum ConnectState
    {
        NONE = 0,
        LISTEN,
        CONNECTING,
        CONNECTED,
        CONNECT_FAILED,
        CONNECT_LOST
    }

    [Activity(Label = "SerialBlue", MainLauncher = true,
              Icon = "@drawable/icon",
              Theme = "@style/TitleTheme")]
    public class MainActivity :Activity
    {
        #region declaration
        // Debugging
        private static string TAG = "BluetoothChat";
        private static bool D = true;

        // Intent request codes
        private static int REQUEST_ENABLE_BT = 1;

        // Layout Views
        //menu item
        IMenuItem mMIBTState;

        // Local Bluetooth adapter
        private BluetoothAdapter _BtAdapter = null;
        //
        private BlueToothDeviceDetectReceiver _BlueToothDeviceDetectReceiver;
        // Member fields
        private static ArrayAdapter<String> _PairedDevicesArrayAdapter;
        // Member object for the chat services
        private BluetoothChatService mChatService = null;
        // Array adapter for the conversation thread
        private ArrayAdapter<string> mConversationArrayAdapter;

        // Set Intent extra
        public static String EXTRA_DEVICE_ADDRESS = "device_address";
        public static String BT_WRITE_ACTION = "bluetooth_write";
        public static String BT_READ_ACTION = "bluetooth_read";
        //public static String TOAST = "toast";
        #endregion
        #region life time
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            if (D) Log.Info(TAG, "OnCreate");

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            var toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetActionBar(toolbar);
            ActionBar.Title = GetString(Resource.String.app_name);

            _BtAdapter = BluetoothAdapter.DefaultAdapter;
            if (null == _BtAdapter)
            {
                Toast.MakeText(this, GetString(Resource.String.bluetooth_not_available), ToastLength.Long).Show();
                Finish();
                return;
            }

            // Initialize array adapters. 
            _PairedDevicesArrayAdapter = new ArrayAdapter<string>(this, Resource.Layout.device_name);
            // Find and set up the ListView for paired devices
            ListView pairedListView = (ListView)FindViewById(Resource.Id.paired_devices);
            pairedListView.Adapter = _PairedDevicesArrayAdapter;
            pairedListView.ItemClick += PairedListView_ItemClick;
        }
        protected override void OnStart()
        {
            base.OnStart();
            if (D) Log.Info(TAG, "OnStart");
            if (!_BtAdapter.IsEnabled)
            {

            }
            else
            {

            }
        }
        protected override void OnResume()
        {
            base.OnResume();
            if (D) Log.Info(TAG, "OnResume");

        }
        protected override void OnPause()
        {
            base.OnPause();
            if (D) Log.Info(TAG, "OnPause");

        }
        protected override void OnStop()
        {
            base.OnStop();
            if (D) Log.Info(TAG, "OnStop");
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            _BtAdapter.Disable();
            if (D) Log.Info(TAG, "OnDestroy");
        }
        #endregion
        private void PairedListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            if (_BtAdapter.IsEnabled)
            {
                _BtAdapter.CancelDiscovery();
                String info = ((TextView)(e.View)).Text;
                //String address = info.Substring(info.Length - 17);

                Intent serverIntent = new Intent(this, typeof(DiagViewActivity));
                serverIntent.PutExtra(EXTRA_DEVICE_ADDRESS, info);
                StartActivity(serverIntent);
            }
        }
        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (REQUEST_ENABLE_BT == requestCode)
            {
                if (Result.Ok == resultCode)
                {
                    if (null != mMIBTState)
                    {
                        mMIBTState.SetIcon(Resource.Mipmap.ic_action_bluetooth_on);
                        blueToothDeviceDetect();
                    }
                }
            }
        }
        #region option menu
        /// <summary>
        /// override OnCreateOptionsMenu
        /// </summary>
        /// <param name="menu">menu interface</param>
        /// <returns></returns>
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.main_top_menu,menu);
            mMIBTState = menu.GetItem(0);

            if (_BtAdapter.IsEnabled)
            {
                if (menu.HasVisibleItems)
                {
                    if (null != mMIBTState)
                    {
                        mMIBTState.SetIcon(Resource.Mipmap.ic_action_bluetooth_on);
                        blueToothDeviceDetect();
                    }
                }
            }

            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.menu_bluetooth_key:
                    {
                        if (!_BtAdapter.IsEnabled)
                        {
                            Intent enableIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                            StartActivityForResult(enableIntent, REQUEST_ENABLE_BT);
                        }
                        else
                        {
                            _BtAdapter.Disable();
                            int escape_cnt = 5;
                            while (escape_cnt>0)
                            {
                                escape_cnt--;
                            }
                            item.SetIcon(Resource.Mipmap.ic_action_bluetooth);
                            _PairedDevicesArrayAdapter.Clear();
                            if (null!= _BlueToothDeviceDetectReceiver)
                                UnregisterReceiver(_BlueToothDeviceDetectReceiver);
                        }
                    }
                    break;
                case Resource.Id.menu_search:
                    {
                        doDiscovery();
                    }
                    break;
                default: return false;
            }

            return base.OnOptionsItemSelected(item);
        }
        #endregion
        private void setupChat()
        {
            // Initialize the array adapter for the conversation thread
            //mConversationArrayAdapter = new ArrayAdapter<string>(this, Resource.Layout.message);
            //mConversationView = (ListView)FindViewById(Resource.Id.@in);
            //mConversationView.Adapter = mConversationArrayAdapter;

            //// Initialize the compose field with a listener for the return key
            //mOutEditText = (EditText)FindViewById(Resource.Id.edit_text_out);
            //mOutEditText.EditorAction += MOutEditText_EditorAction;
            
        }

        private void doDiscovery()
        {
            // If we're already discovering, stop it
            if (_BtAdapter.IsDiscovering)
                _BtAdapter.CancelDiscovery();

            // Request discover from BluetoothAdapter
            _BtAdapter.StartDiscovery();
        }

        private void blueToothDeviceDetect()
        {
            if (!_BtAdapter.IsEnabled)
            {
                Toast.MakeText(this, GetString(Resource.String.bluetooth_not_opened), ToastLength.Long).Show();
                return;
            }  

            if(null== _BlueToothDeviceDetectReceiver)
                _BlueToothDeviceDetectReceiver = new BlueToothDeviceDetectReceiver();
            // Register for broadcasts when a device is discovered
            IntentFilter intentFilter = new IntentFilter(BluetoothDevice.ActionFound);
            RegisterReceiver(_BlueToothDeviceDetectReceiver, intentFilter);
            intentFilter = new IntentFilter(BluetoothAdapter.ActionDiscoveryFinished);
            RegisterReceiver(_BlueToothDeviceDetectReceiver, intentFilter);

            _PairedDevicesArrayAdapter.Clear();
            if (_BtAdapter.BondedDevices.Count > 0)
            {
                FindViewById(Resource.Id.title_paired_devices).Visibility = ViewStates.Visible;
                foreach (BluetoothDevice device in _BtAdapter.BondedDevices)
                {
                    _PairedDevicesArrayAdapter.Add(device.Name + "\n" + device.Address);
                }
            }
        }

        private class BlueToothDeviceDetectReceiver : BroadcastReceiver
        {
            public override void OnReceive(Context context, Intent intent)
            {
                string action = intent.Action;
                // When discovery finds a device
                if (BluetoothDevice.ActionFound == action)
                {
                    // Get the BluetoothDevice object from the Intent
                    BluetoothDevice device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                    // If it's already paired, skip it, because it's been listed
                    // already
                    if (device.BondState != Bond.Bonded)
                    {
                        _PairedDevicesArrayAdapter.Add(device.Name + "\n" + device.Address);
                    }
                }
                else if (BluetoothAdapter.ActionDiscoveryFinished == action)
                {
                    if (0 == _PairedDevicesArrayAdapter.Count)
                    {
                        string str = "No devices have been paired";
                        _PairedDevicesArrayAdapter.Add(str);
                    }
                }
            }
        }
    }
}

