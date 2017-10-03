﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Serilog;

namespace Animatroller.Framework.LogicalDevice
{
    public class GroupDimmer : Group<IReceivesBrightness>, IReceivesBrightness
    {
        public GroupDimmer([System.Runtime.CompilerServices.CallerMemberName] string name = "")
            : base(name)
        {
        }

        public void SetBrightness(double brightness, IControlToken token = null)
        {
            if (token == null)
                token = this.internalLock;

            foreach (var member in AllMembers)
            {
                member.SetBrightness(brightness, token);
            }
        }

        public void SetBrightness(double brightness, IData additionalData, IControlToken token = null)
        {
            if (token == null)
                token = this.internalLock;

            foreach (var member in AllMembers)
            {
                var data = GetFrameBuffer(token, member);

                data[DataElements.Brightness] = brightness;
                if (additionalData != null)
                    foreach (var kvp in additionalData)
                        data[kvp.Key] = kvp.Value;

                member.PushOutput(token);
            }
        }

        //public void PushData(IControlToken token, IData data)
        //{
        //    lock (this.members)
        //    {
        //        foreach (var member in this.members)
        //        {
        //            member.PushData(token, data);
        //        }
        //    }
        //}

        public void BuildDefaultData(IData data)
        {
            // Do nothing
        }

        public double Brightness
        {
            get
            {
                return double.NaN;
            }
        }
    }
}
