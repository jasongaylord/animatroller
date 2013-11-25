﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using NLog;

namespace Animatroller.Framework.Expander
{
    public class Renard : IPort, IRunnable, IDmxOutput
    {
        protected static Logger log = LogManager.GetCurrentClassLogger();
        private int sendCounter;
        private SerialPort serialPort;
        private object lockObject = new object();
        private byte[] renardData;
        private int dataChanges;
        private Task senderTask;
        private System.Threading.CancellationTokenSource cancelSource;
        private System.Diagnostics.Stopwatch firstChange;

        public Renard(string portName)
        {
            this.serialPort = new SerialPort(portName, 57600);
            this.renardData = new byte[24];

            this.cancelSource = new System.Threading.CancellationTokenSource();
            this.firstChange = new System.Diagnostics.Stopwatch();

            this.senderTask = new Task(x =>
                {
                    while (!this.cancelSource.IsCancellationRequested)
                    {
                        bool sentChanges = false;

                        lock (lockObject)
                        {
                            if (this.dataChanges > 0)
                            {
                                this.firstChange.Stop();
                                //log.Info("Sending {0} changes to Renard. Oldest {1:N2}ms",
                                //    this.dataChanges, this.firstChange.Elapsed.TotalMilliseconds);
                                this.dataChanges = 0;
                                sentChanges = true;

                                SendSerialData(this.renardData);
                            }
                        }

                        if(!sentChanges)
                            System.Threading.Thread.Sleep(10);
                    }
                }, this.cancelSource.Token, TaskCreationOptions.LongRunning);

            Executor.Current.Register(this);
        }

        protected void SendSerialData(byte[] data)
        {
            if (data.Length > 600)
                throw new ArgumentOutOfRangeException("Max data size is 600 bytes");

            if (data.Length == 0)
                return;

            lock (lockObject)
            {
                sendCounter++;
                //log.Info("Sending packet {0} to Renard", sendCounter);

                try
                {
                    serialPort.Write(new byte[] { 0x7E, 0x80 }, 0, 2);
                    var outputBuffer = new byte[data.Length * 2];
                    int index = 0;
                    foreach (byte b in data)
                    {
                        switch(b)
                        {
                            case 0x7D:
                                outputBuffer[index++] = 0x7F;
                                outputBuffer[index++] = 0x2F;
                                break;

                            case 0x7E:
                                outputBuffer[index++] = 0x7F;
                                outputBuffer[index++] = 0x30;
                                break;

                            case 0x7F:
                                outputBuffer[index++] = 0x7F;
                                outputBuffer[index++] = 0x31;
                                break;
                                
                            default:
                                outputBuffer[index++] = b;
                                break;
                        }
                    }
                    serialPort.Write(outputBuffer, 0, index);
                }
                catch (Exception ex)
                {
                    log.Info("SendSerialCommand exception: " + ex.Message);
                    // Ignore
                }
            }
        }

        private void DataChanged()
        {
            lock (lockObject)
            {
                if (this.dataChanges++ == 0)
                {
                    this.firstChange.Restart();
                }
            }
        }

        public SendStatus SendDimmerValue(int channel, byte value)
        {
            return SendDimmerValues(channel, new byte[] { value }, 0, 1);
        }

        public SendStatus SendDimmerValues(int firstChannel, byte[] values)
        {
            return SendDimmerValues(firstChannel, values, 0, values.Length);
        }

        public SendStatus SendDimmerValues(int firstChannel, byte[] values, int offset, int length)
        {
            if (firstChannel < 1 || firstChannel + values.Length - 1 > 24)
                throw new ArgumentOutOfRangeException("Invalid first channel (1-24)");

            for(int i = 0; i < length; i++)
                this.renardData[firstChannel + i - 1] = values[offset + i];

            DataChanged();

            return SendStatus.NotSet;
        }

        public void Start()
        {
            serialPort.Open();

            this.senderTask.Start();
        }

        public void Run()
        {
        }

        public void Stop()
        {
            this.cancelSource.Cancel();
            serialPort.Close();
        }

        public Renard Connect(PhysicalDevice.INeedsDmxOutput device)
        {
            device.DmxOutputPort = this;

            return this;
        }
    }
}