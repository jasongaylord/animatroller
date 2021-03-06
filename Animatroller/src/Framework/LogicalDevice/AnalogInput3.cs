﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Animatroller.Framework.LogicalDevice.Event;
using System.Linq.Expressions;
using System.Reflection;

namespace Animatroller.Framework.LogicalDevice
{
    public class AnalogInput3 : BaseDevice, ISupportsPersistence, ILogicalOutputDevice<double>, IInputHardware
    {
        protected double currentValue;
        protected double defaultValue;
        protected ISubject<double> control;
        protected ISubject<double> outputValue;

        public AnalogInput3(bool persistState = false, double defaultValue = 0.0, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
            : base(name, persistState)
        {
            this.currentValue = defaultValue;
            this.defaultValue = defaultValue;
            this.outputValue = new Subject<double>();
            this.control = new Subject<double>();

            this.control.Subscribe(x =>
                {
                    if (this.currentValue != x)
                    {
                        this.currentValue = x;

                        UpdateOutput();
                    }
                });
        }

        public void SetValueFromPersistence(Func<string, string, string> getKeyFunc)
        {
            double value;
            double.TryParse(getKeyFunc("input", this.defaultValue.ToString()), out value);

            Value = value;
        }

        public void SaveValueToPersistence(Action<string, string> setKeyFunc)
        {
            setKeyFunc("input", this.currentValue.ToString());
        }

        public bool PersistState
        {
            get { return this.persistState; }
        }

        public ISubject<double> Control
        {
            get
            {
                return this.control;
            }
        }

        public IObservable<double> Output
        {
            get
            {
                return this.outputValue;
            }
        }

        public void ConnectTo(ISubject<double> component)
        {
            this.outputValue.Subscribe(component);
        }

        public void ConnectTo(Action<double> component)
        {
            this.outputValue.Subscribe(component);
        }

        public double Value
        {
            get { return this.currentValue; }
            set
            {
                this.currentValue = value;

                UpdateOutput();
            }
        }

        protected override void UpdateOutput()
        {
            this.outputValue.OnNext(this.currentValue);
        }

        public void WhenOutputChanges(Action<double> action)
        {
            Output.Subscribe(action);
        }
    }
}
