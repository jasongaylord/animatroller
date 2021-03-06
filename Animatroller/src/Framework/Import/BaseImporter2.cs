﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Serilog;
using Animatroller.Framework.Controller;
using System.Threading.Tasks;
using Animatroller.Framework.LogicalDevice;

namespace Animatroller.Framework.Import
{
    public abstract class BaseImporter2
    {
        protected class DeviceController : Controller.BaseDeviceController<IReceivesData>
        {
            public IPushDataController Observer { get; set; }

            public IData AdditionalData { get; set; }

            public DeviceController(IReceivesData device, IData additionalData)
                : base(device, 0)
            {
                AdditionalData = additionalData;
            }
        }

        public class ChannelData
        {
            public string Name { get; private set; }

            public bool Mapped { get; set; }

            public bool HasEffects { get; set; }

            public ChannelData(string name)
            {
                this.Name = name;
            }

            public void ChangeName(string newName)
            {
                Name = newName;
            }
        }

        /*        public class MappedDeviceDimmer
                {
                    public IReceivesBrightness Device { get; set; }

                    //public void RunEffect(Effect.IMasterBrightnessEffect effect, TimeSpan oneSweepDuration)
                    //{
                    //    this.Device.RunEffect(effect, oneSweepDuration);
                    //}

                    public MappedDeviceDimmer(IReceivesBrightness device)
                    {
                        this.Device = device;
                    }
                }*/
        /*
                public class MappedDeviceRGB
                {
                    public LogicalDevice.IHasColorControl Device { get; set; }

                    public MappedDeviceRGB(LogicalDevice.IHasColorControl device)
                    {
                        this.Device = device;
                    }
                }
        */
        protected ILogger log;
        protected Dictionary<IChannelIdentity, ChannelData> channelData;
        //        private List<IChannelIdentity> channels;
        protected Dictionary<IChannelIdentity, HashSet<DeviceController>> mappedDevices;
        protected Dictionary<RGBChannelIdentity, HashSet<DeviceController>> mappedRgbDevices;
        protected HashSet<IControlledDevice> controlledDevices;
        protected string name;
        protected int priority;

        public BaseImporter2(string name, int priority)
        {
            this.log = Log.Logger;
            this.name = name;
            this.priority = priority;

            this.channelData = new Dictionary<IChannelIdentity, ChannelData>();
            //            this.channels = new List<IChannelIdentity>();
            this.mappedDevices = new Dictionary<IChannelIdentity, HashSet<DeviceController>>();
            this.mappedRgbDevices = new Dictionary<RGBChannelIdentity, HashSet<DeviceController>>();
            this.controlledDevices = new HashSet<IControlledDevice>();
        }

        public abstract Task Start(long offsetMs, TimeSpan? duration = null);

        public abstract void Stop();

        //public IEnumerable<IChannelIdentity> GetChannels
        //{
        //    get
        //    {
        //        return this.channels;
        //    }
        //}

        public string GetChannelName(IChannelIdentity channelIdentity)
        {
            var channel = this.channelData[channelIdentity];

            return channel.Name;
        }

        public IChannelIdentity ChannelIdentityFromName(string name)
        {
            foreach (var kvp in this.channelData)
            {
                if (kvp.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return kvp.Key;
            }

            throw new KeyNotFoundException(string.Format("Channel {0} not found", name));
        }

        private bool ChannelNameExists(string channelName)
        {
            foreach (var kvp in this.channelData)
            {
                if (kvp.Value.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        protected void AddChannelData(IChannelIdentity channelIdentity, ChannelData data)
        {
            string originalName = data.Name;
            int suffixNumber = 0;
            while (ChannelNameExists(data.Name))
            {
                suffixNumber++;

                data.ChangeName(string.Format("{0} ({1})", originalName, suffixNumber));
            }

            if (this.channelData.ContainsKey(channelIdentity))
                this.log.Warning("Channel id {0} already exists", channelIdentity);

            this.channelData[channelIdentity] = data;
            //            this.channels.Add(channelIdentity);
        }

        protected void InternalMapDevice(IChannelIdentity channelIdentity, DeviceController device)
        {
            HashSet<DeviceController> devices;
            if (!mappedDevices.TryGetValue(channelIdentity, out devices))
            {
                devices = new HashSet<DeviceController>();
                mappedDevices[channelIdentity] = devices;
            }
            devices.Add(device);

            if (device is IControlledDevice)
                this.controlledDevices.Add((IControlledDevice)device);
            if (device is LogicalDevice.IHasControlledDevice)
                this.controlledDevices.Add(((LogicalDevice.IHasControlledDevice)device).ControlledDevice);

            var channelData = this.channelData[channelIdentity];
            channelData.Mapped = true;

            if (!channelData.HasEffects)
                this.log.Warning("Channel {0}/{1} is mapped, but has no effects", channelIdentity, channelData.Name);
        }

        protected void InternalMapDevice(RGBChannelIdentity channelIdentity, DeviceController device)
        {
            HashSet<DeviceController> devices;
            if (!mappedRgbDevices.TryGetValue(channelIdentity, out devices))
            {
                devices = new HashSet<DeviceController>();
                mappedRgbDevices[channelIdentity] = devices;
            }
            devices.Add(device);

            if (device is IControlledDevice)
                this.controlledDevices.Add((IControlledDevice)device);
            if (device is LogicalDevice.IHasControlledDevice)
                this.controlledDevices.Add(((LogicalDevice.IHasControlledDevice)device).ControlledDevice);

            var channelDataR = this.channelData[channelIdentity.R];
            var channelDataG = this.channelData[channelIdentity.G];
            var channelDataB = this.channelData[channelIdentity.B];
            channelDataR.Mapped = true;
            channelDataG.Mapped = true;
            channelDataB.Mapped = true;

            if (!channelDataR.HasEffects && !channelDataG.HasEffects && !channelDataB.HasEffects)
                this.log.Warning("Channel {0} is mapped, but has no effects", channelIdentity);
        }

        //public void MapDevice(IChannelIdentity channelIdentity, IReceivesBrightness device)
        //{
        //    InternalMapDevice(channelIdentity, device);
        //}

        //public T MapDevice<T>(IChannelIdentity channelIdentity, Func<string, T> logicalDevice) where T : LogicalDevice.IHasBrightnessControl
        //{
        //    string name = GetChannelName(channelIdentity);

        //    var device = logicalDevice.Invoke(name);

        //    MapDevice(channelIdentity, device);

        //    return device;
        //}
        /*
                public void MapDevice(
                    IChannelIdentity channelIdentityR,
                    IChannelIdentity channelIdentityG,
                    IChannelIdentity channelIdentityB,
                    LogicalDevice.IHasColorControl device)
                {
                    var mappedDevice = new MappedDeviceRGB(device);
                    InternalMapDevice(new RGBChannelIdentity(channelIdentityR, channelIdentityG, channelIdentityB), mappedDevice);

                    device.Color = Color.Black;
                }
        */
        /*        public TDevice MapDevice<TDevice>(
                    IChannelIdentity channelIdentityR,
                    IChannelIdentity channelIdentityG,
                    IChannelIdentity channelIdentityB,
                    Func<string, TDevice> logicalDevice) where TDevice : LogicalDevice.IHasColorControl
                {
                    string name = string.Format("{0}/{1}/{2}",
                        GetChannelName(channelIdentityR), GetChannelName(channelIdentityG), GetChannelName(channelIdentityB));

                    var device = logicalDevice.Invoke(name);

                    MapDevice(channelIdentityR, channelIdentityG, channelIdentityB, device);

                    return device;
                }*/
    }
}
