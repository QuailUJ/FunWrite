using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DigiNoteSample;


namespace FunWrite
{
    public sealed class TabletDevice : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void PenCallback(int x, int y, int pressure);
        [DllImport("DigitNoteController.dll")]
        static extern void SetCallBackForRealTimePenDatas(PenCallback callback);
        [DllImport("DigitNoteController.dll")] static extern int openPort();
        [DllImport("DigitNoteController.dll")] static extern int switchToRealTimeMode();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly PenCallback callback;
        readonly TaskCompletionSource<Control> ready = new TaskCompletionSource<Control>(TaskCreationOptions.RunContinuationsAsynchronously);
        volatile bool disposed;
        bool connectionAttempted;
        int pressureMax = 2048;
        public event Action<Sample> SampleReceived;
        public TabletDevice()
        {
            callback = Receive;
            // Keep the vendor SDK on one STA with a message pump, as on the original
            // UI thread, while keeping connection work off the actual UI thread.
            var worker = new Thread(() =>
            {
                try
                {
                    using (var dispatcher = new Control())
                    {
                        dispatcher.CreateControl();
                        var handle = dispatcher.Handle;
                        ready.SetResult(dispatcher);
                        Application.Run();
                    }
                }
                catch (Exception ex) { ready.TrySetException(ex); }
            }) { IsBackground = true, Name = "FunWrite tablet" };
            worker.SetApartmentState(ApartmentState.STA); worker.Start();
        }

        public Task ConnectAsync() { return Dispatch(Connect); }

        async Task Dispatch(Action action)
        {
            var dispatcher = await ready.Task.ConfigureAwait(false);
            var complete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.BeginInvoke(new Action(() =>
            {
                try { action(); complete.SetResult(true); }
                catch (Exception ex) { complete.SetException(ex); }
            }));
            await complete.Task.ConfigureAwait(false);
        }

        void Connect()
        {
            if (disposed) throw new ObjectDisposedException("TabletDevice");
            connectionAttempted = true;
            DigiNoteSampleSDK.disconnectDevice();
            SetCallBackForRealTimePenDatas(callback);
            openPort();
            int mode = switchToRealTimeMode();
            int connected = DigiNoteSampleSDK.connectDevice();
            int length = 0, width = 0, maximum = 0;
            DigiNoteSampleSDK.getDeviceDataRange(ref length, ref width, ref maximum);
            if (disposed) { DigiNoteSampleSDK.disconnectDevice(); throw new ObjectDisposedException("TabletDevice"); }
            if (mode < 0 || connected < 0 || maximum <= 0) throw new InvalidOperationException("寫字板未連線，請檢查 USB／藍牙後重新連接。");
            pressureMax = maximum;
        }

        public static Sample Normalize(int x, int y, int pressure, int maximum, long time)
        {
            ushort value = pressure <= 0 ? (ushort)0 : (ushort)Math.Min(1024,
                Math.Max(1, Math.Round(1024.0 * pressure / Math.Max(1, maximum), MidpointRounding.AwayFromZero)));
            return new Sample(0, time, x - 3000, 25000 - (y - 2350), value);
        }

        void Receive(int x, int y, int pressure)
        {
            var handler = SampleReceived;
            if (!disposed && handler != null) handler(Normalize(x, y, pressure, pressureMax, clock.ElapsedMilliseconds));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SampleReceived = null;
            Dispatch(() =>
            {
                try { if (connectionAttempted) DigiNoteSampleSDK.disconnectDevice(); }
                finally { Application.ExitThread(); }
            }).ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
