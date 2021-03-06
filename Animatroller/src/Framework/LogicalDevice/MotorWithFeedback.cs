﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Animatroller.Framework.LogicalDevice.Event;
using Animatroller.Framework.Extensions;
using Serilog;

namespace Animatroller.Framework.LogicalDevice
{
    public class MotorWithFeedback : ILogicalDevice
    {
        protected ILogger log;

        public class MotorVector
        {
            public double Speed { get; private set; }
            public int Target { get; private set; }
            public TimeSpan Timeout { get; private set; }

            public MotorVector(double speed, int target, TimeSpan timeout)
            {
                this.Speed = speed.Limit(-1, 1);
                this.Target = target;
                this.Timeout = timeout;
            }
        }

        protected string name;
        private MotorVector vector;
        private ManualResetEvent movementDone;
        private bool failed;
        private DateTime? lastCommandSent;
        private bool disabled;

        public event EventHandler<MotorVectorChangedEventArgs> VectorChanged;

        public MotorWithFeedback(string name)
        {
            this.log = Log.Logger;
            this.name = name;
            Executor.Current.Register(this);

            this.vector = new MotorVector(0, 0, TimeSpan.FromMilliseconds(0));
            this.movementDone = new ManualResetEvent(true);
        }

        protected virtual void RaiseVectorChanged()
        {
            if (disabled)
            {
                this.movementDone.Set();
                return;
            }

            var handler = VectorChanged;
            if (handler != null)
                handler(this, new MotorVectorChangedEventArgs(this.vector));
        }

        public MotorVector Vector
        {
            get { return this.vector; }
            private set
            {
                this.vector = value;

                RaiseVectorChanged();
            }
        }

        public virtual MotorWithFeedback SetVector(double speed, int target, TimeSpan timeout)
        {
            if (!speed.WithinLimits(0, 1))
                throw new ArgumentOutOfRangeException("Speed");

            if (failed)
                throw new InvalidOperationException("Motor failed");

            if (!movementDone.WaitOne(0))
                throw new InvalidOperationException("Already moving");

            movementDone.Reset();
            lastCommandSent = DateTime.Now;
            this.Vector = new MotorVector(speed, target, timeout);

            return this;
        }

        internal void Trigger(int? newPos, bool failed)
        {
            this.failed = failed;
            movementDone.Set();
        }

        public void WaitForVectorReached()
        {
            if (failed)
                return;

            movementDone.WaitOne();
            if (lastCommandSent.HasValue)
            {
                TimeSpan duration = DateTime.Now - lastCommandSent.Value;
                this.log.Information("Last movement took {0:F1} s", duration.TotalSeconds);
            }
        }

        public void WaitForVectorReached(ISequenceInstance instance)
        {
            if (failed)
                return;

            WaitHandle.WaitAny(new WaitHandle[] { instance.CancelToken.WaitHandle, movementDone });
            instance.CancelToken.ThrowIfCancellationRequested();
                
            if (lastCommandSent.HasValue)
            {
                TimeSpan duration = DateTime.Now - lastCommandSent.Value;
                this.log.Information("Last movement took {0:F1} s", duration.TotalSeconds);
            }
        }

        public void SetInitialState()
        {
            RaiseVectorChanged();
        }

        public MotorWithFeedback SetDisabled(bool disabled)
        {
            this.disabled = disabled;

            return this;
        }

        public void EnableOutput()
        {
            throw new NotImplementedException();
        }

        public string Name
        {
            get { return this.name; }
        }
    }
}
