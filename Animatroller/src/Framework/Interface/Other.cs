﻿using System;
using System.Collections.Generic;
using System.Drawing;

namespace Animatroller.Framework
{
    public interface IPushDataController
    {
        void PushData();

        IData Data { get; }

        void SetDataFromIData(IData source);
    }

    public interface IReceivesData : IOwnedDevice
    {
        IPushDataController GetDataObserver(IControlToken token);

        void PushOutput(IControlToken token);

        void BuildDefaultData(IData data);

        void SetData(IControlToken token, IData data);

        object GetCurrentData(DataElements dataElement);
    }

    public interface IReceivesThroughput : IReceivesData
    {
    }

    public interface IReceivesBrightness : IReceivesData
    {
    }

    public interface IReceivesStrobeSpeed : IReceivesData
    {
    }

    public interface IReceivesColor : IReceivesBrightness
    {
    }

    public interface IReceivesPanTilt : IReceivesData
    {
    }

    public interface ISendsData : ILogicalDevice
    {
        IObservable<IData> OutputData { get; }

        IObservable<IData> OutputChanged { get; }

        IData CurrentData { get; }
    }

    public interface IData : IDictionary<DataElements, object>
    {
        IControlToken CurrentToken { get; set; }

        string CreationId { get; }

        IData Copy();

        T? GetValue<T>(DataElements dataElement, T? defaultValue = null) where T : struct;
    }

    [Obsolete]
    public interface IOwner
    {
        string Name { get; }

        [Obsolete]
        int Priority { get; }
    }

    public interface IHasAnalogInput
    {

    }

    public interface IRunningDevice : IDevice
    {
        void SetInitialState();
    }

    public interface ILogicalDevice : IRunningDevice
    {
        void EnableOutput();
    }

    public interface IApiVersion3 : ILogicalDevice
    {
    }

    public interface IPhysicalDevice : IRunningDevice
    {
    }

    public interface IControlledDevice : ILogicalDevice
    {
        void Suspend();
        void Resume();
        void TurnOff();
    }
}
