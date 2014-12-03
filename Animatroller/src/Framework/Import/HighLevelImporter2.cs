﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Linq;
using System.Drawing;
using NLog;
using LMS = Animatroller.Framework.Import.FileFormat.LightORama.LMS;
using Animatroller.Framework.Extensions;
using Animatroller.Framework.Controller;
using System.Threading.Tasks;
using Animatroller.Framework.LogicalDevice;

namespace Animatroller.Framework.Import
{
    public class HighLevelImporter2 : BaseImporter2
    {
        protected Timeline2<ChannelEffectInstance> timeline;
        protected Dictionary<IChannelIdentity, IList<ChannelEffect>> channelEffectsPerChannel;
        private bool prepared;
        private Dictionary<IOwnedDevice, IDisposableObserver<double>> brightnessObservers;
        private Dictionary<IOwnedDevice, ControlledObserverRGB> rgbObservers;

        public HighLevelImporter2()
        {
            this.channelEffectsPerChannel = new Dictionary<IChannelIdentity, IList<ChannelEffect>>();
            this.timeline = new Timeline2<ChannelEffectInstance>(iterations: 1);
            this.brightnessObservers = new Dictionary<IOwnedDevice, IDisposableObserver<double>>();
            this.rgbObservers = new Dictionary<IOwnedDevice, ControlledObserverRGB>();
        }

        protected void WireUpTimeline(Action exec)
        {
            foreach (var kvp in this.channelData)
            {
                if (!kvp.Value.Mapped)
                {
                    log.Warn("No devices mapped to {0}", kvp.Key);
                }
            }

            timeline.TearDown(() =>
            {
                foreach (var controlledDevice in this.controlledDevices)
                    controlledDevice.TurnOff();
            });

            timeline.MultiTimelineTrigger += (sender, e) =>
            {
                foreach (var controlledDevice in this.controlledDevices)
                    controlledDevice.Suspend();
                try
                {
                    exec();
                }
                finally
                {
                    foreach (var controlledDevice in this.controlledDevices)
                        controlledDevice.Resume();
                }
            };
        }

        private void AddEffectData(IChannelIdentity channelIdentity, IEnumerable<IOwnedDevice> devices, ChannelEffectInstance.DeviceType deviceType)
        {
            foreach (var effectData in channelEffectsPerChannel[channelIdentity])
            {
                var effectInstance = new ChannelEffectInstance
                {
                    Devices = devices,
                    Effect = effectData,
                    Type = deviceType
                };

                timeline.AddMs(effectData.StartMs, effectInstance);
            }
        }

        protected void PopulateTimeline()
        {
            foreach (var kvp in this.channelData)
            {
                if (!kvp.Value.Mapped && kvp.Value.HasEffects)
                {
                    log.Warn("No devices mapped to {0} ({1})", kvp.Key, kvp.Value.Name);
                }
            }

            foreach (var kvp in this.mappedDevices)
            {
                AddEffectData(kvp.Key, kvp.Value, ChannelEffectInstance.DeviceType.Brightness);
            }

            foreach (var kvp in this.mappedRgbDevices)
            {
                var id = kvp.Key;

                AddEffectData(id.R, kvp.Value, ChannelEffectInstance.DeviceType.ColorR);
                AddEffectData(id.G, kvp.Value, ChannelEffectInstance.DeviceType.ColorG);
                AddEffectData(id.B, kvp.Value, ChannelEffectInstance.DeviceType.ColorB);
            }

            timeline.Setup(() =>
                {
                    foreach (var device in this.mappedDevices.SelectMany(x => x.Value))
                    {
                        if (!this.brightnessObservers.ContainsKey(device))
                        {
                            var observer = device.GetBrightnessObserver();

                            this.brightnessObservers.Add(device, observer);
                        }
                    }

                    foreach (var device in this.mappedRgbDevices.SelectMany(x => x.Value))
                    {
                        if (!this.rgbObservers.ContainsKey(device))
                        {
                            var observer = device.GetRgbObsserver();

                            this.rgbObservers.Add(device, observer);
                        }
                    }
                });

            timeline.TearDown(() =>
                {
                    foreach (var controlledDevice in this.controlledDevices)
                        controlledDevice.TurnOff();

                    // Release locks
                    foreach (var observer in this.brightnessObservers.Values)
                        observer.Dispose();
                    this.brightnessObservers.Clear();

                    foreach (var observer in this.rgbObservers.Values)
                        observer.Dispose();
                    this.rgbObservers.Clear();
                });

            timeline.MultiTimelineTrigger += (sender, e) =>
            {
                foreach (var controlledDevice in this.controlledDevices)
                    controlledDevice.Suspend();

                try
                {
                    foreach (var effectInstance in e.Code)
                    {
                        if (effectInstance.Type == ChannelEffectInstance.DeviceType.Brightness)
                        {
                            foreach (var device in effectInstance.Devices)
                            {
                                IDisposableObserver<double> observer;
                                if (!this.brightnessObservers.TryGetValue(device, out observer))
                                    // Why no lock?
                                    continue;

                                effectInstance.Effect.Execute(observer);
                            }
                        }
                        else if (effectInstance.Type == ChannelEffectInstance.DeviceType.ColorR)
                        {
                            foreach (var device in effectInstance.Devices)
                            {
                                ControlledObserverRGB observer;
                                if (!this.rgbObservers.TryGetValue(device, out observer))
                                    // Why no lock?
                                    continue;

                                effectInstance.Effect.Execute(observer.R);
                            }
                        }
                        else if (effectInstance.Type == ChannelEffectInstance.DeviceType.ColorG)
                        {
                            foreach (var device in effectInstance.Devices)
                            {
                                ControlledObserverRGB observer;
                                if (!this.rgbObservers.TryGetValue(device, out observer))
                                    // Why no lock?
                                    continue;

                                effectInstance.Effect.Execute(observer.G);
                            }
                        }
                        else if (effectInstance.Type == ChannelEffectInstance.DeviceType.ColorB)
                        {
                            foreach (var device in effectInstance.Devices)
                            {
                                ControlledObserverRGB observer;
                                if (!this.rgbObservers.TryGetValue(device, out observer))
                                    // Why no lock?
                                    continue;

                                effectInstance.Effect.Execute(observer.B);
                            }
                        }
                    }
                }
                finally
                {
                    foreach (var controlledDevice in this.controlledDevices)
                        controlledDevice.Resume();
                }
            };
        }

        public void Prepare()
        {
            if (this.prepared)
                return;

            this.prepared = true;

            PopulateTimeline();
        }

        public void Dump()
        {
            log.Info("Used channels:");

            int count = 0;
            foreach (var kvp in this.channelData.Where(x => x.Value.HasEffects).OrderBy(x => x.Key))
            {
                count++;

                log.Info("Channel {0} - {1}", kvp.Key, kvp.Value.Name);
            }

            log.Info("Total used channels: {0}", count);
        }

        public void MapDevice(string channelName, IReceivesBrightness device)
        {
            var id = ChannelIdentityFromName(channelName);

            InternalMapDevice(id, device);
        }

        public void MapDeviceRGB(string channelNameR, string channelNameG, string channelNameB, IReceivesColor device)
        {
            var id = new RGBChannelIdentity(
                ChannelIdentityFromName(channelNameR),
                ChannelIdentityFromName(channelNameG),
                ChannelIdentityFromName(channelNameB));

            InternalMapDevice(id, device);
        }

        public void MapDeviceRGBW(string channelNameR, string channelNameG, string channelNameB, string channelNameW, IReceivesColor device)
        {
            // Currently not used
            var idW = ChannelIdentityFromName(channelNameW);

            var id = new RGBChannelIdentity(
                ChannelIdentityFromName(channelNameR),
                ChannelIdentityFromName(channelNameG),
                ChannelIdentityFromName(channelNameB));

            InternalMapDevice(id, device);
        }

        protected abstract class ChannelEffect
        {
            public int StartMs { get; set; }

            public abstract void Execute(IObserver<double> device);
        }

        protected abstract class ChannelEffectRange : ChannelEffect
        {
            public int EndMs { get; set; }

            public int DurationMs
            {
                get { return EndMs - StartMs; }
            }
        }

        protected class ChannelEffectInstance
        {
            public enum DeviceType
            {
                Brightness,
                ColorR,
                ColorG,
                ColorB
            }

            public IEnumerable<IOwnedDevice> Devices { get; set; }

            public ChannelEffect Effect { get; set; }

            public DeviceType Type { get; set; }
        }

        public override Task Start()
        {
            Prepare();

            return this.timeline.Start();
        }

        public override void Stop()
        {
            this.timeline.Stop();
        }
    }
}
