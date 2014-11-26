﻿﻿using System;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace Animatroller.Framework
{
    public class ControlSubject<T, TControl> : ControlSubject<T> where TControl : class
    {
        private Func<TControl, bool> controlChecker;

        public ControlSubject(T initialValue, Func<TControl, bool> controlChecker)
            : base(initialValue)
        {
            this.controlChecker = controlChecker;
        }

        /// <summary>
        /// Notifies all subscribed observers about the arrival of the specified element in the sequence.
        /// </summary>
        /// <param name="value">The value to send to all observers.</param>
        public void OnNext(T value, TControl controlValue)
        {
            if (this.controlChecker != null && !this.controlChecker(controlValue))
                // Skip
                return;

            var os = default(IObserver<T>[]);
            lock (_gate)
            {
                CheckDisposed();

                if (!_isStopped)
                {
                    _value = value;
                    os = new IObserver<T>[_observers.Count];
                    _observers.CopyTo(os);
                }
            }

            if (os != null)
            {
                foreach (var o in os)
                    o.OnNext(value);
            }
        }
    }

    /// <summary>
    /// Represents a value that changes over time.
    /// Observers can subscribe to the subject to receive the last (or initial) value and all subsequent notifications.
    /// </summary>
    /// <typeparam name="T">The type of the elements processed by the subject.</typeparam>
    public class ControlSubject<T> : ISubject<T>, IDisposable
    {
        /// <summary>
        /// Gets the current value or throws an exception.
        /// </summary>
        /// <value>The initial value passed to the constructor until <see cref="OnNext"/> is called; after which, the last value passed to <see cref="OnNext"/>.</value>
        /// <remarks>
        /// <para><see cref="Value"/> is frozen after <see cref="OnCompleted"/> is called.</para>
        /// <para>After <see cref="OnError"/> is called, <see cref="Value"/> always throws the specified exception.</para>
        /// <para>An exception is always thrown after <see cref="Dispose"/> is called.</para>
        /// <alert type="caller">
        /// Reading <see cref="Value"/> is a thread-safe operation, though there's a potential race condition when <see cref="OnNext"/> or <see cref="OnError"/> are being invoked concurrently.
        /// In some cases, it may be necessary for a caller to use external synchronization to avoid race conditions.
        /// </alert>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Dispose was called.</exception>
        public T Value
        {
            get
            {
                lock (_gate)
                {
                    CheckDisposed();

                    if (_exception != null)
                    {
                        throw _exception;
                    }

                    return _value;
                }
            }
        }

        protected readonly object _gate = new object();

        protected ImmutableList<IObserver<T>> _observers;
        protected bool _isStopped;
        protected T _value;
        private Exception _exception;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="System.Reactive.Subjects.BehaviorSubject&lt;T&gt;"/> class which creates a subject that caches its last value and starts with the specified value.
        /// </summary>
        /// <param name="initialValue">Initial value sent to observers when no other value has been received by the subject yet.</param>
        public ControlSubject(T initialValue)
        {
            _value = initialValue;
            _observers = ImmutableList<IObserver<T>>.Empty;
        }

        /// <summary>
        /// Indicates whether the subject has observers subscribed to it.
        /// </summary>
        public bool HasObservers
        {
            get
            {
                var observers = _observers;
                return observers != null && observers.Count > 0;
            }
        }

        /// <summary>
        /// Notifies all subscribed observers about the end of the sequence.
        /// </summary>
        public void OnCompleted()
        {
            var os = default(IObserver<T>[]);
            lock (_gate)
            {
                CheckDisposed();

                if (!_isStopped)
                {
                    os = new IObserver<T>[_observers.Count];
                    _observers.CopyTo(os);
                    _observers = ImmutableList<IObserver<T>>.Empty;
                    _isStopped = true;
                }
            }

            if (os != null)
            {
                foreach (var o in os)
                    o.OnCompleted();
            }
        }

        /// <summary>
        /// Notifies all subscribed observers about the exception.
        /// </summary>
        /// <param name="error">The exception to send to all observers.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
        public void OnError(Exception error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            var os = default(IObserver<T>[]);
            lock (_gate)
            {
                CheckDisposed();

                if (!_isStopped)
                {
                    os = new IObserver<T>[_observers.Count];
                    _observers.CopyTo(os);
                    _observers = ImmutableList<IObserver<T>>.Empty;
                    _isStopped = true;
                    _exception = error;
                }
            }

            if (os != null)
                foreach (var o in os)
                    o.OnError(error);
        }

        /// <summary>
        /// Notifies all subscribed observers about the arrival of the specified element in the sequence.
        /// </summary>
        /// <param name="value">The value to send to all observers.</param>
        public void OnNext(T value)
        {
            var os = default(IObserver<T>[]);
            lock (_gate)
            {
                CheckDisposed();

                if (!_isStopped)
                {
                    _value = value;
                    os = new IObserver<T>[_observers.Count];
                    _observers.CopyTo(os);
                }
            }

            if (os != null)
            {
                foreach (var o in os)
                    o.OnNext(value);
            }
        }

        /// <summary>
        /// Subscribes an observer to the subject.
        /// </summary>
        /// <param name="observer">Observer to subscribe to the subject.</param>
        /// <returns>Disposable object that can be used to unsubscribe the observer from the subject.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="observer"/> is null.</exception>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (observer == null)
                throw new ArgumentNullException("observer");

            var ex = default(Exception);

            lock (_gate)
            {
                CheckDisposed();

                if (!_isStopped)
                {
                    _observers = _observers.Add(observer);

                    // Don't send the default value
                    //                    observer.OnNext(_value);
                    return new Subscription(this, observer);
                }

                ex = _exception;
            }

            if (ex != null)
                observer.OnError(ex);
            else
                observer.OnCompleted();

            return Disposable.Empty;
        }

        class Subscription : IDisposable
        {
            private readonly ControlSubject<T> _subject;
            private IObserver<T> _observer;

            public Subscription(ControlSubject<T> subject, IObserver<T> observer)
            {
                _subject = subject;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null)
                {
                    lock (_subject._gate)
                    {
                        if (!_subject._isDisposed && _observer != null)
                        {
                            _subject._observers = _subject._observers.Remove(_observer);
                            _observer = null;
                        }
                    }
                }
            }
        }

        protected void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(string.Empty);
        }

        /// <summary>
        /// Unsubscribe all observers and release resources.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                _isDisposed = true;
                _observers = null;
                _value = default(T);
                _exception = null;
            }
        }
    }
}
