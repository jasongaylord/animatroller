﻿using Expander = Animatroller.Framework.Expander;

namespace Animatroller.SceneRunner
{
    public interface ISceneRequiresRaspExpander1
    {
        void WireUp(Expander.Raspberry port);
    }

    public interface ISceneRequiresRaspExpander3
    {
        void WireUp1(Expander.Raspberry port);
        void WireUp2(Expander.Raspberry port);
        void WireUp3(Expander.Raspberry port);
    }

    public interface ISceneSupportsSimulator
    {
        void WireUp(Animatroller.Simulator.SimulatorForm sim);
    }

    public interface ISceneRequiresIOExpander
    {
        void WireUp(Expander.IOExpander port);
    }

    public interface ISceneRequiresDMXPro
    {
        void WireUp(Expander.DMXPro port);
    }

    public interface ISceneRequiresAcnStream
    {
        void WireUp(Expander.AcnStream port);
    }
}
