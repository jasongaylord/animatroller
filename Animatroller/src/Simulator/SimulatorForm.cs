﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Animatroller.Framework;
using Animatroller.Framework.LogicalDevice;
using Animatroller.Simulator.Extensions;

namespace Animatroller.Simulator
{
    public partial class SimulatorForm : Form, IPort
    {
        public SimulatorForm()
        {
            InitializeComponent();
        }

        public Control.StrobeBulb AddNewLight(string name)
        {
            var moduleControl = new Control.ModuleControl();
            moduleControl.Text = name;
            moduleControl.Size = new System.Drawing.Size(80, 80);

            var control = new Control.StrobeBulb();
            moduleControl.ChildControl = control;
            control.Color = Color.Black;

            flowLayoutPanelLights.Controls.Add(moduleControl);

            return control;
        }

        public Control.RopeLight AddNewRope(string name, int pixels)
        {
            var moduleControl = new Control.ModuleControl();
            moduleControl.Text = name;
            moduleControl.Size = new System.Drawing.Size(4 * pixels, 50);

            var control = new Control.RopeLight();
            moduleControl.ChildControl = control;
            control.Pixels = pixels;

            flowLayoutPanelLights.Controls.Add(moduleControl);

            return control;
        }

        public Animatroller.Framework.PhysicalDevice.MotorWithFeedback AddMotor(string name)
        {
            var moduleControl = new Control.ModuleControl();
            moduleControl.Text = name;
            moduleControl.Size = new System.Drawing.Size(160, 80);

            var control = new Control.Motor();
            moduleControl.ChildControl = control;

            flowLayoutPanelLights.Controls.Add(moduleControl);

            var device = new Animatroller.Framework.PhysicalDevice.MotorWithFeedback((target, speed, timeout) =>
            {
                control.Target = target;
                control.Speed = speed;
                control.Timeout = timeout;
            });

            control.Trigger = device.Trigger;

            return device;
        }

        public Animatroller.Framework.PhysicalDevice.DigitalOutput AddDigitalOutput(string name)
        {
            var moduleControl = new Control.ModuleControl();
            moduleControl.Text = name;
            moduleControl.Size = new System.Drawing.Size(80, 80);

            var centerControl = new Control.CenterControl();
            moduleControl.ChildControl = centerControl;

            var control = new Animatroller.Simulator.Control.Bulb.LedBulb();
            control.On = false;
            control.Size = new System.Drawing.Size(20, 20);
            centerControl.ChildControl = control;

            flowLayoutPanelLights.Controls.Add(moduleControl);

            var device = new Animatroller.Framework.PhysicalDevice.DigitalOutput(x =>
            {
                this.UIThread(delegate
                {
                    control.On = x;
                });
            });

            return device;
        }

        public Animatroller.Framework.PhysicalDevice.DigitalInput AddDigitalInput_FlipFlop(string name)
        {
            var control = new CheckBox();
            control.Text = name;
            control.Size = new System.Drawing.Size(80, 80);

            flowLayoutPanelLights.Controls.Add(control);

            var device = new Animatroller.Framework.PhysicalDevice.DigitalInput();

            control.CheckedChanged += (sender, e) =>
                {
                    device.Trigger((sender as CheckBox).Checked);
                };

            return device;
        }

        public Animatroller.Framework.PhysicalDevice.DigitalInput AddDigitalInput_Momentarily(string name)
        {
            var control = new Button();
            control.Text = name;
            control.UseMnemonic = false;
            control.Size = new System.Drawing.Size(80, 80);

            flowLayoutPanelLights.Controls.Add(control);

            var device = new Animatroller.Framework.PhysicalDevice.DigitalInput();

            control.MouseDown += (sender, e) =>
                {
                    device.Trigger(true);
                };

            control.MouseUp += (sender, e) =>
            {
                device.Trigger(false);
            };

            return device;
        }

        public void Connect(INeedsLabelLight output, string labelName)
        {
            output.LabelLightControl = AddNewLight(labelName);
        }

        public void Connect(INeedsRopeLight output, string labelName)
        {
            output.RopeLightControl = AddNewRope(labelName, output.Pixels);
        }
    }
}
