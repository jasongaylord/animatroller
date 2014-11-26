﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Reactive;
using NLog;
using Animatroller.Framework.Effect;
using Animatroller.Framework.Extensions;
using System.Threading.Tasks;

namespace Animatroller.Framework.Effect2
{
    public class MasterFader
    {
        private TimerJobRunner timerJobRunner;

        public MasterFader(TimerJobRunner timerJobRunner)
        {
            this.timerJobRunner = timerJobRunner;
        }

        public MasterFader()
            : this(Executor.Current.TimerJobRunner)
        {
        }

        public Task Fade(IReceivesBrightness device, double startBrightness, double endBrightness, int durationMs, int priority = 1)
        {
            var taskSource = new TaskCompletionSource<bool>();

            IControlToken newToken = null;

            IControlToken control = Executor.Current.GetControlToken(device);

            if (control == null)
            {
                newToken =
                control = device.TakeControl(priority);

                Executor.Current.SetControlToken(device, control);
            }

            var deviceObserver = device.GetBrightnessObserver(control);

            double brightnessRange = endBrightness - startBrightness;

            var observer = Observer.Create<long>(
                onNext: currentElapsedMs =>
                {
                    double pos = (double)currentElapsedMs / (double)durationMs;

                    double brightness = startBrightness + (pos * brightnessRange);

                    deviceObserver.OnNext(brightness);
                },
                onCompleted: () =>
                {
                    if(newToken != null)
                    {
                        Executor.Current.RemoveControlToken(device);

                        newToken.Dispose();
                    }

                    taskSource.SetResult(true);
                });

            this.timerJobRunner.AddTimerJob(observer, durationMs);

            return taskSource.Task;
        }
    }
}
