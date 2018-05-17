using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using Android.Bluetooth;
using Android.Views.InputMethods;

namespace SerialBlue
{
    [Activity(Label = "DiagViewActivity",
              Theme = "@style/TitleTheme")]
    public class DiagViewActivity : Activity
    {
        #region declaration
        // Debugging
        private static string TAG = "DiagView";
        private static bool D = true;

        //Local views
        private IMenuItem mMIConnectState;
        private ListView mConversationView;
        private EditText mOutEditText;
        private Button mSendButton;
        // Array adapter for the conversation thread
        private ArrayAdapter<String> mConversationArrayAdapter;
        // String buffer for outgoing messages
        //private StringBuffer mOutStringBuffer;
        //Message
        internal   Messenger _RMessager;
        private static CHandler _Handler;
        internal Messenger _CMessager;
        private CServiceConnection _BTServiceConnection;
        //state
        internal ConnectState _ConnectState = ConnectState.NONE;

        internal string _Addr = "";
        #endregion
        #region life time
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create your application here
            if (D) Log.Info(TAG, "OnCreate");

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.DiagView);
            var toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetActionBar(toolbar);

            // Initialize the array adapter for the conversation thread
            mConversationArrayAdapter = new ArrayAdapter<String>(this, Resource.Layout.message);
            mConversationView = (ListView)FindViewById(Resource.Id.inLv);
            mConversationView.Adapter = mConversationArrayAdapter;
            // Initialize the compose field with a listener for the return key
            mOutEditText = (EditText)FindViewById(Resource.Id.edit_text_out);
            mOutEditText.SetOnEditorActionListener(new TVActionListener(this));
            mSendButton = (Button)FindViewById(Resource.Id.button_send);
            mSendButton.Click += MSendButton_Click;

            //get intent info
            String info = Intent.GetStringExtra(MainActivity.EXTRA_DEVICE_ADDRESS);
            String name = info.Substring(0, info.Length - 17);
            _Addr = info.Substring(info.Length - 17);
            ActionBar.Title = name;
            //setup handler
            _Handler = new CHandler(RefreshUI);
            _CMessager = new Messenger(_Handler);
            //bind service
            _BTServiceConnection = new CServiceConnection(this);
            Intent serviceIntent = new Intent(this, typeof(BluetoothChatService));
            BindService(serviceIntent, _BTServiceConnection, Bind.AutoCreate);
        }

        protected override void OnStart()
        {
            base.OnStart();
            if (D) Log.Info(TAG, "OnStart");
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
            UnbindService(_BTServiceConnection);
            if (D) Log.Info(TAG, "OnDestroy");
        }
        #endregion
        #region option menu
        /// <summary>
        /// override OnCreateOptionsMenu
        /// </summary>
        /// <param name="menu">menu interface</param>
        /// <returns></returns>
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.diag_top_menu, menu);
            mMIConnectState = menu.GetItem(0);

            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.menu_connect:
                    {
                        
                    }
                    break;
                case Resource.Id.menu_clear:
                    {
                        mConversationArrayAdapter.Clear();
                    }
                    break;
                default: return false;
            }

            return base.OnOptionsItemSelected(item);
        }
        private void sendMessage(MessageService service)
        {
            if (null == _RMessager)
                return;

            Message msg = new Message();
            msg.What = (int)service;

            try
            {
                _RMessager.Send(msg);
            }
            catch (RemoteException e)
            {
                Log.Error(TAG, e.ToString());
            }
        }
        #endregion
        private class TVActionListener : Java.Lang.Object,TextView.IOnEditorActionListener
        {
            private DiagViewActivity _LocalActivity;
            public TVActionListener(DiagViewActivity da)
            {
                _LocalActivity = da;
            }
            public bool OnEditorAction(TextView v, [GeneratedEnum] ImeAction actionId, KeyEvent e)
            {
                if (ConnectState.CONNECTED != _LocalActivity._ConnectState)
                    return true;

                if (ImeAction.ImeNull == actionId && KeyEventActions.Up == e.Action)
                {
                    String msg = v.Text;
                    _LocalActivity.SendMessage(msg);
                }

                return true;
            }
            
        }
        private void MSendButton_Click(object sender, EventArgs e)
        {
            if (ConnectState.CONNECTED != _ConnectState)
                return;

            String msg = mOutEditText.Text;
            if ("" == msg)
                return;

            SendMessage(msg);
            mConversationArrayAdapter.Add("Tx:["+msg.Length.ToString()+"] " + msg);
            mOutEditText.Text = "";
        }
        internal void SendMessage(String info)
        {
            Message msg = new Message();
            msg.What = (int)MessageService.WRITE;
            Bundle bundle = new Bundle();
            bundle.PutString(MainActivity.BT_WRITE_ACTION, info);
            msg.Data = bundle;
            try
            {
                _RMessager.Send(msg);
            }
            catch (RemoteException e)
            {
                Log.Error(TAG, e.ToString());
            }
        }
        #region service handle
        private class CServiceConnection : Java.Lang.Object,IServiceConnection
        {
            private DiagViewActivity _LocalActivity;
            public CServiceConnection(DiagViewActivity da)
            {
                _LocalActivity = da;
            }
            public void OnServiceConnected(ComponentName name, IBinder service)
            {
                _LocalActivity._RMessager = new Messenger(service);
                //send local messager to remote
                handshake();

                Log.Info(TAG, "Service Connected");
            }

            public void OnServiceDisconnected(ComponentName name)
            {
                _LocalActivity._RMessager.Dispose();
                _LocalActivity._RMessager = null;

                Log.Info(TAG, "Service Disconnected");
            }

            private void handshake()
            {
                Message msg = new Message();
                msg.What = (int)MessageService.HANDSHAKE;
                Bundle bundle = new Bundle();
                bundle.PutString(MainActivity.EXTRA_DEVICE_ADDRESS, _LocalActivity._Addr);
                msg.Data = bundle;
                msg.ReplyTo = _LocalActivity._CMessager;
                try
                {
                    _LocalActivity._RMessager.Send(msg);
                }
                catch (RemoteException e)
                {
                    Log.Error(TAG, e.ToString());
                }
            }
        }

        private void RefreshUI(Message msg)
        {
            switch (msg.What)
            {
                case (int)MessageService.CONNECTSTATECHANGE:
                    {
                        switch (msg.Arg1)
                        {
                            case (int)ConnectState.NONE:
                            case (int)ConnectState.LISTEN:
                                {
                                    mMIConnectState.SetIcon(Resource.Mipmap.ic_action_unconnect);
                                    _ConnectState = ConnectState.NONE;
                                }
                                break;
                            case (int)ConnectState.CONNECTING:
                                {
                                    mMIConnectState.SetIcon(Resource.Mipmap.ic_action_connecting);
                                    Toast.MakeText(this, GetString(Resource.String.connecting_device), ToastLength.Long).Show();
                                    _ConnectState = ConnectState.CONNECTING;
                                }
                                break;
                            case (int)ConnectState.CONNECTED:
                                {
                                    mMIConnectState.SetIcon(Resource.Mipmap.ic_action_connected);
                                    Toast.MakeText(this, GetString(Resource.String.connect_device_ok), ToastLength.Long).Show();
                                    _ConnectState = ConnectState.CONNECTED;
                                }
                                break;
                            case (int)ConnectState.CONNECT_FAILED:
                                {
                                    mMIConnectState.SetIcon(Resource.Mipmap.ic_action_unconnect);
                                    Toast.MakeText(this, GetString(Resource.String.connect_device_failed), ToastLength.Long).Show();
                                    _ConnectState = ConnectState.NONE;
                                }
                                break;
                            case (int)ConnectState.CONNECT_LOST:
                                {
                                    mMIConnectState.SetIcon(Resource.Mipmap.ic_action_unconnect);
                                    Toast.MakeText(this, GetString(Resource.String.connect_device_lost), ToastLength.Long).Show();
                                    _ConnectState = ConnectState.NONE;
                                }
                                break;
                            default: break;
                        }
                    }break;
                case (int)MessageService.READ:
                    {
                        String str = msg.Data.GetString(MainActivity.BT_READ_ACTION);
                        mConversationArrayAdapter.Add("Rx:[" + str.Length.ToString() + "] " + str);
                    }
                    break;
                default:break;
            }
        }
        private class CHandler : Handler
        {
            private Action<Message> _Action;
            public CHandler(Action<Message> action)
            {
                _Action = action;
            }
            public override void HandleMessage(Message msg)
            {
                base.HandleMessage(msg);

                _Action?.Invoke(msg);
            }
        }
        #endregion
    }
}