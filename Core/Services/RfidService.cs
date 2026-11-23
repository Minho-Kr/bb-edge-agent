using System;
using System.Timers;
using ADRcpLib;
using ADSioLib;

namespace BBEdgeAgent.Services
{
    public class TagReadEventArgs : EventArgs
    {
        public string Epc     { get; }
        public int    Rssi    { get; }
        public int    Antenna { get; }
        public TagReadEventArgs(string epc, int rssi, int antenna)
        {
            Epc = epc; Rssi = rssi; Antenna = antenna;
        }
    }

    public class RfidService : IDisposable
    {
        private SioBase?  _sio;
        private RcpBase   _rcp = new RcpBase();
        private readonly Timer _scanTimer = new Timer(600) { AutoReset = true };

        public event EventHandler<TagReadEventArgs>? TagRead;
        public event EventHandler<string>?           StatusChanged;

        public bool IsConnected => _sio?.bConnected == true;

        private AppConfig _cfg;

        public RfidService(AppConfig cfg)
        {
            _cfg = cfg;
            _rcp.RxRspParsed  += OnRxParsed;
            _scanTimer.Elapsed += (_, __) => SendInventoryCmd();
        }

        public void UpdateConfig(AppConfig cfg) => _cfg = cfg;

        // ── Connection ───────────────────────────────────────────

        public void Connect()
        {
            Disconnect();
            _sio = new SioNet();
            _sio.onStatus   += OnStatus;
            _sio.onReceived += OnReceived;
            _sio.Connect(_cfg.ReaderIp, _cfg.ReaderPort);
        }

        public void Disconnect()
        {
            StopScan();
            if (_sio != null)
            {
                _sio.DisConnect();
                _sio.Dispose();
                _sio = null;
            }
        }

        // ── Scanning ─────────────────────────────────────────────

        public void StartScan() { if (IsConnected) _scanTimer.Start(); }
        public void StopScan()  { _scanTimer.Stop(); }

        private void SendInventoryCmd()
        {
            if (!IsConnected) { _scanTimer.Stop(); return; }
            try
            {
                var pkt = new ProtocolPacket(_rcp.Address, RcpBase.RCP_MM_READ_C_UII, RcpBase.RCP_MSG_CMD);
                _sio!.Send(pkt.ToArray());
            }
            catch { }
        }

        // ── Events ───────────────────────────────────────────────

        private void OnStatus(object sender, StatusEventArgs e)
        {
            switch ((StatusType)e.Status)
            {
                case StatusType.CONNECT_OK:
                    StatusChanged?.Invoke(this, "connected:" + e.Msg);
                    break;
                case StatusType.CONNECT_FAIL:
                    StatusChanged?.Invoke(this, "fail:" + e.Msg);
                    break;
                case StatusType.DISCONNECT_OK:
                case StatusType.DISCONNECT_EXCEPT:
                    StatusChanged?.Invoke(this, "disconnected");
                    _scanTimer.Stop();
                    break;
            }
        }

        private void OnReceived(object sender, ReceivedEventArgs e) =>
            _rcp.ReciveBytePkt(e.Data);

        private void OnRxParsed(object sender, ProtocolEventArgs e)
        {
            var pkt = e.Protocol;
            int rtn = pkt.Type & 0x7f;

            if (pkt.Code == RcpBase.RCP_MM_READ_C_UII &&
                (rtn == RcpBase.RCP_MSG_NOTI || rtn == RcpBase.RCP_MSG_AUTO || rtn == 2 || rtn == 5))
            {
                try
                {
                    int epcWords = (pkt.Payload[1] >> 3) & 0x1F;
                    int pcepclen = 2 + epcWords * 2;
                    var cp = new TagInfo
                    {
                        TagType = TagType.TYPE_6C,
                        Antenna = pkt.Payload[0] & 0x1f,
                        PCData  = TagInfo.GetData(pkt.Payload, 1, 2),
                        EPCData = TagInfo.GetData(pkt.Payload, 3, pcepclen - 2),
                    };
                    var rssiRaw = pkt.Payload[pkt.Length - 1];
                    int rssi    = (sbyte)rssiRaw - 20 - 3;

                    if (!string.IsNullOrEmpty(cp.EPCString))
                        TagRead?.Invoke(this, new TagReadEventArgs(cp.EPCString.ToUpperInvariant(), rssi, cp.Antenna));
                }
                catch { }
            }
        }

        public void Dispose()
        {
            _scanTimer.Stop();
            _scanTimer.Dispose();
            Disconnect();
        }
    }
}
