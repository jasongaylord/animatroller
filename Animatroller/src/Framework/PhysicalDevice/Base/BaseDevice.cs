﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace Animatroller.Framework.PhysicalDevice
{
    public abstract class BaseDevice : IOutputDevice
    {
        protected static Logger log = LogManager.GetCurrentClassLogger();
        private ILogicalDevice[] logicalDevices;

        public BaseDevice(params ILogicalDevice[] logicalDevice)
        {
            Executor.Current.Register(this);

            this.logicalDevices = logicalDevice;
        }

        public void StartDevice()
        {
        }
    }
}