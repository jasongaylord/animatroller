﻿using System;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace Animatroller.Framework.LogicalDevice
{
    public abstract class SingleOwnerDevice : BaseDevice, IOwnedDevice
    {
        protected Stack<IControlToken> owners;
        protected IControlToken currentOwner;

        public SingleOwnerDevice(string name)
            : base(name)
        {
            this.owners = new Stack<IControlToken>();
        }

        public virtual IControlToken TakeControl(int priority = 1, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
        {
            lock (this)
            {
                if (this.currentOwner != null && priority <= this.currentOwner.Priority)
                    // Already owned (by us or someone else)
                    return ControlledDevice.Empty;

                var newOwner = new ControlledDevice(name, true, priority, () =>
                {
                    lock (this)
                    {
                        if (this.owners.Count > 0)
                        {
                            this.currentOwner = this.owners.Pop();
                        }
                        else
                            this.currentOwner = null;

                        Executor.Current.SetControlToken(this, this.currentOwner);
                    }
                });

                this.owners.Push(newOwner);

                this.currentOwner = newOwner;

                Executor.Current.SetControlToken(this, newOwner);

                return this.currentOwner;
            }
        }

        public bool HasControl(IControlToken checkOwner)
        {
            return this.currentOwner == null || checkOwner == this.currentOwner;
        }
    }
}
