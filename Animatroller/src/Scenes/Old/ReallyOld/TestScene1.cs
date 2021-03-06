﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Animatroller.Framework;
using Animatroller.Framework.Extensions;
using Expander = Animatroller.Framework.Expander;
using Controller = Animatroller.Framework.Controller;
using Animatroller.Framework.LogicalDevice;
using Effect = Animatroller.Framework.Effect;
using Physical = Animatroller.Framework.PhysicalDevice;

namespace Animatroller.Scenes
{
    internal class TestScene1 : BaseScene
    {
        private StrobeDimmer georgeStrobeLight;
        private StrobeColorDimmer spiderLight;
        private Dimmer skullsLight;
        private Dimmer cobWebLight;
        private Switch blinkyEyesLight;
        private StrobeColorDimmer rgbLightRight;
        private StrobeColorDimmer rgbLight3;
        private StrobeColorDimmer rgbLight4;
        private MotorWithFeedback georgeMotor;
        private Switch spiderLift;
        private DigitalInput pressureMat;
        private Effect.Pulsating pulsatingEffect;
        private Effect.Flicker flickerEffect;


        public TestScene1(IEnumerable<string> args)
        {
            georgeStrobeLight = new StrobeDimmer("George Strobe");
            spiderLight = new StrobeColorDimmer("Spider Light");
            skullsLight = new Dimmer("Skulls");
            cobWebLight = new Dimmer("Cob Web");
            blinkyEyesLight = new Switch("Blinky Eyes");
            rgbLightRight = new StrobeColorDimmer("Light Right");
            rgbLight3 = new StrobeColorDimmer("Light 3");
            rgbLight4 = new StrobeColorDimmer("Light 4");
            georgeMotor = new MotorWithFeedback("George Motor");
            spiderLift = new Switch("Spider Lift");
            pressureMat = new DigitalInput("Pressure Mat");
            pulsatingEffect = new Effect.Pulsating(S(1), 0.2, 0.7);
            flickerEffect = new Effect.Flicker(0.4, 0.6);
        }

        public void WireUp(Expander.IOExpander port)
        {
            port.Connect(new Physical.AmericanDJStrobe(georgeStrobeLight, 5));

            port.Connect(new Physical.RGBStrobe(spiderLight, 10));
            port.Connect(new Physical.GenericDimmer(skullsLight, 1));
            port.Connect(new Physical.GenericDimmer(cobWebLight, 3));
            port.Connect(new Physical.GenericDimmer(blinkyEyesLight, 4));
            port.Connect(new Physical.RGBStrobe(rgbLightRight, 20));
            port.Connect(new Physical.RGBStrobe(rgbLight3, 30));
            port.Connect(new Physical.RGBStrobe(rgbLight4, 40));

            port.Motor.Connect(georgeMotor);

            port.DigitalInputs[0].Connect(pressureMat);
            port.DigitalOutputs[0].Connect(spiderLift);
        }

        public void WireUp(Expander.DMXPro port)
        {
            port.Connect(new Physical.AmericanDJStrobe(georgeStrobeLight, 16));

            port.Connect(new Physical.RGBStrobe(spiderLight, 10));
            port.Connect(new Physical.GenericDimmer(skullsLight, 1));
            port.Connect(new Physical.GenericDimmer(cobWebLight, 3));
            port.Connect(new Physical.GenericDimmer(blinkyEyesLight, 4));
            port.Connect(new Physical.RGBStrobe(rgbLightRight, 20));
            port.Connect(new Physical.RGBStrobe(rgbLight3, 30));
            port.Connect(new Physical.RGBStrobe(rgbLight4, 40));
        }

        public override void Start()
        {
            pressureMat.ActiveChanged += (sender, e) =>
            {
                if (e.NewState)
                {
                    this.log.Information("Button press!");

                    pulsatingEffect.Stop();

                    spiderLift.SetPower(true);
                    georgeMotor.SetVector(1, 160, S(5));
                    georgeMotor.WaitForVectorReached();
                    this.log.Information("Motor done");
                    georgeMotor.SetVector(0.8, 0, S(5));
                    georgeMotor.WaitForVectorReached();
                    this.log.Information("Motor back");

                    pulsatingEffect.Start();
                    spiderLift.SetPower(false);
                }
            };

            spiderLight.SetColor(Color.Blue, 1);

            pulsatingEffect.AddDevice(spiderLight);

            flickerEffect.AddDevice(georgeStrobeLight);
        }

        public override void Run()
        {
        }

        public override void Stop()
        {
        }
    }
}
