using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Icklewik.Core.Model;

namespace Icklewik.Core
{
    public static class EventHelper
    {
        public static void SubscribeToWikiEvent<TEventArgs>(object target, string eventName, IScheduler scheduler, Action<TEventArgs> eventAction) where TEventArgs : WikiEventArgs
        {
            ObserveOnAndSubscribe(
                    Observable.FromEventPattern<TEventArgs>(target, eventName),
                    eventName,
                    scheduler,
                    eventAction,
                    args => args.Id);
        }

        public static void SubscribeToSampledEvent<TEventArgs>(object target, string eventName, IScheduler scheduler, Action<TEventArgs> eventAction, Func<TEventArgs, string> identityFunc, TimeSpan sampleTimeSpan) where TEventArgs : EventArgs
        {
            // TODO: Investigate
            // experimental method, tries to group identical events together so we don't have to worry about multiple firings
            // currently doesn't pass all unit tests so not used
            //ObserveOnAndSubscribe(
            //        Observable.FromEventPattern<TEventArgs>(target, eventName)
            //            .GroupBy(evt => identityFunc(evt.EventArgs))
            //            .SelectMany(grp => grp.Sample(sampleTimeSpan)),
            //        eventName,
            //        scheduler,
            //        eventAction,
            //        identityFunc);

            // use forward to standard method
            ObserveOnAndSubscribe(
                Observable.FromEventPattern<TEventArgs>(target, eventName),
                eventName,
                scheduler,
                eventAction,
                identityFunc);
        }

        private static void ObserveOnAndSubscribe<TEventArgs>(IObservable<EventPattern<TEventArgs>> observable, string eventName, IScheduler scheduler, Action<TEventArgs> eventAction, Func<TEventArgs, string> identityFunc) where TEventArgs : EventArgs
        {
            observable
                //.SubscribeOn(scheduler)
                .ObserveOn(scheduler)
                .Subscribe(evt =>
                {
                    System.Console.WriteLine("{0} Handling event ({1}:{2}) on thread: {3}", DateTime.Now.Ticks, eventName, identityFunc(evt.EventArgs), Thread.CurrentThread.Name);
                    eventAction(evt.EventArgs);
                });
        }
    }
}
