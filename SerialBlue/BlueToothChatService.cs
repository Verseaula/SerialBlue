using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Bluetooth;
using Android.Util;
using Java.Util;

namespace SerialBlue
{
    [Service]
    public class BluetoothChatService:Service
    {
        #region declaration
        //Debugging
        private static String TAG = "BluetoothChatService";
        private static bool D = true;
        //message
        private static SHander _Hander;
        private Messenger _Messager;
        private Messenger _CMessager;
        //bluetooth
        private static BluetoothAdapter _BtAdapter = null;
        private BluetoothSocket _Socket;
        //thread
        private Thread _ConnectThread;
        private Thread _TxThread;
        private Thread _RxThread;
        //state
        private static ConnectState _ConnectState = ConnectState.NONE;
        private static int IN_BUFFER_LEN = 256;
        private byte[] _InBuffer = new byte[IN_BUFFER_LEN];
        private static BufferPos _InBuffPos;
        private static List<String> _OutBuffList;
        #endregion

        // Name for the SDP record when creating server socket
        //        private static String NAME = "BluetoothChat";
        // Unique UUID for this application

        private static UUID MY_UUID = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

        // Member fields
        //private BluetoothAdapter mAdapter;
        //private Handler mHandler;
        //private int mState;

        #region life time
        public override void OnCreate()
        {
            base.OnCreate();
            if(D)
                Log.Info(TAG, "OnCreate");

            _OutBuffList = new List<string>();
            _InBuffPos = new BufferPos(IN_BUFFER_LEN);
            _BtAdapter = BluetoothAdapter.DefaultAdapter;
            _Hander = new SHander(SHanderCallBack);
            _Messager = new Messenger(_Hander);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_Socket.IsConnected)
            {
                try
                {
                    _Socket.Close();
                    if (D)
                        Log.Info(TAG, "Connect close");
                }
                catch (Java.IO.IOException e2)
                {
                    if (D)
                        Log.Error(TAG, "unable to close() socket during connection failure", e2);
                }
            }
            if (ConnectState.CONNECTED == _ConnectState)
            {
                _ConnectState = ConnectState.CONNECT_LOST;
                if (_TxThread.IsAlive)
                {
                    _TxThread = null;
                }

                if (_RxThread.IsAlive)
                {
                    _RxThread = null;
                }
            }

            Log.Info(TAG, "OnDestroy");
        }

        public override IBinder OnBind(Intent intent)
        {
            return _Messager.Binder;
        }
        #endregion
        private void setState(ConnectState state)
        {
            _ConnectState = state;
            sendMessage(state);
        }
        private void sendMessage(ConnectState state, MessageService service = MessageService.CONNECTSTATECHANGE)
        {
            if (null == _CMessager)
                return;

            Message msg = new Message();
            msg.What = (int)service;
            msg.Arg1 = (int)state;

            try
            {
                _CMessager.Send(msg);
            }
            catch (RemoteException e)
            {
                Log.Error(TAG, e.ToString());
            }
        }
        internal void sendMessage(String readmsg, MessageService service = MessageService.READ)
        {
            if (null == _CMessager)
                return;

            Message msg = new Message();
            msg.What = (int)service;
            Bundle bundle = new Bundle();
            bundle.PutString(MainActivity.BT_READ_ACTION, readmsg);
            msg.Data = bundle;

            try
            {
                _CMessager.Send(msg);
            }
            catch (RemoteException e)
            {
                if (D)
                    Log.Error(TAG, e.ToString());
            }
        }

        private void connect(string addr)
        {
            BluetoothDevice device  = _BtAdapter.GetRemoteDevice(addr);
            _ConnectThread = new Thread(new ParameterizedThreadStart(connectTask));
            _ConnectThread.Name = "Connect thread";
            _ConnectThread.Start(device);
        }
        private void connectTask(object dev)
        {
            BluetoothDevice device = (BluetoothDevice)dev;
            BluetoothSocket socket = null;

            BluetoothSocket tmp = null;
            try
            {
                tmp = device.CreateRfcommSocketToServiceRecord(MY_UUID);
            }
            catch (Java.IO.IOException e)
            {
                if (D)
                    Log.Error(TAG, "create() failed", e);
            }
            socket = tmp;
            _Socket = tmp;

            Log.Info(TAG, "Begin connect thread");
            try
            {
                socket.Connect();
            }
            catch (Java.IO.IOException e)
            {
                connectFailed();
                try
                {
                    socket.Close();
                }
                catch (Java.IO.IOException e2)
                {
                    Log.Error(TAG, "unable to close() socket during connection failure", e2);
                }

                return;
            }
            _ConnectThread = null;
            if (D)
                Log.Info(TAG, "Connected");
            connected(socket, device);
        }

        private void connected(BluetoothSocket socket, BluetoothDevice device)
        {
            if (null != _ConnectThread) { _ConnectThread = null; }
            if (null != _RxThread) { _RxThread = null; }
            if (null != _TxThread) { _TxThread = null; }

            setState(ConnectState.CONNECTED);

            _RxThread = new Thread(new ParameterizedThreadStart(rxTask));
            _RxThread.Name = "Receive thread";
            _RxThread.Start(socket);

            _TxThread = new Thread(new ParameterizedThreadStart(txTask));
            _TxThread.Name = "Transmit thread";
            _TxThread.Start(socket);
        }

        private void txTask(object dev)
        {
            BluetoothSocket socket = (BluetoothSocket)dev;
            Stream outStream = null;
            int timer_cnt = 10;

            if (D)
                Log.Info(TAG, "create TxThread");
            try
            {
                outStream = socket.OutputStream;
            }
            catch (Java.IO.IOException e)
            {
                if (D)
                    Log.Error(TAG, "tx socket not created", e);
            }

            while (ConnectState.CONNECTED==_ConnectState)
            {
                if (timer_cnt > 0)
                    timer_cnt--;
                if (0 == timer_cnt)
                {
                    if (_OutBuffList.Count > 0)
                    {
                        String str = _OutBuffList[0];
                        byte[] sendBytes = System.Text.Encoding.Default.GetBytes(str);
                        outStream.Write(sendBytes, 0, str.Length);
                        _OutBuffList.RemoveAt(0);
                        timer_cnt = 10;
                    }
                }

                Thread.Sleep(50);
            }

            _TxThread = null;
        }

        private void rxTask(object dev)
        {
            BluetoothSocket socket = (BluetoothSocket)dev;
            Stream inStream = null;
            StringBuilder sb = new StringBuilder();
            int cnt = 0;
            byte read = 0x00;

            Log.Info(TAG, "create RxThread");
            try
            {
                inStream = socket.InputStream;
            }
            catch (Java.IO.IOException e)
            {
                if (D)
                    Log.Error(TAG, "rx socket not created", e);
            }

            while (ConnectState.CONNECTED == _ConnectState)
            {
                if (!socket.IsConnected)
                {
                    connectLost();
                    break;
                }

                cnt = 0;
                sb.Clear();
                while (inStream.IsDataAvailable()&& cnt<255)
                {
                    read = (Byte)inStream.ReadByte();
                    //_InBuffer[_InBuffPos.In] = read;
                    //_InBuffPos.Push();
                    sb.Append((char)read);
                    cnt++;
                }
                if(cnt>0)
                    sendMessage(sb.ToString());

                Thread.Sleep(50);
            }

            _RxThread = null;
        }

        private void connectFailed()
        {
            setState(ConnectState.CONNECT_FAILED);
        }
        private void connectLost()
        {
            setState(ConnectState.CONNECT_LOST);
        }
        #region handler
        private void SHanderCallBack(Message msg)
        {
            switch (msg.What)
            {
                case (int)MessageService.HANDSHAKE:
                    {
                        _CMessager = msg.ReplyTo;
                        String addr = msg.Data.GetString(MainActivity.EXTRA_DEVICE_ADDRESS);

                        connect(addr);
                        setState(ConnectState.CONNECTING);
                    }
                    break;
                case (int)MessageService.WRITE:
                    {
                        String str = msg.Data.GetString(MainActivity.BT_WRITE_ACTION);
                        _OutBuffList.Add(str);
                    }break;
                default: break;
            }
        }
        private class SHander : Handler
        {
            private Action<Message> _Action;
            public SHander(Action<Message> action)
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
        #region ring
        internal class BufferPos
        {
            private int _Size = 0;
            /// <summary>
            /// in pos
            /// </summary>
            private int _In = 0;
            public int In
            {
                get { return _In; }
            }
            #region view
            /// <summary>
            /// out pos
            /// </summary>
            private int _Out = 0;
            public int Out
            {
                get { return _Out; }
            }
            /// <summary>
            /// num
            /// </summary>
            private uint _Num = 0;
            public uint Num
            {
                get { return _Num; }
            }
            #endregion
            public BufferPos(int size)
            {
                _In = 0;
                _Out = 0;
                _Num = 0;
                _Size = size;
            }
            /// <summary>
            /// init
            /// </summary>
            public void Init(int size)
            {
                _In = 0;
                _Out = 0;
                _Num = 0;
                _Size = size;
            }
            /// <summary>
            /// push
            /// </summary>
            public void Push()
            {
                _In = address(_In);
                if (_Num < _Size)
                    _Num++;
            }
            /// <summary>
            /// pull
            /// </summary>
            public void Pull_View()
            {
                _Out = address(_Out);
                if (_Num > 0)
                    _Num--;
            }
            /// <summary>
            /// clear
            /// </summary>
            public void Clear_View()
            {
                _Out = _In;
                _Num = 0;
            }
            /// <summary>
            /// address
            /// </summary>
            private int address(int i)
            {
                return (_Size == (i + 1)) ? 0 : i + 1;
            }
        }
        #endregion
    }

}