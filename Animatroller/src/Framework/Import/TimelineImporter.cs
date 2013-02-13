﻿using System;
using System.Collections.Generic;
using System.Drawing;
using NLog;
using Animatroller.Framework.Controller;

namespace Animatroller.Framework.Import
{
    public abstract class TimelineImporter
    {
        public interface ISimpleInvokeEvent
        {
            void Invoke();
        }

        public class Timeline : Timeline<ISimpleInvokeEvent>
        {
            public Timeline(bool loop)
                : base(loop)
            {
            }
        }

        public class ChannelData
        {
            public string Name { get; private set; }
            public bool Mapped { get; set; }

            public ChannelData(string name)
            {
                this.Name = name;
            }
        }

        public class MappedDeviceDimmer
        {
            public LogicalDevice.IHasBrightnessControl Device { get; set; }

            public void RunEffect(Effect.IMasterBrightnessEffect effect, TimeSpan oneSweepDuration)
            {
                this.Device.RunEffect(effect, oneSweepDuration);
            }

            public MappedDeviceDimmer(LogicalDevice.IHasBrightnessControl device)
            {
                this.Device = device;
            }
        }

        public class MappedDeviceRGB
        {
            public LogicalDevice.IHasColorControl Device { get; set; }

            public MappedDeviceRGB(LogicalDevice.IHasColorControl device)
            {
                this.Device = device;
            }
        }

        protected static Logger log = LogManager.GetCurrentClassLogger();
        protected Dictionary<IChannelIdentity, ChannelData> channelData;
        protected Dictionary<IChannelIdentity, HashSet<MappedDeviceDimmer>> mappedDevices;
        protected Dictionary<RGBChannelIdentity, HashSet<MappedDeviceRGB>> mappedRGBDevices;

        public TimelineImporter()
        {
            this.channelData = new Dictionary<IChannelIdentity, ChannelData>();
            this.mappedDevices = new Dictionary<IChannelIdentity, HashSet<MappedDeviceDimmer>>();
            this.mappedRGBDevices = new Dictionary<RGBChannelIdentity, HashSet<MappedDeviceRGB>>();
        }

        public IEnumerable<IChannelIdentity> GetChannels
        {
            get
            {
                return this.channelData.Keys;
            }
        }

        public string GetChannelName(IChannelIdentity channelIdentity)
        {
            var channel = this.channelData[channelIdentity];

            return channel.Name;
        }

        protected void InternalMapDevice(IChannelIdentity channelIdentity, MappedDeviceDimmer device)
        {
            HashSet<MappedDeviceDimmer> devices;
            if (!mappedDevices.TryGetValue(channelIdentity, out devices))
            {
                devices = new HashSet<MappedDeviceDimmer>();
                mappedDevices[channelIdentity] = devices;
            }
            devices.Add(device);
         
            this.channelData[channelIdentity].Mapped = true;
        }

        protected void InternalMapDevice(RGBChannelIdentity channelIdentity, MappedDeviceRGB device)
        {
            HashSet<MappedDeviceRGB> devices;
            if (!mappedRGBDevices.TryGetValue(channelIdentity, out devices))
            {
                devices = new HashSet<MappedDeviceRGB>();
                mappedRGBDevices[channelIdentity] = devices;
            }
            devices.Add(device);

            this.channelData[channelIdentity.R].Mapped = true;
            this.channelData[channelIdentity.G].Mapped = true;
            this.channelData[channelIdentity.B].Mapped = true;
        }

        public void MapDevice(IChannelIdentity channelIdentity, LogicalDevice.IHasBrightnessControl device)
        {
            var mappedDevice = new MappedDeviceDimmer(device);
            InternalMapDevice(channelIdentity, mappedDevice);

            device.Brightness = 0.0;
        }

        public T MapDevice<T>(IChannelIdentity channelIdentity, Func<string, T> logicalDevice) where T : LogicalDevice.IHasBrightnessControl
        {
            string name = GetChannelName(channelIdentity);

            var device = logicalDevice.Invoke(name);

            MapDevice(channelIdentity, device);

            return device;
        }

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

        public T MapDevice<T>(
            IChannelIdentity channelIdentityR,
            IChannelIdentity channelIdentityG,
            IChannelIdentity channelIdentityB,
            Func<string, T> logicalDevice) where T : LogicalDevice.IHasColorControl
        {
            string name = string.Format("{0}/{1}/{2}",
                GetChannelName(channelIdentityR), GetChannelName(channelIdentityG), GetChannelName(channelIdentityB));

            var device = logicalDevice.Invoke(name);

            MapDevice(channelIdentityR, channelIdentityG, channelIdentityB, device);

            return device;
        }

        protected Timeline InternalCreateTimeline(bool loop)
        {
            foreach (var kvp in this.channelData)
            {
                if (!kvp.Value.Mapped)
                {
                    log.Warn("No devices mapped to {0}", kvp.Key);
                }
            }

            var timeline = new Timeline(loop);
            timeline.MultiTimelineTrigger += (sender, e) =>
                {
                    foreach (var invokeEvent in e.Code)
                        invokeEvent.Invoke();
                };

            return timeline;
        }

        public abstract Timeline CreateTimeline(bool loop);
    }

    public class SimpleDimmerEvent : TimelineImporter.ISimpleInvokeEvent
    {
        private IEnumerable<TimelineImporter.MappedDeviceDimmer> devices;
        private double brightness;

        public SimpleDimmerEvent(IEnumerable<TimelineImporter.MappedDeviceDimmer> devices, double brightness)
        {
            this.devices = devices;
            this.brightness = brightness;
        }

        public void Invoke()
        {
            foreach (var device in this.devices)
            {
                device.Device.Brightness = this.brightness;
            }
        }
    }

    public class SimpleColorEvent : TimelineImporter.ISimpleInvokeEvent
    {
        private IEnumerable<TimelineImporter.MappedDeviceRGB> devices;
        private Color color;

        public SimpleColorEvent(IEnumerable<TimelineImporter.MappedDeviceRGB> devices, Color color)
        {
            this.devices = devices;
            this.color = color;
        }

        public void Invoke()
        {
            foreach (var device in this.devices)
            {
                device.Device.Color = this.color;
            }
        }
    }
}
