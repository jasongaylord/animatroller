﻿using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Serilog;
using Animatroller.Framework.Extensions;
using Animatroller.Framework.LogicalDevice.Event;

namespace Animatroller.Framework.LogicalDevice
{
    public class SerialDevice : SingleOwnerDevice, IReceivesData
    {
        public SerialDevice([System.Runtime.CompilerServices.CallerMemberName] string name = "")
            : base(name)
        {
        }

        public override void BuildDefaultData(IData data)
        {
        }
    }
}
