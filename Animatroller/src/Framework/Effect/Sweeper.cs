﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Animatroller.Framework.Effect
{
    public class Sweeper
    {
        protected ILogger log;

        private object lockObject = new object();
        private object lockJobs = new object();
        private Timer timer;
        private int index1;
        private int index2;
        private int index3;
        private int positions;
        private List<EffectAction.Action> jobs;
        private TimeSpan interval;
        private bool oneShot;
        private bool ended;
        private int iterations;
        private int hitCounter;
        private long ticks;

        public Sweeper(TimeSpan duration, int dataPoints, bool startRunning)
        {
            if (dataPoints < 2)
                throw new ArgumentOutOfRangeException("dataPoints");

            this.log = Log.Logger;
            this.positions = dataPoints;
            InternalReset();
            this.jobs = new List<EffectAction.Action>();
            this.timer = new Timer(new TimerCallback(TimerCallback));
            this.ended = false;

            this.interval = new TimeSpan(duration.Ticks / dataPoints);
            log.Debug("Interval {0:N1} ms", this.interval.TotalMilliseconds);

            if (startRunning)
                Resume();
        }

        public Sweeper OneShot()
        {
            this.oneShot = true;

            return this;
        }

        public Action<int> NewIterationAction { get; set; }

        public Sweeper Pause()
        {
            this.timer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            return this;
        }

        public Sweeper Resume()
        {
            this.timer.Change(TimeSpan.FromMilliseconds(0), this.interval);

            return this;
        }

        private void InternalReset()
        {
            this.hitCounter = 0;
            this.index1 = 0;
            this.index2 = positions / 4;
            this.index3 = positions / 2;
            this.ticks = 0;
            this.ended = false;
        }

        public Sweeper Prime()
        {
            Pause();

            InternalReset();

            UpdateOutput();

            return this;
        }

        public Sweeper Reset()
        {
            Pause();

            InternalReset();

            Resume();

            return this;
        }

        public Sweeper ForceValue(double zeroToOne, double negativeOneToOne, double zeroToOneToZero, long totalTicks)
        {
            lock (lockJobs)
            {
                foreach (var job in jobs)
                    job(zeroToOne, negativeOneToOne, zeroToOneToZero, true, totalTicks, true);
            }

            return this;
        }

        public Sweeper RegisterJob(EffectAction.Action job)
        {
            lock (lockJobs)
            {
                jobs.Add(job);
            }

            return this;
        }

        private void TimerCallback(object state)
        {
            if (this.ended)
            {
                Pause();

                ForceValue(1.0, 1.0, 1.0, ticks);

                this.ended = false;
                return;
            }

            UpdateOutput();

            lock (lockObject)
            {
                if (++this.index1 >= this.positions)
                    this.index1 = 0;
                if (++this.index2 >= this.positions)
                    this.index2 = 0;
                if (++this.index3 >= this.positions)
                    this.index3 = 0;

                this.ticks++;

                if (++this.hitCounter >= this.positions)
                {
                    // Next iteration
                    this.iterations++;
                    this.hitCounter = 0;

                    if (this.oneShot)
                        this.ended = true;

                    NewIterationAction?.Invoke(this.iterations);
                }
            }
        }

        private void UpdateOutput()
        {
            double value1;
            double value2;
            double value3;
            long valueTicks;

            lock (lockObject)
            {
                value1 = SweeperTables.DataValues1[SweeperTables.GetScaledIndex(this.index1, this.positions + 1)];
                value2 = SweeperTables.DataValues2[SweeperTables.GetScaledIndex(this.index2, this.positions + 1)];
                value3 = SweeperTables.DataValues3[SweeperTables.GetScaledIndex(this.index3, this.positions + 1)];

                valueTicks = this.ticks;
            }

            if (Monitor.TryEnter(lockJobs))
            {
                try
                {
                    foreach (var job in jobs)
                        job(
                            zeroToOne: value1,
                            negativeOneToOne: value2,
                            zeroToOneToZero: value3,
                            forced: false,
                            totalTicks: valueTicks,
                            final: this.ended);
                }
                catch (Exception ex)
                {
                    log.Error("Exception in Sweeper job" + ex.ToString());
                }
                finally
                {
                    Monitor.Exit(lockJobs);
                }
            }
            else
                this.log.Warning("Missed execute task in Sweeper job");
        }
    }
}
