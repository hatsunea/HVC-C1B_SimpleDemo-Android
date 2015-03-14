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
using omron.HVC;
using Android.Bluetooth;
using System.Threading;
using System.Threading.Tasks;

namespace HVC_Test.Resources
{
    [Activity(Label = "HVC_Test", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        public const int EXECUTE_STOP = 0;
        public const int EXECUTE_START = 1;
        public const int EXECUTE_END = -1;

        private HVC_BLE HvcBle = null;
        private HVC_PRM HvcPrm = null;
        private HVC_RES HvcRes = null;

        private static int IsExecute = 0;
        private static int SelectDeviceNo = -1;
        private static List<BluetoothDevice> DeviceList = null;
        private static bool IsShowDialog = false;

        private SynchronizationContext Context = SynchronizationContext.Current;
        TextView StatusTextView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);
            var findButton = FindViewById<Button>(Resource.Id.FindButton);
            var startButton = FindViewById<Button>(Resource.Id.StartButton);
            this.StatusTextView = FindViewById<TextView>(Resource.Id.StatusTextView);

            findButton.Click += delegate
            {
                if (IsExecute == EXECUTE_START)
                {
                    // トースト表示
                    Toast.MakeText(this, "You are executing now", ToastLength.Short).Show();
                }
                else
                {
                    SelectDeviceNo = -1;
                    IsShowDialog = true;
                    var newFragment = new DeviceDialogFragment();
                    newFragment.Cancelable = false;
                    newFragment.Show(FragmentManager, "Bluetooth Devices");
                }
            };

            startButton.Click += delegate
            {
                if (SelectDeviceNo == -1)
                {
                    // トースト表示
                    Toast.MakeText(this, "You must select device", ToastLength.Short).Show();
                }
                else
                {
                    if (IsExecute == EXECUTE_STOP)
                    {
                        startButton.SetText(Resource.String.EndButton);
                        IsExecute = EXECUTE_START;
                    }
                    else
                    {
                        if (IsExecute == EXECUTE_START)
                        {
                            startButton.SetText(Resource.String.StartButton);
                            IsExecute = EXECUTE_STOP;
                        }
                    }
                }
            };

            /* */
            HvcBle = new HVC_BLE();
            HvcPrm = new HVC_PRM();
            HvcRes = new HVC_RES();

            HvcBle.SetCallBack(new hvcCallback(this));
            Action async = HVCDeviceThread;
            System.Threading.Tasks.Task.Run(async);
        }

        public async void HVCDeviceThread()
        {
            IsExecute = EXECUTE_STOP;
            while (IsExecute != EXECUTE_END)
            {
                BluetoothDevice device = await this.SelectHVCDevice("OMRON_HVC.*|omron_hvc.*");
                if (device == null)
                {
                    continue;
                }

                //接続
                if (IsExecute == EXECUTE_START)
                {
                    this.HvcBle.Connect(global::Android.App.Application.Context, device);
                    await ExecuteWait(15);

                    this.HvcPrm.CameraAngle = HVC_PRM.HVC_CAMERA_ANGLE.HVC_CAMERA_ANGLE_0;
                    this.HvcPrm.Face.MinSize = 100;
                    this.HvcPrm.Face.MaxSize = 400;
                    await this.HvcBle.SetParam(this.HvcPrm);
                    await ExecuteWait(15);

                    //１秒ごとにデータを取得する
                    while (IsExecute != EXECUTE_STOP)
                    {
                        int nUseFunc = HVC.HVC_ACTIV_BODY_DETECTION |
                                       HVC.HVC_ACTIV_HAND_DETECTION |
                                       HVC.HVC_ACTIV_FACE_DETECTION |
                                       HVC.HVC_ACTIV_FACE_DIRECTION |
                                       HVC.HVC_ACTIV_AGE_ESTIMATION |
                                       HVC.HVC_ACTIV_GENDER_ESTIMATION |
                                       HVC.HVC_ACTIV_GAZE_ESTIMATION |
                                       HVC.HVC_ACTIV_BLINK_ESTIMATION |
                                       HVC.HVC_ACTIV_EXPRESSION_ESTIMATION;
                        //int nUseFunc = HVC.HVC_ACTIV_FACE_DETECTION |
                        //    HVC.HVC_ACTIV_FACE_DIRECTION |
                        //    HVC.HVC_ACTIV_AGE_ESTIMATION | 
                        //    HVC.HVC_ACTIV_GENDER_ESTIMATION | 
                        //    HVC.HVC_ACTIV_EXPRESSION_ESTIMATION;
                        await this.HvcBle.Execute(nUseFunc, this.HvcRes);
                        await ExecuteWait(30);
                    }

                    //切断
                    this.HvcBle.Disconnect();
                }
            }
        }

        public async Task ExecuteWait(int nWaitCount)
        {
            do
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(1000);
                }
                catch (Exception e)
                {
                    // TODO Auto-generated catch block
                    Console.WriteLine(e.ToString());
                    Console.Write(e.StackTrace);
                }
                if (!this.HvcBle.IsBusy())
                {
                    return;
                }
                nWaitCount--;
            } while (nWaitCount > 0);
        }

        private async Task<BluetoothDevice> SelectHVCDevice(string regStr)
        {
            while (SelectDeviceNo < 0)
            {
                if (IsShowDialog)
                {
                    BleDeviceSearch bleSearch = new BleDeviceSearch(ApplicationContext);

                    // トースト表示
                    this.Context.Post((o) =>
                    {
                        Toast.MakeText(this, "You can select a device", ToastLength.Short).Show();
                    }, null);
                    while (IsShowDialog)
                    {
                        DeviceList = bleSearch.Devices;
                        try
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                            Console.Write(e.StackTrace);
                        }
                    }
                    bleSearch.StopDeviceSearch(ApplicationContext);
                }
                if (SelectDeviceNo > -1)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(DeviceList[SelectDeviceNo].Name, regStr))
                    {
                        // Find HVC device
                        return DeviceList[SelectDeviceNo];
                    }
                    SelectDeviceNo = -1;
                }
            }
            return DeviceList[SelectDeviceNo];
        }

        private class hvcCallback : HVCBleCallback
        {
            private readonly MainActivity outerInstance;

            public hvcCallback(MainActivity outerInstance)
            {
                this.outerInstance = outerInstance;
            }
            public override void OnConnected()
            {
                // トースト表示
                outerInstance.Context.Post((o) =>
                {
                    Toast.MakeText(outerInstance, "Selected device has connected", ToastLength.Short).Show();
                }, null);
            }

            public override void OnDisconnected()
            {
                // トースト表示
                outerInstance.Context.Post((o) =>
                {
                    Toast.MakeText(outerInstance, "Selected device has disconnected", ToastLength.Short).Show();
                }, null);
            }

            public override void OnPostSetParam(int nRet, byte outStatus)
            {
                // トースト表示
                outerInstance.Context.Post((o) =>
                {
                    Toast.MakeText(outerInstance, "Set parameters", ToastLength.Short).Show();
                }, null);
            }

            public override void OnPostGetParam(int nRet, byte outStatus)
            {
                // トースト表示
                outerInstance.Context.Post((o) =>
                {
                    Toast.MakeText(outerInstance, "Get parameters", ToastLength.Short).Show();
                }, null);
            }

            public override void OnPostExecute(int nRet, byte outStatus)
            {
                if (nRet != HVC.HVC_NORMAL || outStatus != 0)
                {
                    // Error processing
                }
                else
                {
                    string str = "Body Detect = " + string.Format("{0:D}\n", outerInstance.HvcRes.Body.Count());
                    foreach (HVC_RES.DetectionResult bodyResult in outerInstance.HvcRes.Body)
                    {
                        int size = bodyResult.Size;
                        int posX = bodyResult.PosX;
                        int posY = bodyResult.PosY;
                        int conf = bodyResult.Confidence;
                        str += string.Format("  [Body Detection] : size = {0:D}, x = {1:D}, y = {2:D}, conf = {3:D}\n", size, posX, posY, conf);
                    }
                    str += "Hand Detect = " + string.Format("{0:D}\n", outerInstance.HvcRes.Hand.Count());
                    foreach (HVC_RES.DetectionResult handResult in outerInstance.HvcRes.Hand)
                    {
                        int size = handResult.Size;
                        int posX = handResult.PosX;
                        int posY = handResult.PosY;
                        int conf = handResult.Confidence;
                        str += string.Format("  [Hand Detection] : size = {0:D}, x = {1:D}, y = {2:D}, conf = {3:D}\n", size, posX, posY, conf);
                    }
                    str += "Face Detect = " + string.Format("{0:D}\n", outerInstance.HvcRes.Face.Count());
                    foreach (HVC_RES.FaceResult faceResult in outerInstance.HvcRes.Face)
                    {
                        if ((outerInstance.HvcRes.ExecutedFunc & HVC.HVC_ACTIV_FACE_DETECTION) != 0)
                        {
                            int size = faceResult.Size;
                            int posX = faceResult.PosX;
                            int posY = faceResult.PosY;
                            int conf = faceResult.Confidence;
                            str += string.Format("  [Face Detection] : size = {0:D}, x = {1:D}, y = {2:D}, conf = {3:D}\n", size, posX, posY, conf);
                        }
                        if ((outerInstance.HvcRes.ExecutedFunc & HVC.HVC_ACTIV_FACE_DIRECTION) != 0)
                        {
                            str += string.Format("  [Face Direction] : yaw = {0:D}, pitchx = {1:D}, roll = {2:D}, conf = {3:D}\n", faceResult.Dir.Yaw, faceResult.Dir.Pitch, faceResult.Dir.Roll, faceResult.Dir.Confidence);
                        }
                        if ((outerInstance.HvcRes.ExecutedFunc & HVC.HVC_ACTIV_AGE_ESTIMATION) != 0)
                        {
                            str += string.Format("  [Age Estimation] : age = {0:D}, conf = {1:D}\n", faceResult.Age.Age, faceResult.Age.Confidence);
                        }
                        if ((outerInstance.HvcRes.ExecutedFunc & HVC.HVC_ACTIV_GENDER_ESTIMATION) != 0)
                        {
                            str += string.Format("  [Gender Estimation] : gender = {0}, confidence = {1:D}\n", faceResult.Gen.Gender == HVC.HVC_GEN_MALE ? "Male" : "Female", faceResult.Gen.Confidence);
                        }
                        if ((outerInstance.HvcRes.ExecutedFunc & HVC.HVC_ACTIV_GAZE_ESTIMATION) != 0)
                        {
                            str += string.Format("  [Gaze Estimation] : LR = {0:D}, UD = {1:D}\n", faceResult.Gaze.GazeLR, faceResult.Gaze.GazeUD);
                        }
                        if ((outerInstance.HvcRes.ExecutedFunc & HVC.HVC_ACTIV_BLINK_ESTIMATION) != 0)
                        {
                            str += string.Format("  [Blink Estimation] : ratioL = {0:D}, ratioR = {1:D}\n", faceResult.Blink.RatioL, faceResult.Blink.RatioR);
                        }
                        if ((outerInstance.HvcRes.ExecutedFunc & HVC.HVC_ACTIV_EXPRESSION_ESTIMATION) != 0)
                        {
                            str += string.Format("  [Expression Estimation] : expression = {0}, score = {1:D}, degree = {2:D}\n", faceResult.Exp.Expression == HVC.HVC_EX_NEUTRAL ? "Neutral" : faceResult.Exp.Expression == HVC.HVC_EX_HAPPINESS ? "Happiness" : faceResult.Exp.Expression == HVC.HVC_EX_SURPRISE ? "Supprise" : faceResult.Exp.Expression == HVC.HVC_EX_ANGER ? "Anger" : faceResult.Exp.Expression == HVC.HVC_EX_SADNESS ? "Sadness" : "", faceResult.Exp.Score, faceResult.Exp.Degree);
                        }
                    }
                    string viewText = str;
                    outerInstance.Context.Post((o) =>
                    {
                        outerInstance.StatusTextView.Text = viewText;
                    }, null);
                }
            }

            public override void OnPostGetDeviceName(byte[] value)
            {
                base.OnPostGetDeviceName(value);
            }

            public override void OnPostGetVersion(int nRet, byte outStatus)
            {
                base.OnPostGetVersion(nRet, outStatus);
            }
        };

        public class DeviceDialogFragment : DialogFragment, IDialogInterfaceOnClickListener
        {
            private static string[] DeviceNameList = null;
            private static ArrayAdapter<String> ListAdpString = null;
            private SynchronizationContext syncContext = SynchronizationContext.Current;
            private ListView BTList;
            private ImageView RefreshButton;

            public override Dialog OnCreateDialog(Bundle savedInstanceState)
            {
                // ダイアログを作って返す
                var builder = new AlertDialog.Builder(this.Activity);
                var content = this.Activity.LayoutInflater.Inflate(Resource.Layout.Devices, null);
                builder.SetView(content);
                this.BTList = (ListView)content.FindViewById(Resource.Id.devices);
                ListAdpString = new ArrayAdapter<string>(this.Activity, Android.Resource.Layout.SimpleListItemSingleChoice);
                this.BTList.Adapter = ListAdpString;
                this.BTList.ItemClick += (s, a) =>
                {
                    SelectDeviceNo = a.Position;
                    IsShowDialog = false;
                    Dismiss();
                };

                Task.Factory.StartNew(() =>
                    {
                    }).ContinueWith(t =>
                        {
                            while (true)
                            {
                                syncContext.Post(State =>
                                {
                                    RefreshList();
                                }, null);
                                try
                                {
                                    Thread.Sleep(1000);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.ToString());
                                    Console.Write(e.StackTrace);
                                }
                            }
                        }
                        );
                builder.SetNegativeButton("Cancel", this);
                return builder.Create(); ;
            }

            public void OnClick(IDialogInterface dialog, int which)
            {
            }

            private void RefreshList()
            {
                if (ListAdpString != null)
                {
                    ListAdpString.Clear();
                    if (DeviceList == null)
                    {
                        DeviceNameList = new String[] { "null" };
                    }
                    else
                    {
                        lock (DeviceList)
                        {
                            DeviceNameList = new String[DeviceList.Count];

                            int nIndex = 0;
                            foreach (BluetoothDevice device in DeviceList)
                            {
                                if (device.Name == null)
                                {
                                    DeviceNameList[nIndex] = "no name";
                                }
                                else
                                {
                                    DeviceNameList[nIndex] = device.Name;
                                }
                                nIndex++;
                            }
                        }
                    }
                    ListAdpString.AddAll(DeviceNameList.ToList());
                    ListAdpString.NotifyDataSetChanged();
                }
            }
        }

        public override void OnBackPressed()
        {
            var alertDialog = new AlertDialog.Builder(this);
            alertDialog.SetIcon(Resource.Drawable.Icon);
            alertDialog.SetTitle(Resource.String.PopupTitle);
            alertDialog.SetMessage(Resource.String.PopupMssage);
            alertDialog.SetPositiveButton(Resource.String.PopupYes, (sender, args) =>
            {
                // Positiveボタンをタップしたときに呼ばれる
            });
            //alertDialog.SetNegativeButton(Resource.String.PopupNo, (sender, args) =>
            //{
            //    // Negativeボタンをタップしたときに呼ばれる
            //});

            RunOnUiThread(() =>
            {
                alertDialog.Show();
            });
        }
    }
}