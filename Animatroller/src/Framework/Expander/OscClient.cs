﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Net;
using Serilog;
using Rug.Osc;

namespace Animatroller.Framework.Expander
{
    public class OscClient : IPort, IRunnable
    {
        protected ILogger log;
        private OscSender sender;
        private System.Net.IPAddress destination;
        private int destinationPort;
        private object lockObject = new object();

        public OscClient(string destination, int destinationPort)
            : this(IPAddress.Parse(destination), destinationPort)
        {
        }

        public OscClient(IPAddress destination, int destinationPort)
        {
            this.log = Log.Logger;
            this.destination = destination;
            this.destinationPort = destinationPort;

            this.sender = new OscSender(
                IPAddress.Any,
                0,
                destination,
                destinationPort,
                OscSocket.DefaultMulticastTimeToLive,
                OscSender.DefaultMessageBufferSize,
                OscSocket.DefaultPacketSize);

            this.sender.Connect();

            Executor.Current.Register(this);
        }

        public void Start()
        {
        }

        public void Stop()
        {
            this.sender.Close();
        }

        public OscClient Send(string address, params object[] data)
        {
            return Send(address, true, data);
        }

        public OscClient Send(string address, bool convertDoubleToFloat, params object[] data)
        {
            //            this.sender.WaitForAllMessagesToComplete();

            this.log.Information("Sending to {0}", address);

            if (data == null || data.Length == 0)
            {
                // Send empty message
                var oscMessage = new OscMessage(address);

                lock (lockObject)
                {
                    this.sender.Send(oscMessage);
                }
            }
            else
            {
                this.log.Information("   Data {0}", string.Join(" ", data));

                var sendData = new object[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    if (convertDoubleToFloat && data[i] is double)
                        sendData[i] = (float)((double)data[i]);
                    else
                        sendData[i] = data[i];
                }

                var oscMessage = new OscMessage(address, sendData);
                var oscPacket = new OscBundle(0, oscMessage);

                lock (lockObject)
                {
                    this.sender.Send(oscPacket);
                }
            }

            return this;
        }
    }
}
