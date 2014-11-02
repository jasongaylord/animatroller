﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reactive.Subjects;
using NLog;

namespace Animatroller.Framework.Effect
{
    public abstract class BaseSweeperEffect : IEffect
    {
        protected static Logger log = LogManager.GetCurrentClassLogger();
        protected int priority;
        protected string name;
        protected object lockObject = new object();
        protected Sweeper sweeper;
        protected List<IObserver<double>> devices;
        protected bool isRunning;
        protected ISubject<bool> inputRun;

        public BaseSweeperEffect(
            TimeSpan sweepDuration,
            int dataPoints,
            bool startRunning,
            [System.Runtime.CompilerServices.CallerMemberName] string name = "")
        {
            this.name = name;
            Executor.Current.Register(this);

            this.inputRun = new Subject<bool>();

            this.inputRun.Subscribe(x =>
            {
                if (this.isRunning != x)
                {
                    if (x)
                        Start();
                    else
                        Stop();
                }
            });

            this.devices = new List<IObserver<double>>();
            this.sweeper = new Sweeper(sweepDuration, dataPoints, startRunning);

            this.sweeper.RegisterJob((zeroToOne, negativeOneToOne, oneToZeroToOne, forced, totalTicks, final) =>
                {
                    bool isUnlocked = false;
                    if (forced)
                    {
                        Monitor.Enter(lockObject);
                        isUnlocked = true;
                    }
                    else
                        isUnlocked = Monitor.TryEnter(lockObject);

                    if (isUnlocked)
                    {
                        try
                        {
                            double value = GetValue(zeroToOne, negativeOneToOne, oneToZeroToOne, final);

                            SendOutput(value);
                        }
                        catch
                        {
                        }
                        finally
                        {
                            Monitor.Exit(lockObject);
                        }
                    }
                    else
                        log.Warn("Missed Job in BaseSweepEffect   Name: " + Name);
                });
        }

        // Generate sweeper with 50 ms interval
        public BaseSweeperEffect(TimeSpan sweepDuration, bool startRunning, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
            : this(
            sweepDuration,
            (int)(sweepDuration.TotalMilliseconds > 500 ? sweepDuration.TotalMilliseconds / 50 : sweepDuration.TotalMilliseconds / 25),
            startRunning,
            name)
        {
        }

        public BaseSweeperEffect AddDevice(Animatroller.Framework.LogicalDevice.IHasBrightnessControl device)
        {
            ConnectTo(System.Reactive.Observer.Create<double>(x =>
            {
                device.Brightness = x;
            }));

            return this;
        }

        public IObserver<bool> InputRun
        {
            get
            {
                return this.inputRun;
            }
        }

        protected abstract double GetValue(double zeroToOne, double negativeOneToOne, double oneToZeroToOne, bool final);

        protected void SendOutput(double value)
        {
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();

            var watches = new System.Diagnostics.Stopwatch[this.devices.Count];
            for (int i = 0; i < this.devices.Count; i++)
            {
                watches[i] = System.Diagnostics.Stopwatch.StartNew();

                this.devices[i].OnNext(value);

                watches[i].Stop();
            }
            totalWatch.Stop();

            if (watches.Any())
            {
                double max = watches.Select(x => x.ElapsedMilliseconds).Max();
                double avg = watches.Select(x => x.ElapsedMilliseconds).Average();

                if (totalWatch.ElapsedMilliseconds > 25)
                {
                    log.Info(string.Format("Devices {0}   Max: {1:N1}   Avg: {2:N1}   Total: {3:N1}",
                        this.devices.Count, max, avg, totalWatch.ElapsedMilliseconds));
                }
            }
        }

        public BaseSweeperEffect SetPriority(int priority)
        {
            this.priority = priority;

            return this;
        }

        public IEffect Start()
        {
            this.sweeper.Resume();
            this.isRunning = true;

            return this;
        }

        public IEffect Prime()
        {
            this.sweeper.Prime();

            return this;
        }

        public IEffect Restart()
        {
            this.sweeper.Reset();

            return this;
        }

        public IEffect Stop()
        {
            this.sweeper.Pause();

            this.sweeper.ForceValue(0, 0, 0, 0);
            this.isRunning = false;

            return this;
        }

        public string Name
        {
            get { return this.name; }
        }

        public int Priority
        {
            get { return this.priority; }
        }

        public BaseSweeperEffect ConnectTo(IObserver<double> device)
        {
            lock (lockObject)
            {
                this.devices.Add(device);
            }

            return this;
        }

        public BaseSweeperEffect ConnectTo(IObserver<DoubleZeroToOne> device)
        {
            lock (lockObject)
            {
                this.devices.Add(System.Reactive.Observer.Create<double>(x =>
                    {
                        device.OnNext(new DoubleZeroToOne(x));
                    }));
            }

            return this;
        }

        public BaseSweeperEffect Disconnect(ISubject<double> device)
        {
            lock (lockObject)
            {
                if (this.devices.Contains(device))
                    this.devices.Remove(device);
            }
            return this;
        }
    }
}