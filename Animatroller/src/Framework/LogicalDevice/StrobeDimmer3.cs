﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using Animatroller.Framework.Extensions;
using Animatroller.Framework.LogicalDevice.Event;

namespace Animatroller.Framework.LogicalDevice
{
    public class StrobeDimmer3 : Dimmer3, IReceivesStrobeSpeed
    {
        public StrobeDimmer3([System.Runtime.CompilerServices.CallerMemberName] string name = "")
            : base(name)
        {
            this.currentData[DataElements.StrobeSpeed] = 0.0;
        }

        public double StrobeSpeed
        {
            get { return (double)this.currentData[DataElements.StrobeSpeed]; }
        }

        public void SetStrobeSpeed(double strobeSpeed, IControlToken token)
        {
            PushData(token, Tuple.Create(DataElements.StrobeSpeed, (object)strobeSpeed));
        }
    }
}