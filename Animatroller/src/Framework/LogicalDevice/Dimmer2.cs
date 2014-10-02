﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NLog;
using Animatroller.Framework.Extensions;
using Animatroller.Framework.LogicalDevice.Event;

namespace Animatroller.Framework.LogicalDevice
{
    public class Dimmer2 : SingleOwnerDevice, IOutput, ILogicalDevice//, /*IHasBrightnessControl, *///, IHasAnalogInput
    {
        protected object lockObject = new object();
        protected double currentBrightness;
        protected Effect.MasterSweeper.Job effectJob;
        protected ISubject<DoubleZeroToOne> brightness;

        public Dimmer2(string name)
            : base(name)
        {
            this.brightness = new Subject<DoubleZeroToOne>();

            this.brightness.Subscribe(x =>
                {
                    if (this.currentBrightness != x.Value)
                    {
#if DEBUG
                        if (!x.IsValid())
                            throw new ArgumentOutOfRangeException("Value is out of range");
#endif
                        this.currentBrightness = x.Value.Limit(0, 1);

                        if (x.Value == 0)
                            // Reset owner
                            owner = null;
                    }
                });
        }

        public ISubject<DoubleZeroToOne> Brightness
        {
            get
            {
                return this.brightness;
            }
        }

        public virtual Dimmer2 SetBrightness(double value)
        {
            this.Brightness.OnNext(new DoubleZeroToOne(value));

            return this;
        }

        public virtual void SetBrightness(double value, IOwner owner)
        {
            if (value == 0)
                // Reset owner
                owner = null;

            if (this.owner != null && owner != this.owner)
            {
                if (owner != null)
                {
                    if (owner.Priority <= this.owner.Priority)
                        return;
                }
                else
                    return;
            }

            this.owner = owner;
            this.Brightness.OnNext(new DoubleZeroToOne { Value = value });
        }

        public virtual void TurnOff()
        {
            this.Brightness.OnNext(DoubleZeroToOne.Zero);
        }

        public virtual Effect.MasterSweeper.Job RunEffect(Effect.IMasterBrightnessEffect effect, TimeSpan oneSweepDuration)
        {
            var effectAction = effect.GetEffectAction(brightness =>
                {
                    this.SetBrightness(brightness, this);
                });

            lock (this.lockObject)
            {
                if (this.effectJob == null)
                {
                    // Create new
                    this.effectJob = Executor.Current.RegisterSweeperJob(effectAction, oneSweepDuration, effect.Iterations);
                }
                else
                {
                    this.effectJob.Reset(effectAction, oneSweepDuration, effect.Iterations);
                }
                this.effectJob.Restart();
            }

            return this.effectJob;
        }

        public void StopEffect()
        {
            if (this.effectJob != null)
                this.effectJob.Stop();
        }

        public override void StartDevice()
        {
            Brightness.OnNext(DoubleZeroToOne.Zero);
        }
    }
}
