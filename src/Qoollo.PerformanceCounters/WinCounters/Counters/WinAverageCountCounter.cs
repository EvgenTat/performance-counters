﻿using Qoollo.PerformanceCounters.WinCounters.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Qoollo.PerformanceCounters.WinCounters.Counters
{
    /// <summary>
    /// Счётчик замера среднего значений для WinCounters
    /// </summary>
    public class WinAverageCountCounter : AverageCountCounter, IWinCounterInitialization
    {
        /// <summary>
        /// Дескриптор для счётчика WinAverageCountCounter
        /// </summary>
        private class Descriptor : WinCounterDescriptor
        {
            /// <summary>
            /// Конструктор Descriptor
            /// </summary>
            /// <param name="name">Имя счётчика</param>
            /// <param name="description">Описание счётчика</param>
            /// <param name="info">Информация о функционировании</param>
            public Descriptor(string name, string description, WinCountersWorkingInfo info)
                : base(name, description, CounterTypes.AverageCount, info)
            {
            }

            /// <summary>
            /// Создание счётчика
            /// </summary>
            /// <returns>Созданный счётчик</returns>
            public override Counter CreateCounter()
            {
                return new WinAverageCountCounter(this.Name, this.Description, this.Info);
            }
            /// <summary>
            /// Занести данные о необходимых счётчиках Windows в коллекцию
            /// </summary>
            /// <param name="col">Коллекция</param>
            public override void FillCounterCreationData(CounterCreationDataCollection col)
            {
                CounterCreationData data = new CounterCreationData(this.Name, this.Description, CounterHelper.ConvertCounterType(this.Type, Info.Prefer64BitCounters));
                CounterCreationData baseData = new CounterCreationData(this.Name + "_Base", this.Description, BaseCounterType);
                col.Add(data);
                col.Add(baseData);
            }
        }

        /// <summary>
        /// Создание дескриптора для счётчика WinAverageCountCounter
        /// </summary>
        /// <param name="name">Имя счётчика</param>
        /// <param name="description">Описание счётчика</param>
        /// <param name="info">Информация о функционировании</param>
        /// <returns>Созданный дескриптор</returns>
        internal static WinCounterDescriptor CreateDescriptor(string name, string description, WinCountersWorkingInfo info)
        {
            return new Descriptor(name, description, info);
        }

        // ===================

        /// <summary>
        /// Тип базового счётчика Windows
        /// </summary>
        private const PerformanceCounterType BaseCounterType = PerformanceCounterType.AverageBase;

        private readonly WinCountersWorkingInfo _info;
        private PerformanceCounter _winCounter;
        private PerformanceCounter _winBaseCounter;
        private volatile WinCounterState _state;

        /// <summary>
        /// Конструктор WinAverageCountCounter
        /// </summary>
        /// <param name="name">Имя счётчика</param>
        /// <param name="description">Описание счётчика</param>
        /// <param name="info">Информация о функционировании</param>
        internal WinAverageCountCounter(string name, string description, WinCountersWorkingInfo info)
            : base(name, description)
        {
            _state = WinCounterState.Created;
            _info = info;
        }

        /// <summary>
        /// Текущее значение
        /// </summary>
        public override double CurrentValue
        {
            get
            {
                var counterCpy = _winCounter;
                var baseCounterCpy = _winBaseCounter;
                if (counterCpy == null || baseCounterCpy == null)
                    return FailureNum;

                return (double)counterCpy.NextValue();
            }
        }

        /// <summary>
        /// Зарегистрировать измерение
        /// </summary>
        /// <param name="value">Новое значение</param>
        public override void RegisterValue(long value)
        {
            var counterCpy = _winCounter;
            var baseCounterCpy = _winBaseCounter;
            if (counterCpy == null || baseCounterCpy == null)
                return;

            counterCpy.IncrementBy(value);
            baseCounterCpy.Increment();
        }


        /// <summary>
        /// Сброс значения счётчика
        /// </summary>
        public override void Reset()
        {
            var counterCpy = _winCounter;
            var baseCounterCpy = _winBaseCounter;
            if (counterCpy == null || baseCounterCpy == null)
                return;

            _winCounter.RawValue = 0;
            _winBaseCounter.RawValue = 0;
        }


        /// <summary>
        /// Занести данные о необходимых счётчиках Windows в коллекцию
        /// </summary>
        /// <param name="col">Коллекция</param>
        void IWinCounterInitialization.CounterFillCreationData(CounterCreationDataCollection col)
        {
            CounterCreationData data = new CounterCreationData(this.Name, this.Description, CounterHelper.ConvertCounterType(this.Type, _info.Prefer64BitCounters));
            CounterCreationData baseData = new CounterCreationData(this.Name + "_Base", this.Description, BaseCounterType);
            col.Add(data);
            col.Add(baseData);
        }

        /// <summary>
        /// Проинициализировать счётчик Windows
        /// </summary>
        /// <param name="categoryName">Имя категории</param>
        /// <param name="instanceName">Имя инстанса (null, если одноинстансовая категория)</param>
        void IWinCounterInitialization.CounterInit(string categoryName, string instanceName)
        {
            if (_state == WinCounterState.Disposed)
                throw new ObjectDisposedException(this.GetType().Name);
            if (_state == WinCounterState.Initialized)
                return;

            _state = WinCounterState.Initialized;

            if (_info.IsLocalMachine)
            {
                _winBaseCounter = new PerformanceCounter(categoryName, this.Name + "_Base", instanceName ?? "", _info.ReadOnlyCounters);
                _winCounter = new PerformanceCounter(categoryName, this.Name, instanceName ?? "", _info.ReadOnlyCounters);

                if (!_info.ReadOnlyCounters)
                {
                    _winCounter.RawValue = 0;
                    _winBaseCounter.RawValue = 0;
                }
            }
            else
            {
                _winBaseCounter = new PerformanceCounter(categoryName, this.Name + "_Base", instanceName ?? "", _info.MachineName);
                _winCounter = new PerformanceCounter(categoryName, this.Name, instanceName ?? "", _info.MachineName);
            }
            _winCounter.NextValue();
        }

        /// <summary>
        /// Освободить счётчик
        /// </summary>
        /// <param name="removeInstance">Удалить ли его инстанс из Windows</param>
        void IWinCounterInitialization.CounterDispose(bool removeInstance)
        {
            _state = WinCounterState.Disposed;

            var oldVal = Interlocked.Exchange(ref _winCounter, null);
            if (oldVal != null)
            {
                if (removeInstance)
                    oldVal.RemoveInstance();
                oldVal.Dispose();
            }

            var oldBaseVal = Interlocked.Exchange(ref _winBaseCounter, null);
            if (oldBaseVal != null)
            {
                if (removeInstance)
                    oldBaseVal.RemoveInstance();
                oldBaseVal.Dispose();
            }
        }
    }
}
