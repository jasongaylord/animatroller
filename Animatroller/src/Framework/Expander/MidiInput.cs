﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Animatroller.Framework.LogicalDevice;
using Animatroller.Framework.Extensions;
using NLog;
using Sanford.Multimedia.Midi;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Animatroller.Framework.Expander
{
    public class MidiInput : IPort, IRunnable
    {
        protected static Logger log = LogManager.GetCurrentClassLogger();
        private InputDevice inputDevice;
        private Dictionary<Tuple<int, ChannelCommand, int>, Action<ChannelMessage>> messageMapper;
        private ISubject<ChannelMessage> midiMessages;

        public MidiInput(int deviceId = 0)
        {
            if (InputDevice.DeviceCount == 0)
                throw new ArgumentException("No Midi device detected");

            this.messageMapper = new Dictionary<Tuple<int, ChannelCommand, int>, Action<ChannelMessage>>();

            this.midiMessages = new Subject<ChannelMessage>();

            this.inputDevice = new InputDevice(deviceId);
            this.inputDevice.ChannelMessageReceived += inputDevice_ChannelMessageReceived;

            Executor.Current.Register(this);

            this.midiMessages.Subscribe(x =>
                {
                    log.Trace("Recv midi cmd {0}, chn: {1}   data1: {2}   data2: {3}",
                        x.Command,
                        x.MidiChannel,
                        x.Data1,
                        x.Data2);
                });
        }

        public IObservable<ChannelMessage> MidiMessages
        {
            get
            {
                return this.midiMessages;
            }
        }

        private void inputDevice_ChannelMessageReceived(object sender, ChannelMessageEventArgs e)
        {
            log.Trace("Recv midi cmd {0}, chn: {1}   data1: {2}   data2: {3}",
                e.Message.Command,
                e.Message.MidiChannel,
                e.Message.Data1,
                e.Message.Data2);

            this.midiMessages.OnNext(e.Message);

            try
            {
                var key = Tuple.Create(e.Message.MidiChannel, e.Message.Command, e.Message.Data1);

                Action<ChannelMessage> action;
                if (this.messageMapper.TryGetValue(key, out action))
                {
                    action.Invoke(e.Message);
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to invoke action", ex);
            }
        }

        public void Start()
        {
            this.inputDevice.StartRecording();
        }

        public void Stop()
        {
            this.inputDevice.StopRecording();
            this.inputDevice.Close();
        }

        public IObservable<DoubleZeroToOne> SubscribeToController(int midiChannel, int controller)
        {
            return this.midiMessages
                .Where(x =>
                    x.MidiChannel == midiChannel &&
                    x.Command == ChannelCommand.Controller &&
                    x.Data1 == controller)
                .Select(x => new DoubleZeroToOne(x.Data2 / 127.0));
        }

        public IObservable<DoubleZeroToOne> Controller(int midiChannel, int controller)
        {
            return this.midiMessages
                .Where(x =>
                    x.MidiChannel == midiChannel &&
                    x.Command == ChannelCommand.Controller &&
                    x.Data1 == controller)
                .Select(x => new DoubleZeroToOne(x.Data2 / 127.0));
        }

        private void WireUpDevice_Note(Animatroller.Framework.PhysicalDevice.DigitalInput device, int midiChannel, int note)
        {
            this.messageMapper.Add(Tuple.Create(midiChannel, ChannelCommand.NoteOn, note), m =>
                {
                    device.Trigger(true);
                });

            this.messageMapper.Add(Tuple.Create(midiChannel, ChannelCommand.NoteOff, note), m =>
                {
                    device.Trigger(false);
                });
        }

        public Animatroller.Framework.PhysicalDevice.DigitalInput AddDigitalInput_Note(DigitalInput logicalDevice, int midiChannel, int note)
        {
            var device = new Animatroller.Framework.PhysicalDevice.DigitalInput();

            WireUpDevice_Note(device, midiChannel, note);

            device.Connect(logicalDevice);

            return device;
        }

        public Animatroller.Framework.PhysicalDevice.AnalogInput AddAnalogInput_Note(AnalogInput logicalDevice, int midiChannel, int note)
        {
            var device = new Animatroller.Framework.PhysicalDevice.AnalogInput();

            this.messageMapper.Add(Tuple.Create(midiChannel, ChannelCommand.NoteOn, note), m =>
            {
                device.Trigger(m.Data2 / 127.0);
            });

            this.messageMapper.Add(Tuple.Create(midiChannel, ChannelCommand.NoteOff, note), m =>
            {
                device.Trigger(0);
            });

            device.Connect(logicalDevice);

            return device;
        }

        public Animatroller.Framework.PhysicalDevice.AnalogInput AddAnalogInput_Controller(AnalogInput logicalDevice, int midiChannel, int controller)
        {
            var device = new Animatroller.Framework.PhysicalDevice.AnalogInput();

            this.messageMapper.Add(Tuple.Create(midiChannel, ChannelCommand.Controller, controller), m =>
            {
                device.Trigger(m.Data2 / 127.0);
            });

            device.Connect(logicalDevice);

            return device;
        }
    }
}
